-- 062_push_token_stale_cleanup.sql
--
-- Fixes multi-device push token accumulation.
--
-- Problem: upsert_push_token() only prevents duplicate rows for the same
-- (employee_id, token) pair. Different devices produce different tokens, so
-- an employee who logs in on 3 devices accumulates 3 tokens permanently.
-- Notifications are then delivered to all devices — including ones no longer
-- in use — with no cleanup path.
--
-- Solution:
--   1. Add `last_seen_at` column to push_tokens — updated on every registration.
--   2. Replace upsert_push_token() with register_push_token():
--      a. Upsert the new token (existing behaviour).
--      b. Delete tokens for this employee that have not been seen in 90 days.
--   3. Keep upsert_push_token() as a shim (calls register_push_token) so any
--      existing callers continue to work without changes.

-- ─── 1. Add last_seen_at column ───────────────────────────────────────────────

ALTER TABLE push_tokens
  ADD COLUMN IF NOT EXISTS last_seen_at timestamptz DEFAULT now();

-- Backfill: treat existing tokens as last seen now (they are current installs).
UPDATE push_tokens SET last_seen_at = now() WHERE last_seen_at IS NULL;

-- ─── 2. New function: register_push_token ────────────────────────────────────

CREATE OR REPLACE FUNCTION register_push_token(
  p_employee_id uuid,
  p_hotel       text,
  p_token       text,
  p_platform    text
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  -- Validate inputs
  IF p_platform NOT IN ('ios', 'android', 'web') THEN
    RAISE EXCEPTION 'Invalid platform: %', p_platform USING ERRCODE = '22023';
  END IF;

  IF p_token IS NULL OR trim(p_token) = '' THEN
    RAISE EXCEPTION 'Token must not be empty' USING ERRCODE = '22023';
  END IF;

  -- Ensure employee is active and belongs to the declared hotel
  IF NOT EXISTS (
    SELECT 1 FROM employees
    WHERE id     = p_employee_id
      AND hotel  = p_hotel
      AND status = 'active'
  ) THEN
    RAISE EXCEPTION 'Employee not found or inactive' USING ERRCODE = '42501';
  END IF;

  -- Upsert the token, refreshing last_seen_at on conflict
  INSERT INTO push_tokens (employee_id, hotel, token, platform, updated_at, last_seen_at)
  VALUES (p_employee_id, p_hotel, p_token, p_platform, now(), now())
  ON CONFLICT (employee_id, token) DO UPDATE
    SET platform     = EXCLUDED.platform,
        updated_at   = now(),
        last_seen_at = now();

  -- Prune stale tokens for this employee (not seen in 90 days).
  -- This keeps at most the tokens registered in the last 90 days, removing
  -- tokens from devices the employee no longer uses.
  DELETE FROM push_tokens
  WHERE employee_id = p_employee_id
    AND token      <> p_token
    AND last_seen_at < now() - INTERVAL '90 days';
END;
$$;

-- ─── 3. Replace upsert_push_token to call register_push_token ────────────────
--
-- Preserves the existing API surface so NotificationProvider and
-- notification-permission.tsx don't need immediate changes. Both RPCs are
-- available; new callers should prefer register_push_token directly.

CREATE OR REPLACE FUNCTION upsert_push_token(
  p_employee_id uuid,
  p_hotel       text,
  p_token       text,
  p_platform    text
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  PERFORM register_push_token(p_employee_id, p_hotel, p_token, p_platform);
END;
$$;
