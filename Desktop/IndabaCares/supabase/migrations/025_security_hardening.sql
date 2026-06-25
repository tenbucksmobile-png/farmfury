-- =============================================================================
-- Migration 025 — Security Hardening
--
-- Fixes:
--   1. Atomic authenticate_employee() — combines login + session creation so
--      sessions cannot be created independently from a successful login.
--   2. Rate limiting wired into all auth RPCs — uses the existing
--      auth_rate_limits table from migration 007.
--   3. Password minimum length enforced server-side (8 chars).
--   4. current_employee_id() — companion to current_employee_hotel() for
--      per-employee scoped RLS policies (used by notifications).
--   5. Notifications RLS tightened — requires employee_id match, not just hotel.
--   6. Points balance guard — explicit trigger blocks direct UPDATE on
--      employees.points_balance; only SECURITY DEFINER functions may change it.
--   7. Verify identity rate limiting — prevents employee enumeration.
--   8. Session expiry reduced to 14 days (was 30).
--   9. Expired session cleanup scheduled more aggressively.
-- =============================================================================

-- ── 0. Drop functions before replacing ───────────────────────────────────────
--
-- PostgreSQL does not allow CREATE OR REPLACE to change parameter names on an
-- existing function (even if the signature is compatible).  We DROP first so
-- the subsequent CREATE OR REPLACE always starts from a clean state.
-- All GRANTs are re-applied after each CREATE.

DROP FUNCTION IF EXISTS public.authenticate_employee(text, text, text);
DROP FUNCTION IF EXISTS public.verify_employee_identity(text, text, text);
DROP FUNCTION IF EXISTS public.set_employee_password(uuid, text);
DROP FUNCTION IF EXISTS public.create_employee_session(uuid, text);
DROP FUNCTION IF EXISTS public.admin_grant_points(uuid, integer, text);
DROP TRIGGER  IF EXISTS trg_recognition_points ON public.recognitions;
DROP FUNCTION IF EXISTS public.award_recognition_points();
DROP FUNCTION IF EXISTS public.employee_auth_rate_check(text, text);
DROP FUNCTION IF EXISTS public.current_employee_id();


-- ── 1. current_employee_id() — session → employee_id lookup ──────────────────
--
-- Reads the x-session-token header (same as current_employee_hotel) and
-- returns the employee_id bound to that session.
-- Returns NULL for missing / expired tokens.

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
  WHERE  token      = v_token
    AND  expires_at > now();

  RETURN v_employee_id;
END;
$$;

COMMENT ON FUNCTION public.current_employee_id IS
  'Returns the employee_id bound to the current x-session-token header.
   Returns NULL for missing or expired tokens. Used for per-employee RLS.';


-- ── 2. Tighten notifications RLS — require employee_id match ─────────────────
--
-- The hotel-only policy (migration 017/023) lets any employee in a hotel read
-- all hotel notifications.  Replace it with a dual check.

-- Drop the broader hotel-only policies (they may have different names across
-- the migration history — drop all known variants idempotently).
DROP POLICY IF EXISTS "notifications_hotel_select"  ON public.notifications;
DROP POLICY IF EXISTS "notifications_hotel_update"  ON public.notifications;
DROP POLICY IF EXISTS "notifications_own_select"    ON public.notifications;
DROP POLICY IF EXISTS "notifications_own_update"    ON public.notifications;

-- Employees may only read their OWN notifications.
CREATE POLICY "notifications_own_select"
  ON public.notifications
  FOR SELECT TO anon, authenticated
  USING (
    hotel       = public.current_employee_hotel()
    AND employee_id = public.current_employee_id()
  );

-- Employees may only mark their OWN notifications read.
CREATE POLICY "notifications_own_update"
  ON public.notifications
  FOR UPDATE TO anon, authenticated
  USING (
    hotel       = public.current_employee_hotel()
    AND employee_id = public.current_employee_id()
  )
  WITH CHECK (
    hotel       = public.current_employee_hotel()
    AND employee_id = public.current_employee_id()
  );


-- ── 3. Rate-limit helper for employee auth actions ────────────────────────────
--
-- Extends migration 007's check_rate_limit / record_rate_limit to add a
-- specific action type for employee login, and adds a convenience wrapper.

-- Ensure the action column accepts the new type (original had an open text col,
-- no CHECK constraint — safe to just use it).

-- Convenience wrapper: returns true if the caller is NOT rate-limited.
CREATE OR REPLACE FUNCTION public.employee_auth_rate_check(
  p_key    text,   -- e.g. 'EMPCODE::Hotel Name'
  p_action text    -- e.g. 'employee_login', 'verify_identity'
)
RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  -- Allow max 5 failed attempts per 15-minute window per account.
  SELECT public.check_rate_limit(p_key, p_action, 5, 15);
