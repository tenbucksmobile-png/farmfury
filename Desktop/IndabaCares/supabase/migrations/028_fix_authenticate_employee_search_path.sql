-- =============================================================================
-- Migration 028 — Fix authenticate_employee() search path
--
-- Problem:
--   authenticate_employee() (migration 025) has SET search_path = public.
--   The crypt() function from pgcrypto lives in the extensions schema.
--   When search_path = public only, crypt() cannot be resolved → the RPC
--   returns a PostgreSQL error → client shows "Login failed. Please try again."
--
-- Fix:
--   Recreate authenticate_employee() with SET search_path = public, extensions
--   so crypt() resolves correctly (same fix applied to first_time_authenticate
--   in migration 027).
-- =============================================================================

DROP FUNCTION IF EXISTS public.authenticate_employee(text, text, text);

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
  v_id        uuid;
  v_full_name text;
  v_hotel     text;
  v_token     uuid;
  v_rate_key  text;
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
  SELECT e.id, e.full_name, e.hotel
  INTO   v_id, v_full_name, v_hotel
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

  INSERT INTO public.employee_active_sessions (employee_id, hotel, expires_at)
  VALUES (v_id, v_hotel, now() + INTERVAL '14 days')
  RETURNING token INTO v_token;

  RETURN jsonb_build_object(
    'ok',        true,
    'token',     v_token,
    'id',        v_id,
    'full_name', v_full_name,
    'hotel',     v_hotel
  );

EXCEPTION WHEN others THEN
  RETURN jsonb_build_object(
    'ok',    false,
    'error', SQLERRM
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.authenticate_employee(text, text, text)
  TO anon, authenticated;

COMMENT ON FUNCTION public.authenticate_employee IS
  'Atomic login + session creation with built-in rate limiting.
   search_path includes extensions so crypt() (pgcrypto) resolves correctly.';
