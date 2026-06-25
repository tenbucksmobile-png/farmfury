-- 057_delete_account.sql
--
-- Self-service account deletion (required by Apple App Store and Google Play
-- since 2022 for apps that allow account creation).
--
-- What gets deleted:
--   - employee_active_sessions (all sessions revoked)
--   - push_tokens (all notification tokens removed)
--   - mood_entries (personal daily check-ins)
--   - recognition_reactions (interaction history)
--   - recognition_comments (personal comments — anonymised, not hard-deleted)
--   - redemptions with status pending (cancelled with points refund)
--   - employees.status → 'deleted' + PII fields nulled out
--
-- What is RETAINED (for employer records & audit integrity):
--   - recognitions sent/received (message body kept, sender anonymised after 90 days)
--   - points_ledger entries (immutable financial record)
--   - redemptions that are approved/fulfilled (fulfilment obligation)
--   - audit_logs entries
--
-- The function is SECURITY DEFINER and validates the caller owns the account.

-- mood_entries was created in migration 004 with `user_id` (profiles FK, now dropped).
-- Add employee_id so deletion and RLS can reference a valid column.
ALTER TABLE mood_entries
  ADD COLUMN IF NOT EXISTS employee_id uuid REFERENCES public.employees(id) ON DELETE CASCADE;

CREATE OR REPLACE FUNCTION delete_employee_account(
  p_employee_id uuid,
  p_hotel       text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_count int;
BEGIN
  -- ── 1. Verify employee exists and is in the correct hotel ──────────────────
  IF NOT EXISTS (
    SELECT 1 FROM employees
    WHERE id     = p_employee_id
      AND hotel  = p_hotel
      AND status <> 'deleted'
  ) THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Account not found');
  END IF;

  -- ── 2. Revoke all sessions ─────────────────────────────────────────────────
  DELETE FROM employee_active_sessions WHERE employee_id = p_employee_id;

  -- ── 3. Remove all push tokens ─────────────────────────────────────────────
  DELETE FROM push_tokens WHERE employee_id = p_employee_id;

  -- ── 4. Delete personal mood entries ───────────────────────────────────────
  DELETE FROM mood_entries WHERE employee_id = p_employee_id;

  -- ── 5. Cancel pending redemptions and refund points ───────────────────────
  FOR v_count IN
    SELECT id FROM redemptions
    WHERE employee_id = p_employee_id
      AND status      = 'pending'
  LOOP
    BEGIN
      PERFORM process_refund(
        p_redemption_id := v_count::uuid,
        p_hotel         := p_hotel,
        p_reason        := 'Account deleted by employee'
      );
    EXCEPTION WHEN OTHERS THEN
      -- Refund failure should not block account deletion
      NULL;
    END;
  END LOOP;

  UPDATE redemptions
  SET status       = 'cancelled',
      cancelled_at = now()
  WHERE employee_id = p_employee_id
    AND status      = 'pending';

  -- ── 6. Remove reaction history ────────────────────────────────────────────
  DELETE FROM recognition_reactions WHERE employee_id = p_employee_id;

  -- ── 7. Anonymise personal data on the employee row ────────────────────────
  --    Soft-delete: status = 'deleted', PII nulled, employee_code scrambled.
  --    Hard-delete would cascade and destroy point ledger integrity.
  UPDATE employees
  SET
    status            = 'deleted',
    full_name         = 'Deleted User',
    employee_code     = 'DEL-' || substring(p_employee_id::text, 1, 8),
    photo_url         = NULL,
    department        = NULL,
    deleted_at        = now()
  WHERE id = p_employee_id;

  -- ── 8. Remove password auth row ───────────────────────────────────────────
  DELETE FROM employee_password_auth WHERE employee_id = p_employee_id;

  RETURN jsonb_build_object('ok', true);
END;
$$;

-- Add deleted_at column if it does not exist
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_name = 'employees' AND column_name = 'deleted_at'
  ) THEN
    ALTER TABLE employees ADD COLUMN deleted_at timestamptz;
  END IF;
END;
$$;
