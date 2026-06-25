-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 016 — Employee Admin Upload Support
--
-- Builds on migrations 014 (table + RLS) and 015 (password_hash + bcrypt).
--
-- Changes:
--   1. RLS policies for admin INSERT, UPDATE, DELETE
--   2. RPC: admin_insert_employee    — single safe insert
--   3. RPC: admin_bulk_insert_employees — batch JSON upload
--   4. RPC: admin_reset_employee_password — wipe hash → forces re-auth
--   5. RPC: admin_deactivate_employee / admin_reactivate_employee
-- ─────────────────────────────────────────────────────────────────────────────

-- ─── 1. RLS policies — admin write access ────────────────────────────────────
--
-- service_role key (used by Supabase dashboard and server-side admin clients)
-- bypasses RLS automatically — no policy needed for that path.
--
-- These policies cover authenticated users whose JWT app_metadata.role
-- is 'admin' or 'super_admin', allowing them to INSERT/UPDATE/DELETE
-- through the standard anon/authenticated Supabase key.

-- INSERT: admins can add new employees
CREATE POLICY employees_insert_admin
  ON public.employees
  FOR INSERT
  TO authenticated
  WITH CHECK (
    (current_setting('request.jwt.claims', true)::jsonb
      -> 'app_metadata' ->> 'role')
    IN ('admin', 'super_admin')
  );

-- UPDATE: admins can edit any employee row
CREATE POLICY employees_update_admin
  ON public.employees
  FOR UPDATE
  TO authenticated
  USING (
    (current_setting('request.jwt.claims', true)::jsonb
      -> 'app_metadata' ->> 'role')
    IN ('admin', 'super_admin')
  )
  WITH CHECK (
    (current_setting('request.jwt.claims', true)::jsonb
      -> 'app_metadata' ->> 'role')
    IN ('admin', 'super_admin')
  );

-- DELETE: only super_admin can hard-delete (prefer deactivation instead)
CREATE POLICY employees_delete_super_admin
  ON public.employees
  FOR DELETE
  TO authenticated
  USING (
    (current_setting('request.jwt.claims', true)::jsonb
      -> 'app_metadata' ->> 'role')
    = 'super_admin'
  );

-- ─── 2. RPC: admin_insert_employee ───────────────────────────────────────────
--
-- Safe single-employee insert for programmatic admin tools.
-- password_hash is intentionally left NULL — employee sets it on first login.
--
-- Returns:
--   { ok: true,  id: uuid, message: 'Employee created.' }
--   { ok: false, error: '...' }

CREATE OR REPLACE FUNCTION public.admin_insert_employee(
  p_full_name     text,
  p_employee_code text,
  p_hotel         text,
  p_department    text DEFAULT NULL,
  p_position      text DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_id uuid;
BEGIN
  -- Normalise inputs
  p_full_name     := TRIM(p_full_name);
  p_employee_code := UPPER(TRIM(p_employee_code));
  p_hotel         := TRIM(p_hotel);

  -- Guard: required fields
  IF p_full_name = '' THEN
    RETURN jsonb_build_object('ok', false, 'error', 'full_name is required.');
  END IF;
  IF p_employee_code = '' THEN
    RETURN jsonb_build_object('ok', false, 'error', 'employee_code is required.');
  END IF;
  IF p_hotel = '' THEN
    RETURN jsonb_build_object('ok', false, 'error', 'hotel is required.');
  END IF;

  -- Guard: duplicate (employee_code, hotel) pair
  IF EXISTS (
    SELECT 1 FROM public.employees
    WHERE employee_code = p_employee_code
      AND hotel         = p_hotel
  ) THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', format(
        'Employee code %s already exists at %s.',
        p_employee_code, p_hotel
      )
    );
  END IF;

  -- Insert — password_hash intentionally NULL
  INSERT INTO public.employees (
    full_name,
    employee_code,
    hotel,
    department,
    position,
    status,
    password_hash
  )
  VALUES (
    p_full_name,
    p_employee_code,
    p_hotel,
    NULLIF(TRIM(COALESCE(p_department, '')), ''),
    NULLIF(TRIM(COALESCE(p_position,   '')), ''),
    'active',
    NULL          -- employee sets password on first login
  )
  RETURNING id INTO v_id;

  RETURN jsonb_build_object(
    'ok',      true,
    'id',      v_id,
    'message', format('%s (%s) added to %s.', p_full_name, p_employee_code, p_hotel)
  );

EXCEPTION
  -- Catches hotel / status CHECK violations
  WHEN check_violation THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Invalid hotel name. Check the allowed hotel list.'
    );
END;
$func$;

-- Only authenticated admins may call this; anon clients must use the auth RPCs
GRANT EXECUTE ON FUNCTION public.admin_insert_employee(text, text, text, text, text)
  TO authenticated;

COMMENT ON FUNCTION public.admin_insert_employee IS
  'Admin-only: safely inserts one employee. password_hash is left NULL so the
   employee sets their password on first login. Returns {ok, id, message} or
   {ok: false, error}.';

-- ─── 3. RPC: admin_bulk_insert_employees ─────────────────────────────────────
--
-- Accepts a JSON array of employee objects.
-- Skips duplicates silently (returns counts).
--
-- Input JSON shape per element:
--   { "full_name": "...", "employee_code": "...", "hotel": "...",
--     "department": "...",  "position": "..." }
--   department and position are optional.
--
-- Returns:
--   { inserted: N, skipped: N, errors: [ { row, reason }, ... ] }

