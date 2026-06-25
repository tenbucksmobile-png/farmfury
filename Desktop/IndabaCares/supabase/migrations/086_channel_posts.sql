-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 086 — Channel Posts
--
-- channel_posts stores photo/video/text posts for each hotel's public channel.
-- All authenticated employees can SELECT across all hotels (cross-hotel read).
-- INSERT/UPDATE/DELETE is restricted to service_role (admin portal only).
-- Storage bucket channel-media is public for reads; upload/delete requires
-- an authenticated Supabase Auth session (hotel admins only).
-- ─────────────────────────────────────────────────────────────────────────────

-- ─── Table ───────────────────────────────────────────────────────────────────

CREATE TABLE public.channel_posts (
  id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  hotel         text        NOT NULL,
  post_type     text        NOT NULL CHECK (post_type IN ('photo', 'video', 'text')),
  media_url     text,
  media_path    text,
  thumbnail_url text,
  caption       text,
  created_by    uuid        NOT NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  is_published  boolean     NOT NULL DEFAULT true,
  -- media_url is required for photo and video posts
  CONSTRAINT channel_posts_media_required CHECK (
    post_type = 'text' OR media_url IS NOT NULL
  )
);

CREATE INDEX idx_channel_posts_hotel_created
  ON public.channel_posts (hotel, created_at DESC);

-- ─── RLS ─────────────────────────────────────────────────────────────────────

ALTER TABLE public.channel_posts ENABLE ROW LEVEL SECURITY;

-- All authenticated employees can read published posts from any hotel
CREATE POLICY "employees read published channel posts"
  ON public.channel_posts
  FOR SELECT
  USING (
    is_published = true
    AND current_employee_id() IS NOT NULL
  );

-- ─── Storage bucket ──────────────────────────────────────────────────────────

INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES (
  'channel-media',
  'channel-media',
  true,
  104857600,
  ARRAY[
    'image/jpeg', 'image/png', 'image/webp', 'image/gif',
    'video/mp4', 'video/quicktime', 'video/webm'
  ]
)
ON CONFLICT (id) DO NOTHING;

-- Hotel admins (authenticated Supabase Auth users) can upload
CREATE POLICY "admin upload channel-media"
  ON storage.objects
  FOR INSERT
  TO authenticated
  WITH CHECK (bucket_id = 'channel-media');

-- Hotel admins can delete their uploads
CREATE POLICY "admin delete channel-media"
  ON storage.objects
  FOR DELETE
  TO authenticated
  USING (bucket_id = 'channel-media');
