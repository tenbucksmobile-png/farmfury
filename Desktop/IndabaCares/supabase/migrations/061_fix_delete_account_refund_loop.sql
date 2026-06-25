-- 061_fix_delete_account_refund_loop.sql
--
-- CRITICAL BUG FIX: delete_employee_account had a type mismatch in the refund loop.
--
-- Original code declared `v_count int` as the loop variable, then iterated over
-- `SELECT id FROM redemptions` — where `id` is uuid. PostgreSQL cannot cast uuid
-- to int, so the loop threw "invalid input syntax for type integer" at runtime.
-- Any employee with a pending redemption could not delete their account.
--
-- Fix: Replace `v_count int` with `v_redemption_id uuid` throughout the loop.
-- The overall deletion logic and transaction integrity are unchanged.

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
  v_redemption_id uuid;   -- ← was incorrectly declared as int
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

  -- ── 5. Cancel pending redemptions and attempt refund for each ─────────────
  FOR v_redemption_id IN
    SELECT id FROM redemptions
    WHERE employee_id = p_employee_id
      AND status      = 'pending'
  LOOP
    BEGIN
      PERFORM process_refund(
        p_redemption_id := v_redemption_id,   -- ← uuid now, not int
        p_hotel         := p_hotel,
        p_reason        := 'Account deleted by employee'
      );
    EXCEPTION WHEN OTHERS THEN
      -- Refund failure must not block account deletion
      NULL;
    END;
  END LOOP;

  -- Mark any still-pending redemptions as cancelled (belt-and-suspenders in
  -- case process_refund left them in pending state after an error above).
  UPDATE redemptions
  SET status       = 'cancelled',
      cancelled_at = now()
  WHERE employee_id = p_employee_id
    AND status      = 'pending';

  -- ── 6. Remove reaction history ────────────────────────────────────────────
  DELETE FROM recognition_reactions WHERE employee_id = p_employee_id;

  -- ── 7. Anonymise personal data on the employee row ────────────────────────
  UPDATE employees
  SET
    status        = 'deleted',
    full_name     = 'Deleted User',
    employee_code = 'DEL-' || substring(p_employee_id::text, 1, 8),
    photo_url     = NULL,
    department    = NULL,
    deleted_at    = now()
  WHERE id = p_employee_id;

  -- ── 8. Remove password auth row ───────────────────────────────────────────
  DELETE FROM employee_password_auth WHERE employee_id = p_employee_id;

  RETURN jsonb_build_object('ok', true);
END;
$$;