CREATE OR REPLACE FUNCTION public.admin_bulk_insert_employees(
  p_employees jsonb
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_row        jsonb;
  v_inserted   int  := 0;
  v_skipped    int  := 0;
  v_errors     jsonb := '[]'::jsonb;
  v_code       text;
  v_hotel      text;
  v_name       text;
  v_dept       text;
  v_pos        text;
  v_result     jsonb;
BEGIN
  IF jsonb_typeof(p_employees) <> 'array' THEN
    RETURN jsonb_build_object(
      'inserted', 0,
      'skipped',  0,
      'errors',   jsonb_build_array(
        jsonb_build_object('row', 0, 'reason', 'Input must be a JSON array.')
      )
    );
  END IF;

  FOR v_row IN SELECT * FROM jsonb_array_elements(p_employees)
  LOOP
    v_name  := TRIM(v_row->>'full_name');
    v_code  := UPPER(TRIM(v_row->>'employee_code'));
    v_hotel := TRIM(v_row->>'hotel');
    v_dept  := NULLIF(TRIM(COALESCE(v_row->>'department', '')), '');
    v_pos   := NULLIF(TRIM(COALESCE(v_row->>'position',   '')), '');

    -- Call single-insert RPC for consistent validation
    v_result := public.admin_insert_employee(v_name, v_code, v_hotel, v_dept, v_pos);

    IF (v_result->>'ok')::boolean THEN
      v_inserted := v_inserted + 1;
    ELSE
      -- Duplicate = skip; any other error = record it
      IF (v_result->>'error') LIKE '%already exists%' THEN
        v_skipped := v_skipped + 1;
      ELSE
        v_errors  := v_errors || jsonb_build_array(
          jsonb_build_object(
            'employee_code', v_code,
            'full_name',     v_name,
            'reason',        v_result->>'error'
          )
        );
        v_skipped := v_skipped + 1;
      END IF;
    END IF;
  END LOOP;

  RETURN jsonb_build_object(
    'inserted', v_inserted,
    'skipped',  v_skipped,
    'errors',   v_errors
  );
END;
$func$;

GRANT EXECUTE ON FUNCTION public.admin_bulk_insert_employees(jsonb)
  TO authenticated;

COMMENT ON FUNCTION public.admin_bulk_insert_employees IS
  'Admin-only: bulk insert from a JSON array. Duplicates are skipped silently.
   Returns { inserted, skipped, errors[] }.';

-- ─── 4. RPC: admin_reset_employee_password ───────────────────────────────────
--
-- Sets password_hash back to NULL, forcing the employee to re-authenticate
-- as if it were their first login (e.g. after account handover or data breach).
--
-- Returns:
--   { ok: true }
--   { ok: false, error: '...' }

CREATE OR REPLACE FUNCTION public.admin_reset_employee_password(
  p_employee_id uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_updated int;
BEGIN
  WITH updated AS (
    UPDATE public.employees
    SET    password_hash = NULL
    WHERE  id = p_employee_id
    RETURNING id
  )
  SELECT COUNT(*) INTO v_updated FROM updated;

  IF v_updated = 0 THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Employee not found.'
    );
  END IF;

  RETURN jsonb_build_object('ok', true);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.admin_reset_employee_password(uuid)
  TO authenticated;

COMMENT ON FUNCTION public.admin_reset_employee_password IS
  'Clears password_hash to NULL. The employee must set a new password on next login.';

-- ─── 5. RPC: admin_deactivate_employee / admin_reactivate_employee ────────────

CREATE OR REPLACE FUNCTION public.admin_deactivate_employee(
  p_employee_id uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE v_updated int;
BEGIN
  WITH u AS (
    UPDATE public.employees SET status = 'inactive'
    WHERE id = p_employee_id RETURNING id
  )
  SELECT COUNT(*) INTO v_updated FROM u;

  IF v_updated = 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Employee not found.');
  END IF;

  RETURN jsonb_build_object('ok', true);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.admin_deactivate_employee(uuid) TO authenticated;

CREATE OR REPLACE FUNCTION public.admin_reactivate_employee(
  p_employee_id uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE v_updated int;
BEGIN
  WITH u AS (
    UPDATE public.employees SET status = 'active'
    WHERE id = p_employee_id RETURNING id
  )
  SELECT COUNT(*) INTO v_updated FROM u;

  IF v_updated = 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Employee not found.');
  END IF;

  RETURN jsonb_build_object('ok', true);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.admin_reactivate_employee(uuid) TO authenticated;

-- ─── 6. Helpful view for admin dashboards ────────────────────────────────────
--
-- Exposes all employee columns EXCEPT password_hash.

CREATE OR REPLACE VIEW public.employees_admin_view AS
  SELECT
    id,
    full_name,
    employee_code,
    hotel,
    department,
    position,
    status,
    CASE
      WHEN password_hash IS NULL THEN 'pending_first_login'
      ELSE                            'password_set'
    END AS auth_status,
    created_at
  FROM public.employees;

COMMENT ON VIEW public.employees_admin_view IS
  'Read-only view of the employees table with password_hash excluded.
   auth_status: pending_first_login = NULL hash; password_set = hash present.';
