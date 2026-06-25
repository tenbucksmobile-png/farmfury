-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 017 — Hotel-level Row Level Security (Tenant Isolation)
--
-- Context
-- ───────
-- This app uses custom employee authentication (no Supabase Auth JWT).
-- Because there is no JWT, request.jwt.claims is always empty.
-- We cannot use the standard current_user_id() / current_company_id()
-- pattern from migration 006.
--
-- Architecture
-- ────────────
-- 1. employee_active_sessions table
--      Tracks live sessions.  On login the app calls create_employee_session()
--      which returns a UUID token.  That token is stored in AsyncStorage and
--      sent as the x-session-token HTTP header on every Supabase request.
--
-- 2. current_employee_hotel() helper  (SECURITY DEFINER)
--      Reads x-session-token from request.headers, looks up the hotel from
--      employee_active_sessions, and returns it.  Returns NULL when no valid
--      session exists — causing all policies to deny access automatically.
--
-- 3. RLS policies on every data table
--      All SELECT / INSERT / UPDATE policies require:
--        hotel = public.current_employee_hotel()
--      This is enforced at the database layer regardless of what the
--      application sends in query filters.
--
-- 4. Defense-in-depth
--      Layer 1 (application)  : explicit .eq('hotel', employee.hotel) in hooks
--      Layer 2 (database RLS) : hotel = current_employee_hotel() in policies
--      Layer 3 (service role) : admin operations bypass RLS via service_role key
-- ─────────────────────────────────────────────────────────────────────────────


-- ─── 0. Allowed hotel list (single source of truth) ──────────────────────────

-- Used in CHECK constraints and session validation.
-- Update here if hotels change; all constraints reference this function.

CREATE OR REPLACE FUNCTION public.is_valid_hotel(p_hotel text)
RETURNS boolean
LANGUAGE sql
IMMUTABLE
AS $$
  SELECT p_hotel IN (
    'Indaba Hotel',
    'Indaba Lodge Richards Bay',
    'Indaba Lodge Gaborone',
    'Chobe Safari Lodge',
    'Chobe Bush Lodge',
    'Nata Lodge',
    'African Procurement Agencies'
  );
$$;


-- ─── 1. employee_active_sessions ─────────────────────────────────────────────
--
-- One row per active login session.
-- Tokens expire after 30 days of inactivity; revoked on explicit logout.

CREATE TABLE IF NOT EXISTS public.employee_active_sessions (
  token       uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id uuid        NOT NULL
                          REFERENCES public.employees(id) ON DELETE CASCADE,
  hotel       text        NOT NULL
                          CHECK (public.is_valid_hotel(hotel)),
  created_at  timestamptz NOT NULL DEFAULT now(),
  last_seen   timestamptz NOT NULL DEFAULT now(),
  expires_at  timestamptz NOT NULL DEFAULT (now() + INTERVAL '30 days')
);

CREATE INDEX IF NOT EXISTS idx_sessions_employee
  ON public.employee_active_sessions (employee_id);

CREATE INDEX IF NOT EXISTS idx_sessions_expires
  ON public.employee_active_sessions (expires_at);

-- No RLS on this table — it is only ever accessed via SECURITY DEFINER functions.
-- Direct client access is blocked (anon and authenticated have no policies).
ALTER TABLE public.employee_active_sessions ENABLE ROW LEVEL SECURITY;
-- (No policies created → no role except service_role can read/write it.)

COMMENT ON TABLE public.employee_active_sessions IS
  'Tracks live employee sessions.  x-session-token header is matched here by
   current_employee_hotel() to determine the requesting employee''s hotel.';


-- ─── 2. current_employee_hotel() — the RLS keystone ─────────────────────────
--
-- Called inside every RLS policy.
-- Reads the x-session-token header that the mobile client sends with each
-- Supabase request, validates it, and returns the employee's hotel.
--
-- Returns NULL when:
--   - No header present (unauthenticated request)
--   - Token not found in sessions table
--   - Token has expired
--
-- A NULL return causes USING (hotel = NULL) → false → row is denied.

CREATE OR REPLACE FUNCTION public.current_employee_hotel()
RETURNS text
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_raw_headers text;
  v_token       uuid;
  v_hotel       text;
