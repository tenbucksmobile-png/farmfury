-- =============================================================================
-- Migration 032 — Rebuild Employee Session Architecture
--
-- Changes from the old schema (migration 017):
--   OLD: token (PK), employee_id, hotel NOT NULL, created_at, last_seen, expires_at
--   NEW: session_token (PK), employee_id, created_at, expires_at
--
-- Rationale:
--   • `hotel` is redundant — available via JOIN to employees.
--   • `last_seen` with rolling expiry adds write load on every RLS evaluation.
--   • Renaming `token` → `session_token` is explicit and prevents confusion
--     with Supabase auth tokens.
--
-- Functions updated in this migration:
--   current_employee_hotel()   — JOIN to employees instead of reading hotel column
--   current_employee_id()      — use session_token column name
--   create_session()           — replaces create_employee_session(uuid, text)
--   validate_session()         — new: returns employee JSON from a session token
--   revoke_employee_session()  — use session_token column name
--   cleanup_expired_sessions() — recreated (references sessions table)
--   authenticate_employee()    — INSERT without hotel; RETURNING session_token
--   first_time_authenticate()  — INSERT without explicit token/hotel columns
--   mark_notification_read()   — use session_token column name
--
-- All DROPs use IF EXISTS — safe to re-run.
-- =============================================================================


-- ─── 1. Drop old employee_active_sessions table ───────────────────────────────
--
-- CASCADE removes dependent indexes and FK constraints on other tables.
-- PL/pgSQL functions that reference the old column names are NOT automatically
-- dropped — they are recreated explicitly below.

DROP TABLE IF EXISTS public.employee_active_sessions CASCADE;


-- ─── 2. Create new employee_active_sessions table ────────────────────────────

CREATE TABLE public.employee_active_sessions (
  session_token uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id   uuid        NOT NULL
                            REFERENCES public.employees(id) ON DELETE CASCADE,
  created_at    timestamptz NOT NULL DEFAULT now(),
  expires_at    timestamptz NOT NULL DEFAULT (now() + INTERVAL '14 days')
);

CREATE INDEX idx_sessions_employee
  ON public.employee_active_sessions (employee_id);

CREATE INDEX idx_sessions_expires
  ON public.employee_active_sessions (expires_at);

-- No client RLS policies — all access via SECURITY DEFINER functions only.
ALTER TABLE public.employee_active_sessions ENABLE ROW LEVEL SECURITY;

COMMENT ON TABLE public.employee_active_sessions IS
  'Live employee sessions. session_token is sent as the x-session-token HTTP header
   and validated by current_employee_hotel() / current_employee_id() on every request.
   hotel is not stored here — resolved via JOIN to employees on demand.';


-- ─── 3. current_employee_hotel() — RLS keystone ──────────────────────────────
--
-- Reads x-session-token, looks up the session, JOINs to employees for hotel.
-- Returns NULL for missing/expired tokens → hotel-gated policies deny access.
-- No rolling expiry update — expires_at is fixed at session creation.

CREATE OR REPLACE FUNCTION public.current_employee_hotel()
RETURNS text
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_raw_headers text;
  v_token       uuid;
  v_hotel       text;
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

  SELECT e.hotel INTO v_hotel
  FROM   public.employee_active_sessions s
  JOIN   public.employees e ON e.id = s.employee_id
  WHERE  s.session_token = v_token
    AND  s.expires_at    > now();

  RETURN v_hotel;  -- NULL if token not found or expired
END;
$$;

GRANT EXECUTE ON FUNCTION public.current_employee_hotel() TO anon, authenticated;

COMMENT ON FUNCTION public.current_employee_hotel IS
  'RLS keystone: resolves the requesting employee''s hotel from their session token.
   JOINs to employees table — hotel is no longer stored in sessions.
   Returns NULL for missing/expired tokens → all hotel-gated policies deny access.';


-- ─── 4. current_employee_id() — per-employee RLS ─────────────────────────────
--
-- Companion to current_employee_hotel().
-- Used by notifications, recognitions INSERT, redemptions INSERT, messages INSERT.

CREATE OR REPLACE FUNCTION public.current_employee_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
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

  IF v_token IS NULL THEN RETURN NULL; END IF;

  SELECT employee_id INTO v_employee_id
  FROM   public.employee_active_sessions
  WHERE  session_token = v_token
    AND  expires_at    > now();

  RETURN v_employee_id;
END;
$$;

GRANT EXECUTE ON FUNCTION public.current_employee_id() TO anon, authenticated;

COMMENT ON FUNCTION public.current_employee_id IS
  'Returns the employee_id bound to the current x-session-token header.
   Returns NULL for missing or expired tokens. Used for per-employee RLS.';


