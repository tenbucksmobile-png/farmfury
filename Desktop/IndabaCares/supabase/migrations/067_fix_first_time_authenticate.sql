-- =============================================================================
-- Migration 067 — Fix first_time_authenticate + admin password reset
--
-- Problem (migration 047):
--   first_time_authenticate() was rewritten with the OLD column names from
--   migration 017 (token, hotel) instead of the new ones from migration 032
--   (session_token, no hotel column).  The INSERT fails with:
--     column "token" of relation "employee_active_sessions" does not exist
--   The EXCEPTION block catches it and rolls back the password UPDATE, so the
--   employee is never activated and the user sees a raw Postgres error.
--
-- Fixes:
--   1. Recreate first_time_authenticate() using session_token (PK default).
--   2. Add reset_employee_password(p_id) — admin-only RPC to clear password_hash,
--      allowing the employee to re-register via the Register tab.
-- =============================================================================


-- ─── 1. Fix first_time_authenticate ──────────────────────────────────────────

DROP FUNCTION IF EXISTS public.first_time_authenticate(text, text, text, text);

CREATE OR REPLACE FUNCTION public.first_time_authenticate(
  p_employee_code text,
  p_hotel         text,
  p_full_name     text,
  p_new_password  text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, extensions
AS $$
DECLARE
  v_employee public.employees%ROWTYPE;
  v_token    uuid;
BEGIN
  -- ── 1. Password length check ────────────────────────────────────────────────
  IF length(p_new_password) < 8 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Password must be at least 8 characters.');
  END IF;

  IF length(p_new_password) > 128 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Password must not exceed 128 characters.');
  END IF;

  -- ── 2. Look up employee ──────────────────────────────────────────────────────
  SELECT * INTO v_employee
  FROM   public.employees
  WHERE  employee_code    = UPPER(TRIM(p_employee_code))
    AND  hotel            = TRIM(p_hotel)
    AND  LOWER(full_name) = LOWER(TRIM(p_full_name))
  LIMIT 1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'No employee record matched. Check your name, code, and hotel.'
    );
  END IF;

  -- ── 3. Reject if password already set ───────────────────────────────────────
  IF v_employee.password_hash IS NOT NULL THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Account already activated. Please use the Login tab.'
    );
  END IF;

  -- ── 4. Reject inactive accounts ─────────────────────────────────────────────
  IF v_employee.status <> 'active' THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Your account is not active. Please contact HR.'
    );
  END IF;

  -- ── 5. Set password hash ─────────────────────────────────────────────────────
  UPDATE public.employees
  SET    password_hash = crypt(p_new_password, gen_salt('bf', 12))
  WHERE  id = v_employee.id;

  -- ── 6. Create session (session_token is the PK with DEFAULT gen_random_uuid())
  INSERT INTO public.employee_active_sessions (employee_id, expires_at)
  VALUES (v_employee.id, now() + interval '14 days')
  RETURNING session_token INTO v_token;

  -- ── 7. Return success ────────────────────────────────────────────────────────
  RETURN jsonb_build_object(
    'ok',         true,
    'token',      v_token,
    'id',         v_employee.id,
    'full_name',  v_employee.full_name,
    'hotel',      v_employee.hotel,
    'department', v_employee.department
  );

EXCEPTION WHEN others THEN
  RETURN jsonb_build_object('ok', false, 'error', SQLERRM);
END;
$$;

GRANT EXECUTE ON FUNCTION public.first_time_authenticate(text, text, text, text)
  TO anon, authenticated;

COMMENT ON FUNCTION public.first_time_authenticate IS
  'Atomic first-time authentication: verifies identity, sets bcrypt password (cost 12),
   creates a session row (session_token PK, no hotel column) in a single transaction.
   Fixed in migration 067 — migration 047 regressed to old column names (token, hotel).';


-- ─── 2. Admin: reset_employee_password ───────────────────────────────────────
--
-- Clears an employee''s password_hash, allowing them to re-register via the
-- "Register" tab on the mobile app.  Called by the admin portal.
--
-- Safety: also revokes all active sessions for the employee.

CREATE OR REPLACE FUNCTION public.reset_employee_password(p_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  -- Revoke all active sessions
  DELETE FROM public.employee_active_sessions WHERE employee_id = p_id;

  -- Clear password hash so the employee can re-register
  UPDATE public.employees
  SET    password_hash = NULL
  WHERE  id = p_id;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Employee not found.');
  END IF;

  RETURN jsonb_build_object('ok', true);
END;
$$;

GRANT EXECUTE ON FUNCTION public.reset_employee_password(uuid)
  TO authenticated;

COMMENT ON FUNCTION public.reset_employee_password IS
  'Admin-only: clears an employee''s password hash and revokes all sessions.
   Allows the employee to re-register via the app Register tab.
   Should only be called via the admin portal (authenticated role).';
