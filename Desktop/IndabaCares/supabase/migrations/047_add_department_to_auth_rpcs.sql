-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 047 — Return department from auth RPCs
--
-- The employees table already has a department column (migration 014).
-- Both auth RPCs were not returning it.  This migration patches both so that
-- the mobile app can display the employee's department in the feed header.
-- ─────────────────────────────────────────────────────────────────────────────

-- ─── 1. authenticate_employee — returning login ───────────────────────────────

CREATE OR REPLACE FUNCTION public.authenticate_employee(
  p_employee_code text,
  p_hotel         text,
  p_password      text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, extensions
AS $$
DECLARE
  v_id         uuid;
  v_full_name  text;
  v_hotel      text;
  v_department text;
  v_token      uuid;
  v_rate_key   text;
BEGIN
  -- ── Rate limit (5 attempts per 15 minutes per account) ───────────────────
  v_rate_key := UPPER(TRIM(p_employee_code)) || '::' || TRIM(p_hotel);

  IF NOT public.check_rate_limit(v_rate_key, 'employee_login', 5, 15) THEN
    RETURN jsonb_build_object(
      'ok',           false,
      'rate_limited', true,
      'error',        'Too many failed login attempts. Please wait 15 minutes and try again.'
    );
  END IF;

  -- ── Credential verification ───────────────────────────────────────────────
  SELECT e.id, e.full_name, e.hotel, e.department
  INTO   v_id, v_full_name, v_hotel, v_department
  FROM   public.employees e
  WHERE  e.employee_code = UPPER(TRIM(p_employee_code))
    AND  e.hotel         = TRIM(p_hotel)
    AND  e.status        = 'active'
    AND  e.password_hash IS NOT NULL
    AND  e.password_hash = crypt(p_password, e.password_hash)
  LIMIT 1;

  IF NOT FOUND THEN
    PERFORM public.record_rate_limit(v_rate_key, 'employee_login');
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Invalid employee code or password.'
    );
  END IF;

  -- ── Create session ────────────────────────────────────────────────────────
  DELETE FROM public.employee_active_sessions
  WHERE  employee_id = v_id
    AND  expires_at  <= now();

  INSERT INTO public.employee_active_sessions (employee_id, expires_at)
  VALUES (v_id, now() + INTERVAL '14 days')
  RETURNING session_token INTO v_token;

  RETURN jsonb_build_object(
    'ok',         true,
    'token',      v_token,
    'id',         v_id,
    'full_name',  v_full_name,
    'hotel',      v_hotel,
    'department', v_department
  );

EXCEPTION WHEN others THEN
  RETURN jsonb_build_object(
    'ok',    false,
    'error', SQLERRM
  );
END;
$$;

-- ─── 2. first_time_authenticate — first login ─────────────────────────────────

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
  -- ── 1. Look up employee ──────────────────────────────────────────────────
  SELECT * INTO v_employee
  FROM   public.employees
  WHERE  employee_code = UPPER(TRIM(p_employee_code))
    AND  hotel         = TRIM(p_hotel)
    AND  LOWER(full_name) = LOWER(TRIM(p_full_name))
  LIMIT 1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'No employee record matched. Check your name, code, and hotel.'
    );
  END IF;

  -- ── 2. Reject if password already set ───────────────────────────────────
  IF v_employee.password_hash IS NOT NULL THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Account already activated. Please use the returning login.'
    );
  END IF;

  -- ── 3. Reject inactive accounts ──────────────────────────────────────────
  IF v_employee.status <> 'active' THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Your account is not active. Please contact HR.'
    );
  END IF;

  -- ── 4. Set password hash ─────────────────────────────────────────────────
  UPDATE public.employees
  SET    password_hash = crypt(p_new_password, gen_salt('bf', 12))
  WHERE  id = v_employee.id;

  -- ── 5. Create session ────────────────────────────────────────────────────
  v_token := gen_random_uuid();

  INSERT INTO public.employee_active_sessions (token, employee_id, hotel, expires_at)
  VALUES (v_token, v_employee.id, v_employee.hotel, now() + interval '14 days');

  -- ── 6. Return success with department ────────────────────────────────────
  RETURN jsonb_build_object(
    'ok',         true,
    'token',      v_token,
    'id',         v_employee.id,
    'full_name',  v_employee.full_name,
    'hotel',      v_employee.hotel,
    'department', v_employee.department
  );

EXCEPTION WHEN others THEN
  RETURN jsonb_build_object(
    'ok',    false,
    'error', SQLERRM
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.authenticate_employee(text, text, text)    TO anon, authenticated;
GRANT EXECUTE ON FUNCTION public.first_time_authenticate(text, text, text, text) TO anon, authenticated;

COMMENT ON FUNCTION public.authenticate_employee IS
  'Atomic login + session creation with built-in rate limiting.
   Returns {ok, token, id, full_name, hotel, department} on success.';

COMMENT ON FUNCTION public.first_time_authenticate IS
  'First-time login: verifies identity, sets password, creates session atomically.
   Returns {ok, token, id, full_name, hotel, department} on success.';