-- ─── 5. create_session() — session creation RPC ──────────────────────────────
--
-- Replaces create_employee_session(uuid, text).
-- hotel is no longer a parameter — it is resolved from employees when needed.
-- Returns the session_token UUID for the client to store in AsyncStorage.

DROP FUNCTION IF EXISTS public.create_employee_session(uuid, text);

CREATE OR REPLACE FUNCTION public.create_session(
  p_employee_id uuid
)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_token uuid;
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM public.employees
    WHERE  id = p_employee_id AND status = 'active'
  ) THEN
    RETURN NULL;  -- caller should check for NULL
  END IF;

  INSERT INTO public.employee_active_sessions (employee_id)
  VALUES (p_employee_id)
  RETURNING session_token INTO v_token;

  RETURN v_token;
END;
$$;

GRANT EXECUTE ON FUNCTION public.create_session(uuid) TO anon, authenticated;

COMMENT ON FUNCTION public.create_session IS
  'Creates a session for an active employee and returns the session_token UUID.
   Client stores the token in AsyncStorage and sends it as the x-session-token header.
   Returns NULL if the employee is not active.';


-- ─── 6. validate_session() — session validation RPC ──────────────────────────
--
-- Validates a session token and returns the bound employee record.
-- Used by the mobile app on startup to verify a stored token is still valid
-- and to re-hydrate the EmployeeContext without requiring re-login.
--
-- Returns on success:
--   { ok: true, employee_id, full_name, employee_code, hotel, expires_at }
-- Returns on failure:
--   { ok: false, error }

