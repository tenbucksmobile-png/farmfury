-- 056_session_ttl_fix.sql
--
-- Enforces a 30-day session lifetime.
-- Updates validate_session() to reject expired sessions.
-- Updates authenticate_employee and first_time_authenticate to set expires_at
-- on every new session row.
--
-- Background: migration 032 added the expires_at column but the existing
-- validate_session() function did not check it. This migration closes the gap.

-- ─── 1. Backfill expires_at on existing open sessions ────────────────────────
--
-- Sessions that predate this migration get a 30-day window from now.
-- This prevents mass-logout of existing users on deploy.

UPDATE employee_active_sessions
SET expires_at = now() + INTERVAL '30 days'
WHERE expires_at IS NULL;

-- ─── 2. Replace validate_session to enforce expiry ───────────────────────────
--
-- Returns { ok: true, employee_id, full_name, employee_code, hotel }
-- or      { ok: false, error: '...' }

CREATE OR REPLACE FUNCTION validate_session(p_session_token uuid)
RETURNS jsonb
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_row record;
BEGIN
  SELECT
    s.employee_id,
    s.expires_at,
    e.full_name,
    e.employee_code,
    e.hotel,
    e.status
  INTO v_row
  FROM employee_active_sessions s
  JOIN employees e ON e.id = s.employee_id
  WHERE s.session_token = p_session_token
  LIMIT 1;

  -- Not found
  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Session not found');
  END IF;

  -- Expired
  IF v_row.expires_at IS NOT NULL AND v_row.expires_at < now() THEN
    -- Remove expired session to keep the table clean
    DELETE FROM employee_active_sessions WHERE session_token = p_session_token;
    RETURN jsonb_build_object('ok', false, 'error', 'Session expired');
  END IF;

  -- Inactive employee
  IF v_row.status <> 'active' THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Account is inactive');
  END IF;

  RETURN jsonb_build_object(
    'ok',            true,
    'employee_id',   v_row.employee_id,
    'full_name',     v_row.full_name,
    'employee_code', v_row.employee_code,
    'hotel',         v_row.hotel
  );
END;
$$;

-- ─── 3. Ensure new sessions always set expires_at ────────────────────────────
--
-- Trigger on employee_active_sessions INSERT to set expires_at = 30 days
-- if the caller does not supply it. Belt-and-suspenders for all auth RPCs.

CREATE OR REPLACE FUNCTION trg_set_session_expiry()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  IF NEW.expires_at IS NULL THEN
    NEW.expires_at := now() + INTERVAL '30 days';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS set_session_expiry ON employee_active_sessions;

CREATE TRIGGER set_session_expiry
  BEFORE INSERT ON employee_active_sessions
  FOR EACH ROW
  EXECUTE FUNCTION trg_set_session_expiry();
