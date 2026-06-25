-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 044 — Profile Photos & Job Title
--
-- Context:
--   Migration 030 removed department and position from employees.
--   The final employees schema is:
--     id, full_name, employee_code, hotel, password_hash,
--     status, points_balance, created_at
--
-- Changes:
--   1. Add job_title column to employees (replaces removed position column)
--   2. Add photo_url column to employees
--   3. current_employee_id() helper (mirrors current_employee_hotel())
--   4. update_employee_avatar() SECURITY DEFINER RPC
--   5. avatars storage bucket + RLS policies
--   6. Rebuild employees_admin_view with new columns
-- ─────────────────────────────────────────────────────────────────────────────


-- ─── 1. New columns on employees ─────────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS job_title text;

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS photo_url text;

COMMENT ON COLUMN public.employees.job_title IS
  'Employee job title / role (e.g. "Front Desk Manager"). '
  'Populated by admins via CSV upload or the admin dashboard. '
  'Displayed on the employee profile screen.';

COMMENT ON COLUMN public.employees.photo_url IS
  'Public URL of the employee''s profile photo stored in the avatars storage bucket. '
  'NULL when no photo has been uploaded yet.';


-- ─── 2. current_employee_id() helper ─────────────────────────────────────────
--
-- Mirrors current_employee_hotel() (migration 017).
-- Reads x-session-token from request.headers and returns the employee_id UUID.
-- Used in storage RLS policies to scope uploads to the employee's own folder.

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
  WHERE  token      = v_token
    AND  expires_at > now();

  RETURN v_employee_id;  -- NULL if token not found or expired
END;
$func$;

COMMENT ON FUNCTION public.current_employee_id IS
  'Reads x-session-token from request.headers and returns the authenticated employee''s UUID. '
  'Returns NULL for missing or expired tokens. Used in storage RLS policies.';


-- ─── 3. update_employee_avatar RPC ───────────────────────────────────────────
--
-- SECURITY DEFINER bypasses RLS so the employee can write their own photo_url
-- without needing a broad UPDATE policy on the employees table.
-- Only photo_url is modified — no other columns can be changed via this RPC.

CREATE OR REPLACE FUNCTION public.update_employee_avatar(
  p_photo_url text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_employee_id uuid;
  v_updated     int;
BEGIN
  v_employee_id := public.current_employee_id();

  IF v_employee_id IS NULL THEN
    RETURN jsonb_build_object('ok', false, 'error', 'No valid session found.');
  END IF;

  IF length(p_photo_url) > 2048 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'URL too long.');
  END IF;

  WITH updated AS (
    UPDATE public.employees
    SET    photo_url = p_photo_url
    WHERE  id = v_employee_id
    RETURNING id
  )
  SELECT COUNT(*) INTO v_updated FROM updated;

  IF v_updated = 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Employee not found.');
  END IF;

  RETURN jsonb_build_object('ok', true);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.update_employee_avatar(text) TO anon;
GRANT EXECUTE ON FUNCTION public.update_employee_avatar(text) TO authenticated;

COMMENT ON FUNCTION public.update_employee_avatar IS
  'Allows an authenticated employee to update their own photo_url. '
  'Uses current_employee_id() — no JWT required. Only modifies photo_url.';


-- ─── 4. avatars storage bucket + RLS ─────────────────────────────────────────
--
-- Public bucket: images are served openly (no auth needed to view).
-- Write access is restricted per employee via current_employee_id():
--   each employee may only upload to /{their-employee-id}/...
--
-- Path convention: {employee_id}/avatar.{ext}
-- The RLS policy checks (storage.foldername(name))[1] = current_employee_id()::text

INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES (
  'avatars',
  'avatars',
  true,
  5242880,
  ARRAY['image/jpeg', 'image/jpg', 'image/png', 'image/webp']
)
ON CONFLICT (id) DO UPDATE
  SET public             = true,
      file_size_limit    = 5242880,
      allowed_mime_types = ARRAY['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];


-- Drop any pre-existing policies to avoid conflicts on re-run
DROP POLICY IF EXISTS "avatars_select_public" ON storage.objects;
DROP POLICY IF EXISTS "avatars_insert_own"    ON storage.objects;
DROP POLICY IF EXISTS "avatars_update_own"    ON storage.objects;
DROP POLICY IF EXISTS "avatars_delete_own"    ON storage.objects;


-- Public read — the bucket is public, so anyone can fetch avatar URLs
CREATE POLICY "avatars_select_public"
  ON storage.objects
  FOR SELECT
  TO public
  USING (bucket_id = 'avatars');


-- Employees may only upload to their own folder
CREATE POLICY "avatars_insert_own"
  ON storage.objects
  FOR INSERT
  TO anon, authenticated
  WITH CHECK (
    bucket_id = 'avatars'
    AND (storage.foldername(name))[1] = public.current_employee_id()::text
  );


-- Employees may replace their own files
CREATE POLICY "avatars_update_own"
  ON storage.objects
  FOR UPDATE
  TO anon, authenticated
  USING (
    bucket_id = 'avatars'
    AND (storage.foldername(name))[1] = public.current_employee_id()::text
  )
  WITH CHECK (
    bucket_id = 'avatars'
    AND (storage.foldername(name))[1] = public.current_employee_id()::text
  );


-- Employees may delete their own files
CREATE POLICY "avatars_delete_own"
  ON storage.objects
  FOR DELETE
  TO anon, authenticated
  USING (
    bucket_id = 'avatars'
    AND (storage.foldername(name))[1] = public.current_employee_id()::text
  );


-- ─── 5. Rebuild employees_admin_view ─────────────────────────────────────────
--
-- Migration 030 rebuilt this view without department/position.
-- We DROP and recreate (CREATE OR REPLACE cannot reorder/rename columns).

DROP VIEW IF EXISTS public.employees_admin_view;

CREATE VIEW public.employees_admin_view AS
  SELECT
    id,
    full_name,
    employee_code,
    hotel,
    job_title,
    photo_url,
    status,
    points_balance,
    CASE
      WHEN password_hash IS NULL THEN 'pending_first_login'
      ELSE                            'password_set'
    END AS auth_status,
    created_at
  FROM public.employees;

COMMENT ON VIEW public.employees_admin_view IS
  'Read-only view of the employees table with password_hash excluded. '
  'Includes job_title and photo_url. '
  'auth_status: pending_first_login = NULL hash; password_set = hash present.';