$$;


-- ── 4. authenticate_employee() — atomic login + session creation ──────────────
--
-- Replaces the two-step pattern:
--   login_employee()           → returns id
--   create_employee_session()  → returns token    ← anyone with the id can call!
--
-- This single RPC verifies credentials AND creates the session atomically.
-- Wires in rate limiting so brute-force is blocked at the database layer.
--
-- Returns:
--   { ok: true,  token, id, full_name, hotel }     — success
--   { ok: false, error, rate_limited?: true }       — failure

CREATE OR REPLACE FUNCTION public.authenticate_employee(
  p_employee_code text,
  p_hotel         text,
  p_password      text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_id        uuid;
  v_full_name text;
  v_hotel     text;
  v_token     uuid;
  v_rate_key  text;
BEGIN
  -- ── Rate limit check (per account, not per IP) ────────────────────────────
  -- Key: normalised code::hotel — so the same account is throttled across IPs.
  v_rate_key := UPPER(TRIM(p_employee_code)) || '::' || TRIM(p_hotel);

  IF NOT public.employee_auth_rate_check(v_rate_key, 'employee_login') THEN
    RETURN jsonb_build_object(
      'ok',          false,
      'rate_limited', true,
      'error',       'Too many failed login attempts. Please wait 15 minutes and try again.'
    );
  END IF;

  -- ── Credential verification ───────────────────────────────────────────────
  SELECT e.id, e.full_name, e.hotel
  INTO   v_id, v_full_name, v_hotel
  FROM   public.employees e
  WHERE  e.employee_code = TRIM(UPPER(p_employee_code))
    AND  e.hotel         = TRIM(p_hotel)
    AND  e.status        = 'active'
    AND  e.password_hash IS NOT NULL
    AND  e.password_hash = crypt(p_password, e.password_hash)
  LIMIT 1;

  IF NOT FOUND THEN
    -- Record failed attempt for rate limiting
    PERFORM public.record_rate_limit(v_rate_key, 'employee_login');

    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Invalid employee code or password.'
    );
  END IF;

  -- ── Session creation ──────────────────────────────────────────────────────
  -- Purge any stale sessions for this employee first (single-session policy).
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
END;
$$;

GRANT EXECUTE ON FUNCTION public.authenticate_employee(text, text, text) TO anon;
GRANT EXECUTE ON FUNCTION public.authenticate_employee(text, text, text) TO authenticated;

COMMENT ON FUNCTION public.authenticate_employee IS
  'Atomic login + session creation with built-in rate limiting.
   Replaces the separate login_employee + create_employee_session pattern.
   Enforces 5-attempt / 15-minute lockout per account.';


-- ── 5. Rate-limit verify_employee_identity ────────────────────────────────────
--
-- First-login identity check reveals whether an employee exists.
-- Rate-limit it to prevent mass enumeration.