BEGIN
  -- PostgREST exposes headers as a JSON string in this setting.
  v_raw_headers := current_setting('request.headers', true);

  IF v_raw_headers IS NULL OR v_raw_headers = '' THEN
    RETURN NULL;
  END IF;

  -- Parse the session token from the x-session-token header.
  BEGIN
    v_token := (v_raw_headers::json->>'x-session-token')::uuid;
  EXCEPTION WHEN others THEN
    RETURN NULL;   -- malformed header or non-uuid value
  END;

  IF v_token IS NULL THEN
    RETURN NULL;
  END IF;

  -- Look up the hotel, refresh last_seen, enforce expiry.
  UPDATE public.employee_active_sessions
  SET    last_seen  = now(),
         expires_at = now() + INTERVAL '30 days'  -- rolling expiry
  WHERE  token      = v_token
    AND  expires_at > now()
  RETURNING hotel INTO v_hotel;

  RETURN v_hotel;   -- NULL if token not found or expired
END;
$func$;

COMMENT ON FUNCTION public.current_employee_hotel IS
  'Reads x-session-token from request.headers and returns the employee''s hotel.
   Used in all hotel-isolation RLS policies.  Returns NULL for invalid/expired tokens.';


-- ─── 3. Session management RPCs ──────────────────────────────────────────────

-- ── create_employee_session ───────────────────────────────────────────────────
-- Called immediately after a successful login (firstAuthentication or returningLogin).
-- Returns the UUID token that the client stores in AsyncStorage and sends as
-- x-session-token on every subsequent request.

CREATE OR REPLACE FUNCTION public.create_employee_session(
  p_employee_id uuid,
  p_hotel       text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_token uuid;
BEGIN
  -- Verify the employee is active
  IF NOT EXISTS (
    SELECT 1 FROM public.employees
    WHERE id = p_employee_id AND status = 'active'
  ) THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Employee account is not active.'
    );
  END IF;

  -- Insert session row; gen_random_uuid() produces the token
  INSERT INTO public.employee_active_sessions (employee_id, hotel)
  VALUES (p_employee_id, p_hotel)
  RETURNING token INTO v_token;

  RETURN jsonb_build_object('ok', true, 'token', v_token);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.create_employee_session(uuid, text) TO anon;
GRANT EXECUTE ON FUNCTION public.create_employee_session(uuid, text) TO authenticated;

COMMENT ON FUNCTION public.create_employee_session IS
  'Creates a session row and returns {ok, token}.  Client stores the token in
   AsyncStorage and sends it as x-session-token header on every request.';


-- ── revoke_employee_session ───────────────────────────────────────────────────
-- Called on explicit logout.  Immediately invalidates the token.

