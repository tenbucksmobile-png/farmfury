-- =============================================================================
-- Migration 027 — Atomic first-time authentication
--
-- Problem (migrations 025/026):
--   The three-step first-time auth flow (verify_employee_identity →
--   set_employee_password → authenticate_employee) is fragile because
--   set_employee_password() runs as a separate RPC call.  Any trigger, GUC
--   issue, or partial migration failure causes "Could not save password."
--
-- Fix:
--   A single SECURITY DEFINER function first_time_authenticate() that does
--   everything atomically in one DB transaction:
--     1. Rate-limit check (5 attempts per 15 minutes per code+hotel)
--     2. Validate password length (≥ 8, ≤ 128 chars)
--     3. Lookup employee by code + hotel + case-insensitive name
--     4. Reject if account already has a password
--     5. Reject if account is not active
--     6. Write password_hash directly via UPDATE (no trigger conflict —
--        trg_guard_points_balance only fires on UPDATE OF points_balance)
--     7. Create session token (includes hotel column required by migration 017)
--     8. Return {ok, token, id, full_name, hotel}
-- =============================================================================

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
  v_rate_key   text;
  v_rate_ok    boolean;
  v_employee   record;
  v_token      uuid;
  v_expires_at timestamptz;
BEGIN
  -- ── 1. Rate limiting (5 attempts per 15 minutes per code+hotel) ─────────────
  v_rate_key := 'first_auth:' || lower(trim(p_employee_code)) || ':' || lower(trim(p_hotel));

  v_rate_ok := public.check_rate_limit(v_rate_key, 'first_time_auth', 5, 15);

  IF NOT v_rate_ok THEN
    RETURN jsonb_build_object(
      'ok',           false,
      'rate_limited', true,
      'error',        'Too many attempts. Please try again in 15 minutes.'
    );
  END IF;

  PERFORM public.record_rate_limit(v_rate_key, 'first_time_auth');

  -- ── 2. Password length check ────────────────────────────────────────────────
  IF length(p_new_password) < 8 THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Password must be at least 8 characters.'
    );
  END IF;

  IF length(p_new_password) > 128 THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Password must not exceed 128 characters.'
    );
  END IF;

  -- ── 3. Find employee ────────────────────────────────────────────────────────
  SELECT id, full_name, hotel, status, password_hash
  INTO   v_employee
  FROM   public.employees
  WHERE  employee_code      = upper(trim(p_employee_code))
    AND  hotel              = trim(p_hotel)
    AND  lower(full_name)   = lower(trim(p_full_name))
  LIMIT  1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Employee not recognised. Check your name, code, and hotel.'
    );
  END IF;

  -- ── 4. Reject if already has a password ────────────────────────────────────
  IF v_employee.password_hash IS NOT NULL THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'This account already has a password. Please use the Login tab.'
    );
  END IF;

  -- ── 5. Reject inactive accounts ────────────────────────────────────────────
  IF v_employee.status <> 'active' THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Your account is not active. Please contact HR.'
    );
  END IF;

  -- ── 6. Set password hash ────────────────────────────────────────────────────
  --   Only touches password_hash — the trg_guard_points_balance trigger
  --   (migration 026) is scoped to UPDATE OF points_balance and never fires here.
  UPDATE public.employees
  SET    password_hash = crypt(p_new_password, gen_salt('bf', 12))
  WHERE  id = v_employee.id;

  -- ── 7. Create session ───────────────────────────────────────────────────────
  --   employee_active_sessions (migration 017) requires hotel NOT NULL.
  v_token      := gen_random_uuid();
  v_expires_at := now() + interval '14 days';

  INSERT INTO public.employee_active_sessions (token, employee_id, hotel, expires_at)
  VALUES (v_token, v_employee.id, v_employee.hotel, v_expires_at);

  -- ── 8. Return success ───────────────────────────────────────────────────────
  RETURN jsonb_build_object(
    'ok',        true,
    'token',     v_token,
    'id',        v_employee.id,
    'full_name', v_employee.full_name,
    'hotel',     v_employee.hotel
  );

EXCEPTION WHEN others THEN
  RETURN jsonb_build_object(
    'ok',    false,
    'error', SQLERRM
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.first_time_authenticate(text, text, text, text)
  TO anon, authenticated;

COMMENT ON FUNCTION public.first_time_authenticate IS
  'Atomic first-time authentication: validates identity, sets bcrypt password (cost 12),
   and creates a session token in a single transaction.
   Replaces the fragile three-step verify_employee_identity → set_employee_password
   → authenticate_employee flow.
   Rate-limited to 5 attempts per 15 minutes per employee code + hotel.';