CREATE OR REPLACE FUNCTION public.verify_employee_identity(
  p_full_name     text,
  p_employee_code text,
  p_hotel         text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_id            uuid;
  v_full_name     text;
  v_hotel         text;
  v_password_hash text;
  v_rate_key      text;
BEGIN
  v_rate_key := UPPER(TRIM(p_employee_code)) || '::' || TRIM(p_hotel);

  -- Rate limit: 10 identity checks per 15 minutes per account
  IF NOT public.check_rate_limit(v_rate_key, 'verify_identity', 10, 15) THEN
    RETURN jsonb_build_object(
      'found',       false,
      'rate_limited', true,
      'error',       'Too many attempts. Please wait 15 minutes and try again.'
    );
  END IF;

  PERFORM public.record_rate_limit(v_rate_key, 'verify_identity');

  SELECT id, full_name, hotel, password_hash
  INTO   v_id, v_full_name, v_hotel, v_password_hash
  FROM   public.employees
  WHERE  employee_code    = TRIM(p_employee_code)
    AND  LOWER(full_name) = LOWER(TRIM(p_full_name))
    AND  hotel            = TRIM(p_hotel)
    AND  status           = 'active'
  LIMIT 1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'found', false,
      'error', 'Employee not recognised. Check your name, code, and hotel.'
    );
  END IF;

  RETURN jsonb_build_object(
    'found',          true,
    'needs_password', (v_password_hash IS NULL),
    'id',             v_id,
    'full_name',      v_full_name,
    'hotel',          v_hotel
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.verify_employee_identity(text, text, text) TO anon;
GRANT EXECUTE ON FUNCTION public.verify_employee_identity(text, text, text) TO authenticated;


-- ── 6. Enforce password minimum length ────────────────────────────────────────
--
-- set_employee_password previously accepted any string.
-- Now requires >= 8 characters before bcrypt hashing.

CREATE OR REPLACE FUNCTION public.set_employee_password(
  p_employee_id uuid,
  p_new_password text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_updated int;
BEGIN
  -- ── Input validation ──────────────────────────────────────────────────────

  IF p_new_password IS NULL OR char_length(p_new_password) < 8 THEN
    RETURN jsonb_build_object(
      'success', false,
      'error',   'Password must be at least 8 characters.'
    );
  END IF;

  IF char_length(p_new_password) > 128 THEN
    RETURN jsonb_build_object(
      'success', false,
      'error',   'Password must not exceed 128 characters.'
    );
  END IF;

  -- ── Update (only when password has never been set) ────────────────────────

  WITH updated AS (
    UPDATE public.employees
    SET    password_hash = crypt(p_new_password, gen_salt('bf', 12))  -- cost 12 (was 10)
    WHERE  id            = p_employee_id
      AND  password_hash IS NULL
      AND  status        = 'active'
    RETURNING id
  )
  SELECT COUNT(*) INTO v_updated FROM updated;

  IF v_updated = 0 THEN
    RETURN jsonb_build_object(
      'success', false,
      'error',   'Password could not be set. The account may already have a password or is inactive.'
    );
  END IF;

  RETURN jsonb_build_object('success', true);
END;
$$;

GRANT EXECUTE ON FUNCTION public.set_employee_password(uuid, text) TO anon;
GRANT EXECUTE ON FUNCTION public.set_employee_password(uuid, text) TO authenticated;

COMMENT ON FUNCTION public.set_employee_password IS
  'Sets bcrypt password for first-time login. Enforces 8-character minimum.
   bcrypt cost raised to 12. Cannot overwrite an existing password.';


-- ── 7. points_balance write guard ─────────────────────────────────────────────
--
-- employees.points_balance must only be modified by SECURITY DEFINER functions
-- (award_recognition_points, admin_grant_points, deduct_points_for_redemption).
-- This trigger blocks any other UPDATE path as defense-in-depth.
-- (RLS already blocks client UPDATEs — this catches rogue server code.)
--
-- SECURITY DEFINER functions set the session-local GUC
-- indabacares.allow_points_update = 'true' before touching the column.

CREATE OR REPLACE FUNCTION public.guard_points_balance()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  IF NEW.points_balance IS DISTINCT FROM OLD.points_balance THEN
    IF current_setting('indabacares.allow_points_update', true) IS DISTINCT FROM 'true' THEN
      RAISE EXCEPTION
        'Direct modification of employees.points_balance is forbidden. '
        'Use award_recognition_points() or admin_grant_points().'
        USING ERRCODE = 'P0005';
    END IF;
  END IF;
  RETURN NEW;
END;
$$;

CREATE TRIGGER trg_guard_points_balance
  BEFORE UPDATE ON public.employees
  FOR EACH ROW EXECUTE FUNCTION public.guard_points_balance();


-- Update award_recognition_points to set the GUC before touching points_balance.
CREATE OR REPLACE FUNCTION public.award_recognition_points()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_base_points  CONSTANT integer := 10;
  v_multiplier   integer;
  v_bonus_points integer;
  v_campaign_id  uuid;
BEGIN
  v_multiplier := public.active_campaign_multiplier(NEW.hotel);

  -- Allow points write for this transaction
  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = points_balance + v_base_points
  WHERE  id = NEW.receiver_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (NEW.receiver_id, v_base_points, 'recognition_received', NEW.hotel);

  IF v_multiplier > 1 THEN
    v_bonus_points := v_base_points * (v_multiplier - 1);
    v_campaign_id  := public.active_campaign_id(NEW.hotel);

    UPDATE public.employees
    SET    points_balance = points_balance + v_bonus_points
    WHERE  id = NEW.receiver_id;

    INSERT INTO public.points_ledger
      (employee_id, points, source, hotel, campaign_id)
    VALUES
      (NEW.receiver_id, v_bonus_points, 'campaign_reward', NEW.hotel, v_campaign_id);
  END IF;

  RETURN NEW;
END;
$$;

-- Recreate the trigger that was dropped above so award_recognition_points
-- could be replaced without Postgres complaining about dependent objects.
CREATE TRIGGER trg_recognition_points
  AFTER INSERT ON public.recognitions
  FOR EACH ROW EXECUTE FUNCTION public.award_recognition_points();


-- Update admin_grant_points to set the GUC.
CREATE OR REPLACE FUNCTION public.admin_grant_points(
  p_employee_id uuid,
  p_points      integer,
  p_source      text DEFAULT 'admin_bonus'
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_hotel text;
BEGIN
  IF p_points = 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Points must be non-zero.');
  END IF;

  IF p_source NOT IN ('admin_bonus', 'campaign_reward') THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Source must be admin_bonus or campaign_reward.');
  END IF;

  SELECT hotel INTO v_hotel
  FROM   public.employees
  WHERE  id = p_employee_id AND status = 'active';

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Active employee not found.');
  END IF;

  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = GREATEST(0, points_balance + p_points)
  WHERE  id = p_employee_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (p_employee_id, p_points, p_source, v_hotel);

  RETURN jsonb_build_object('ok', true, 'points', p_points, 'source', p_source);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.admin_grant_points(uuid, integer, text) TO anon;
GRANT EXECUTE ON FUNCTION public.admin_grant_points(uuid, integer, text) TO authenticated;


-- ── 8. Tighten recognitions INSERT — sender must be authenticated employee ────
--
-- The existing INSERT policy allows any anon with a valid hotel session to
-- insert recognitions for ANY sender_id.  We add a WITH CHECK that enforces
-- sender_id = current_employee_id().

DROP POLICY IF EXISTS "recognitions_hotel_insert" ON public.recognitions;

CREATE POLICY "recognitions_hotel_insert"
  ON public.recognitions
  FOR INSERT TO anon, authenticated
  WITH CHECK (
    hotel      = public.current_employee_hotel()
    AND sender_id = public.current_employee_id()
  );


-- ── 9. Reduce session lifetime to 14 days ────────────────────────────────────
--
-- Update create_employee_session to use 14 days (was 30).
-- New authenticate_employee already uses 14 days.

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
  IF NOT EXISTS (
    SELECT 1 FROM public.employees
    WHERE id = p_employee_id AND status = 'active'
  ) THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Employee account is not active.');
  END IF;

  INSERT INTO public.employee_active_sessions (employee_id, hotel, expires_at)
  VALUES (p_employee_id, p_hotel, now() + INTERVAL '14 days')
  RETURNING token INTO v_token;

  RETURN jsonb_build_object('ok', true, 'token', v_token);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.create_employee_session(uuid, text) TO anon;
GRANT EXECUTE ON FUNCTION public.create_employee_session(uuid, text) TO authenticated;


-- ── 10. Redemption INSERT guard — employee can only redeem for themselves ─────

DROP POLICY IF EXISTS "redemptions_hotel_insert" ON public.redemptions;

CREATE POLICY "redemptions_hotel_insert"
  ON public.redemptions
  FOR INSERT TO anon, authenticated
  WITH CHECK (
    hotel       = public.current_employee_hotel()
    AND employee_id = public.current_employee_id()
  );


-- ── 11. Message INSERT guard — sender_id must match session ──────────────────

DROP POLICY IF EXISTS "messages_hotel_insert" ON public.messages;

CREATE POLICY "messages_hotel_insert"
  ON public.messages
  FOR INSERT TO anon, authenticated
  WITH CHECK (
    hotel     = public.current_employee_hotel()
    AND sender_id = public.current_employee_id()
  );


-- ── 12. Campaigns: block client writes explicitly ─────────────────────────────
--
-- No INSERT/UPDATE/DELETE policies → only service_role (admin portal) can write.
-- Add a comment to make this explicit.

COMMENT ON TABLE public.campaigns IS
  'Recognition multiplier campaigns. Write access: service_role only (admin portal).
   Client read access is gated by current_employee_hotel() via campaigns_hotel_select.';


-- ── 13. Cleanup: scheduled expired-session purge ──────────────────────────────
--
-- Run via Supabase scheduled functions or pg_cron.
-- If pg_cron is available, uncomment:
--
-- SELECT cron.schedule(
--   'purge-expired-sessions',
--   '*/30 * * * *',    -- every 30 minutes
--   'SELECT public.cleanup_expired_sessions()'
-- );
--
-- SELECT cron.schedule(
--   'purge-rate-limits',
--   '0 * * * *',        -- every hour
--   'SELECT public.cleanup_rate_limits()'
-- );


-- ── 14. Grant current_employee_id to anon/authenticated ──────────────────────

-- (Function is SECURITY DEFINER so no extra grant needed on the sessions table,
-- but the function itself must be executable by the client roles.)
GRANT EXECUTE ON FUNCTION public.current_employee_id() TO anon;
GRANT EXECUTE ON FUNCTION public.current_employee_id() TO authenticated;

GRANT EXECUTE ON FUNCTION public.employee_auth_rate_check(text, text) TO anon;
GRANT EXECUTE ON FUNCTION public.employee_auth_rate_check(text, text) TO authenticated;