CREATE OR REPLACE FUNCTION public.revoke_employee_session(
  p_token uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
BEGIN
  DELETE FROM public.employee_active_sessions WHERE token = p_token;
  RETURN jsonb_build_object('ok', true);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.revoke_employee_session(uuid) TO anon;
GRANT EXECUTE ON FUNCTION public.revoke_employee_session(uuid) TO authenticated;


-- ── cleanup_expired_sessions ──────────────────────────────────────────────────
-- Run periodically (e.g. via pg_cron or Supabase scheduled function).

CREATE OR REPLACE FUNCTION public.cleanup_expired_sessions()
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE v_deleted integer;
BEGIN
  DELETE FROM public.employee_active_sessions WHERE expires_at <= now();
  GET DIAGNOSTICS v_deleted = ROW_COUNT;
  RETURN v_deleted;
END;
$func$;


-- ─── 4. Add hotel column to tables that do not yet have it ───────────────────

ALTER TABLE public.recognitions
  ADD COLUMN IF NOT EXISTS hotel text;

ALTER TABLE public.rewards
  ADD COLUMN IF NOT EXISTS hotel text;

ALTER TABLE public.redemptions
  ADD COLUMN IF NOT EXISTS hotel text;

ALTER TABLE public.leaderboard_cache
  ADD COLUMN IF NOT EXISTS hotel text;

ALTER TABLE public.notifications
  ADD COLUMN IF NOT EXISTS hotel text;

-- messages table (create if it does not exist)
CREATE TABLE IF NOT EXISTS public.messages (
  id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  hotel       text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  sender_id   uuid        NOT NULL REFERENCES public.employees(id),
  body        text        NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_messages_hotel
  ON public.messages (hotel);


-- ─── 5. Enable RLS on all data tables ────────────────────────────────────────

ALTER TABLE public.recognitions       ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.rewards            ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.redemptions        ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.leaderboard_cache  ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.notifications      ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.messages           ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.employees          ENABLE ROW LEVEL SECURITY;  -- already done in 014


-- ─── 6. Drop any conflicting legacy policies ─────────────────────────────────
-- Legacy policies from migration 006 use company_id isolation.
-- Remove them so the hotel policies below are the sole authority.

DROP POLICY IF EXISTS "recognitions_select"      ON public.recognitions;
DROP POLICY IF EXISTS "recognitions_insert"      ON public.recognitions;
DROP POLICY IF EXISTS "rewards_select"           ON public.rewards;
DROP POLICY IF EXISTS "rewards_insert_admin"     ON public.rewards;
DROP POLICY IF EXISTS "redemptions_select_own"   ON public.redemptions;
DROP POLICY IF EXISTS "redemptions_insert_own"   ON public.redemptions;
DROP POLICY IF EXISTS "leaderboard_select"       ON public.leaderboard_cache;
DROP POLICY IF EXISTS "notifications_select_own" ON public.notifications;
DROP POLICY IF EXISTS "notifications_update_own" ON public.notifications;


-- ─── 7. Hotel isolation policies ─────────────────────────────────────────────
--
-- Pattern:
--   USING  (hotel = public.current_employee_hotel())   — read gate
--   WITH CHECK (hotel = public.current_employee_hotel())  — write gate
--
-- If current_employee_hotel() returns NULL (no valid session),
-- NULL = NULL evaluates to NULL (not TRUE), so access is denied.

-- ── recognitions ─────────────────────────────────────────────────────────────

CREATE POLICY "recognitions_hotel_select"
  ON public.recognitions
  FOR SELECT
  TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

CREATE POLICY "recognitions_hotel_insert"
  ON public.recognitions
  FOR INSERT
  TO anon, authenticated
  WITH CHECK (hotel = public.current_employee_hotel());

-- ── rewards ───────────────────────────────────────────────────────────────────

CREATE POLICY "rewards_hotel_select"
  ON public.rewards
  FOR SELECT
  TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

-- Rewards are created/updated by admins (service_role bypasses RLS).
-- No INSERT/UPDATE policy for anon/authenticated.

-- ── redemptions ───────────────────────────────────────────────────────────────

CREATE POLICY "redemptions_hotel_select"
  ON public.redemptions
  FOR SELECT
  TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

CREATE POLICY "redemptions_hotel_insert"
  ON public.redemptions
  FOR INSERT
  TO anon, authenticated
  WITH CHECK (hotel = public.current_employee_hotel());

-- ── leaderboard_cache ─────────────────────────────────────────────────────────

CREATE POLICY "leaderboard_hotel_select"
  ON public.leaderboard_cache
  FOR SELECT
  TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

-- Leaderboard is computed server-side (service_role); no client INSERT/UPDATE.

-- ── notifications ─────────────────────────────────────────────────────────────

CREATE POLICY "notifications_hotel_select"
  ON public.notifications
  FOR SELECT
  TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

CREATE POLICY "notifications_hotel_update"
  ON public.notifications
  FOR UPDATE
  TO anon, authenticated
  USING    (hotel = public.current_employee_hotel())
  WITH CHECK (hotel = public.current_employee_hotel());

-- ── messages ──────────────────────────────────────────────────────────────────

CREATE POLICY "messages_hotel_select"
  ON public.messages
  FOR SELECT
  TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

CREATE POLICY "messages_hotel_insert"
  ON public.messages
  FOR INSERT
  TO anon, authenticated
  WITH CHECK (hotel = public.current_employee_hotel());

-- ── employees ─────────────────────────────────────────────────────────────────
-- Replace the JWT-based policies from migration 014 with hotel-session ones.

DROP POLICY IF EXISTS employees_select_own   ON public.employees;
DROP POLICY IF EXISTS employees_select_admin ON public.employees;

-- Employees may read only members of their own hotel.
CREATE POLICY "employees_hotel_select"
  ON public.employees
  FOR SELECT
  TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

-- Auth RPCs (verify_employee_identity, login_employee, etc.) are
-- SECURITY DEFINER and bypass RLS — they can always read the table.


-- ─── 8. Scheduled cleanup (pg_cron — enable if available) ───────────────────
-- Uncomment if pg_cron extension is enabled on your Supabase project:
--
-- SELECT cron.schedule(
--   'cleanup-expired-sessions',
--   '0 3 * * *',                         -- 03:00 UTC daily
--   'SELECT public.cleanup_expired_sessions()'
-- );


-- ─── 9. Comments ─────────────────────────────────────────────────────────────

COMMENT ON TABLE public.employee_active_sessions IS
  'Live employee sessions.  Token sent as x-session-token HTTP header; looked up
   by current_employee_hotel() which drives all hotel-isolation RLS policies.';

COMMENT ON FUNCTION public.current_employee_hotel IS
  'RLS keystone: resolves the requesting employee''s hotel from their session token.
   Returns NULL for missing/expired tokens → all hotel-gated policies deny access.';
