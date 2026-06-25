-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 051 — Media Storage: rewards catalogue + initiatives
--
-- Changes:
--   1. rewards:          add category ('retail' | 'hotel') and terms columns
--   2. initiatives:      new table for admin-managed initiative tab content
--   3. Storage bucket:   rewards (public read, admin write)
--   4. Storage bucket:   initiative-media (public read, admin write, 50 MB limit)
-- ─────────────────────────────────────────────────────────────────────────────


-- ─── 1. rewards: add category + terms ────────────────────────────────────────

ALTER TABLE public.rewards
  ADD COLUMN IF NOT EXISTS category text NOT NULL DEFAULT 'retail'
    CHECK (category IN ('retail', 'hotel')),
  ADD COLUMN IF NOT EXISTS terms text;

COMMENT ON COLUMN public.rewards.category IS
  'retail = external vouchers (KFC, Steers, etc.); '
  'hotel  = in-house experience rewards (breakfast, boma dinner, etc.).';

COMMENT ON COLUMN public.rewards.terms IS
  'Terms and conditions text shown on the back of the reward card in the mobile app.';


-- ─── 2. initiatives table ─────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.initiatives (
  id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  hotel       text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  tab         text        NOT NULL,
  -- ^ slug matching the UI tab: 'billy-says' | 'feed-the-kids' | 'mandela-day'
  mascot_url  text,
  -- ^ hero / mascot image shown at the top of the tab (e.g. Billy the peacock)
  image_urls  text[]      NOT NULL DEFAULT '{}',
  -- ^ ordered array of gallery photo URLs from the initiative-media bucket
  video_url   text,
  -- ^ optional video uploaded by an admin; rendered below the mascot
  sort_order  integer     NOT NULL DEFAULT 0,
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_initiatives_hotel_tab
  ON public.initiatives (hotel, tab, sort_order);

ALTER TABLE public.initiatives ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "initiatives_hotel_select" ON public.initiatives;

CREATE POLICY "initiatives_hotel_select"
  ON public.initiatives
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

COMMENT ON TABLE public.initiatives IS
  'Admin-managed content for each initiative tab (Billy Says, FeedtheKids, MandelaDay). '
  'Images and videos live in the initiative-media Storage bucket. '
  'Each row represents one content block within a tab, ordered by sort_order.';


-- ─── 3. rewards Storage bucket ───────────────────────────────────────────────

INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES (
  'rewards',
  'rewards',
  true,
  10485760,   -- 10 MB per image
  ARRAY['image/jpeg', 'image/jpg', 'image/png', 'image/webp']
)
ON CONFLICT (id) DO UPDATE
  SET public             = true,
      file_size_limit    = 10485760,
      allowed_mime_types = ARRAY['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];

DROP POLICY IF EXISTS "rewards_images_select_public" ON storage.objects;

CREATE POLICY "rewards_images_select_public"
  ON storage.objects
  FOR SELECT TO public
  USING (bucket_id = 'rewards');


-- ─── 4. initiative-media Storage bucket ──────────────────────────────────────

INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES (
  'initiative-media',
  'initiative-media',
  true,
  52428800,   -- 50 MB (accommodates short video clips)
  ARRAY[
    'image/jpeg', 'image/jpg', 'image/png', 'image/webp',
    'video/mp4', 'video/quicktime', 'video/x-m4v'
  ]
)
ON CONFLICT (id) DO UPDATE
  SET public             = true,
      file_size_limit    = 52428800,
      allowed_mime_types = ARRAY[
        'image/jpeg', 'image/jpg', 'image/png', 'image/webp',
        'video/mp4', 'video/quicktime', 'video/x-m4v'
      ];

DROP POLICY IF EXISTS "initiative_media_select_public" ON storage.objects;

CREATE POLICY "initiative_media_select_public"
  ON storage.objects
  FOR SELECT TO public
  USING (bucket_id = 'initiative-media');
