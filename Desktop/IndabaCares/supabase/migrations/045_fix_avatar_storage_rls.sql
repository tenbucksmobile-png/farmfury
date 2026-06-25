-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 045 — Fix avatars storage RLS
--
-- Problem:
--   current_employee_id() reads x-session-token via current_setting('request.headers')
--   which is a PostgREST-only feature.  Supabase Storage is a separate HTTP service
--   and never sets request.headers, so current_employee_id() always returns NULL in
--   the storage RLS context — blocking every upload attempt.
--
-- Fix:
--   Replace the upload/update/delete policies with open anon policies.
--   Security is enforced at the DB level by update_employee_avatar() (PostgREST RPC)
--   which validates the x-session-token before writing photo_url to employees.
--
--   Threat model: an attacker can upload arbitrary images to the avatars bucket
--   but CANNOT associate them with another employee's profile row without a valid
--   session token.  Bucket file-size (5 MB) and MIME-type limits remain in place.
-- ─────────────────────────────────────────────────────────────────────────────

-- Drop old policies (both original names and open names) before recreating
DROP POLICY IF EXISTS "avatars_insert_own"  ON storage.objects;
DROP POLICY IF EXISTS "avatars_update_own"  ON storage.objects;
DROP POLICY IF EXISTS "avatars_delete_own"  ON storage.objects;
DROP POLICY IF EXISTS "avatars_insert_open" ON storage.objects;
DROP POLICY IF EXISTS "avatars_update_open" ON storage.objects;
DROP POLICY IF EXISTS "avatars_delete_open" ON storage.objects;

-- Allow any client to upload to the avatars bucket
-- (auth is enforced downstream by update_employee_avatar RPC)
CREATE POLICY "avatars_insert_open"
  ON storage.objects
  FOR INSERT
  TO anon, authenticated
  WITH CHECK (bucket_id = 'avatars');

CREATE POLICY "avatars_update_open"
  ON storage.objects
  FOR UPDATE
  TO anon, authenticated
  USING     (bucket_id = 'avatars')
  WITH CHECK (bucket_id = 'avatars');

CREATE POLICY "avatars_delete_open"
  ON storage.objects
  FOR DELETE
  TO anon, authenticated
  USING (bucket_id = 'avatars');