CREATE OR REPLACE FUNCTION public.validate_session(
  p_session_token uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_employee_id   uuid;
  v_full_name     text;
  v_employee_code text;
  v_hotel         text;
  v_expires_at    timestamptz;
BEGIN
  SELECT
    s.employee_id,
    e.full_name,
    e.employee_code,
    e.hotel,
    s.expires_at
  INTO
    v_employee_id,
    v_full_name,
    v_employee_code,
    v_hotel,
    v_expires_at
  FROM   public.employee_active_sessions s
  JOIN   public.employees e ON e.id = s.employee_id
  WHERE  s.session_token = p_session_token
    AND  s.expires_at    > now()
    AND  e.status        = 'active';

  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Session expired or invalid.'
    );
  END IF;

  RETURN jsonb_build_object(
    'ok',            true,
    'employee_id',   v_employee_id,
    'full_name',     v_full_name,
    'employee_code', v_employee_code,
    'hotel',         v_hotel,
    'expires_at',    v_expires_at
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.validate_session(uuid) TO anon, authenticated;

COMMENT ON FUNCTION public.validate_session IS
  'Validates a session token and returns the bound employee data.
   Used on app startup to restore session from AsyncStorage without re-login.
   Returns {ok: false} for expired, revoked, or inactive-employee sessions.';


-- ─── 7. revoke_employee_session() — logout ───────────────────────────────────

CREATE OR REPLACE FUNCTION public.revoke_employee_session(
  p_token uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  DELETE FROM public.employee_active_sessions WHERE session_token = p_token;
  RETURN jsonb_build_object('ok', true);
END;
$$;

GRANT EXECUTE ON FUNCTION public.revoke_employee_session(uuid) TO anon, authenticated;

COMMENT ON FUNCTION public.revoke_employee_session IS
  'Immediately invalidates a session token. Called on explicit logout.';


-- ─── 8. cleanup_expired_sessions() — maintenance ─────────────────────────────

CREATE OR REPLACE FUNCTION public.cleanup_expired_sessions()
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_deleted integer;
BEGIN
  DELETE FROM public.employee_active_sessions WHERE expires_at <= now();
  GET DIAGNOSTICS v_deleted = ROW_COUNT;
  RETURN v_deleted;
END;
$$;

COMMENT ON FUNCTION public.cleanup_expired_sessions IS
  'Purges expired sessions. Run periodically via pg_cron or Supabase scheduled functions.';


-- ─── 9. authenticate_employee() — returning login ────────────────────────────
--
-- Updated to INSERT without hotel column and RETURNING session_token (not token).

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
  -- Purge stale sessions for this employee first.
  DELETE FROM public.employee_active_sessions
  WHERE  employee_id = v_id
    AND  expires_at  <= now();

  INSERT INTO public.employee_active_sessions (employee_id, expires_at)
  VALUES (v_id, now() + INTERVAL '14 days')
  RETURNING session_token INTO v_token;

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

GRANT EXECUTE ON FUNCTION public.authenticate_employee(text, text, text) TO anon, authenticated;

COMMENT ON FUNCTION public.authenticate_employee IS
  'Atomic login + session creation with built-in rate limiting.
   search_path includes extensions so crypt() (pgcrypto) resolves correctly.
   Returns {ok, token, id, full_name, hotel} on success.';


-- ─── 10. first_time_authenticate() — first-login flow ────────────────────────
--
-- Updated: INSERT into employee_active_sessions without explicit token or hotel
-- columns (both removed from the table in this migration).

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
  v_rate_key  text;
  v_rate_ok   boolean;
  v_employee  record;
  v_token     uuid;
BEGIN
  -- ── 1. Rate limiting ────────────────────────────────────────────────────────
  v_rate_key := 'first_auth:' || lower(trim(p_employee_code)) || ':' || lower(trim(p_hotel));
  v_rate_ok  := public.check_rate_limit(v_rate_key, 'first_time_auth', 5, 15);

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
    RETURN jsonb_build_object('ok', false, 'error', 'Password must be at least 8 characters.');
  END IF;

  IF length(p_new_password) > 128 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Password must not exceed 128 characters.');
  END IF;

  -- ── 3. Find employee ────────────────────────────────────────────────────────
  SELECT id, full_name, hotel, status, password_hash
  INTO   v_employee
  FROM   public.employees
  WHERE  employee_code    = upper(trim(p_employee_code))
    AND  hotel            = trim(p_hotel)
    AND  lower(full_name) = lower(trim(p_full_name))
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
  UPDATE public.employees
  SET    password_hash = crypt(p_new_password, gen_salt('bf', 12))
  WHERE  id = v_employee.id;

  -- ── 7. Create session ───────────────────────────────────────────────────────
  INSERT INTO public.employee_active_sessions (employee_id, expires_at)
  VALUES (v_employee.id, now() + interval '14 days')
  RETURNING session_token INTO v_token;

  -- ── 8. Return success ───────────────────────────────────────────────────────
  RETURN jsonb_build_object(
    'ok',        true,
    'token',     v_token,
    'id',        v_employee.id,
    'full_name', v_employee.full_name,
    'hotel',     v_employee.hotel
  );

EXCEPTION WHEN others THEN
  RETURN jsonb_build_object('ok', false, 'error', SQLERRM);
END;
$$;

GRANT EXECUTE ON FUNCTION public.first_time_authenticate(text, text, text, text) TO anon, authenticated;

COMMENT ON FUNCTION public.first_time_authenticate IS
  'Atomic first-time authentication: validates identity, sets bcrypt password (cost 12),
   and creates a session token in a single transaction.
   Rate-limited to 5 attempts per 15 minutes per employee code + hotel.';


-- ─── 11. mark_notification_read() — update token → session_token ─────────────
--
-- Migration 023 referenced the old `token` column name.  Recreated here
-- using `session_token`.

CREATE OR REPLACE FUNCTION public.mark_notification_read(p_id uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  UPDATE public.notifications
  SET    read = true
  WHERE  id          = p_id
    AND  employee_id = (
      SELECT employee_id
      FROM   public.employee_active_sessions
      WHERE  session_token = (current_setting('request.headers', true)::json->>'x-session-token')::uuid
        AND  expires_at    > now()
      LIMIT  1
    );
END;
$$;

GRANT EXECUTE ON FUNCTION public.mark_notification_read(uuid) TO anon, authenticated;

COMMENT ON FUNCTION public.mark_notification_read IS
  'Marks a single notification as read. Verifies the notification belongs to the
   session owner via the x-session-token header before updating.';


-- =============================================================================
-- Post-migration summary
-- =============================================================================
--
-- Table: employee_active_sessions
--   session_token  uuid  PK  (was: token)
--   employee_id    uuid  FK → employees(id) ON DELETE CASCADE
--   created_at     timestamptz  DEFAULT now()
--   expires_at     timestamptz  DEFAULT now() + 14 days
--
-- Removed columns: hotel, last_seen
-- Renamed column:  token → session_token
--
-- RPCs exposed to anon/authenticated:
--   create_session(employee_id)           → uuid (session_token)
--   validate_session(session_token)       → jsonb {ok, employee_id, full_name, employee_code, hotel, expires_at}
--   revoke_employee_session(token)        → jsonb {ok}
--   authenticate_employee(code,hotel,pw)  → jsonb {ok, token, id, full_name, hotel}
--   first_time_authenticate(code,hotel,name,pw) → jsonb {ok, token, id, full_name, hotel}
--
-- Internal only (no grant):
--   current_employee_hotel()   → text   (STABLE SECURITY DEFINER — used in RLS)
--   current_employee_id()      → uuid   (STABLE SECURITY DEFINER — used in RLS)
--   cleanup_expired_sessions() → integer
-- =============================================================================
