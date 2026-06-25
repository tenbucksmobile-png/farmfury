-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 046 — Fix current_employee_id() column reference
--
-- Problem:
--   Migration 044 created current_employee_id() querying employee_active_sessions
--   with WHERE token = v_token.  Migration 032 rebuilt that table and renamed
--   the primary key column from `token` to `session_token`.
--   Result: every storage RLS check and avatar RPC call threw
--   "column token does not exist".
--
-- Fix:
--   Recreate current_employee_id() using the correct column name `session_token`.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.current_employee_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_raw_headers text;
  v_token       uuid;
  v_employee_id uuid;
BEGIN
  v_raw_headers := current_setting('request.headers', true);

  IF v_raw_headers IS NULL OR v_raw_headers = '' THEN
    RETURN NULL;
  END IF;

  BEGIN
    v_token := (v_raw_headers::json->>'x-session-token')::uuid;
  EXCEPTION WHEN others THEN
    RETURN NULL;
  END;

  IF v_token IS NULL THEN
    RETURN NULL;
  END IF;

  SELECT employee_id INTO v_employee_id
  FROM   public.employee_active_sessions
  WHERE  session_token = v_token          -- fixed: was `token`, now `session_token`
    AND  expires_at    > now();

  RETURN v_employee_id;
END;
$func$;

COMMENT ON FUNCTION public.current_employee_id IS
  'Reads x-session-token from request.headers and returns the authenticated '
  'employee UUID. Returns NULL for missing or expired tokens. '
  'Fixed in migration 046 to use session_token (renamed in migration 032).';
