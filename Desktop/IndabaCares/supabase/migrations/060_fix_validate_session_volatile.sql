-- 060_fix_validate_session_volatile.sql
--
-- CRITICAL BUG FIX: validate_session was declared STABLE but performs a DELETE.
--
-- PostgreSQL's STABLE annotation tells the query planner the function produces
-- no side effects and its result may be cached within a single query execution.
-- A STABLE function that writes (DELETE) violates this contract — the planner
-- may skip the DELETE entirely, leaving expired sessions in the table forever,
-- and may cache the result of the first call within a transaction, preventing
-- real-time session revocation from taking effect.
--
-- Fix: Change to VOLATILE (the correct annotation for any function that writes).

CREATE OR REPLACE FUNCTION validate_session(p_session_token uuid)
RETURNS jsonb
LANGUAGE plpgsql
VOLATILE                    -- ← was incorrectly STABLE; a DELETE requires VOLATILE
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

  -- Expired — delete the row to keep the table clean
  IF v_row.expires_at IS NOT NULL AND v_row.expires_at < now() THEN
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
