-- =============================================================================
-- Migration 080 — Sponsor Ad Campaigns
--
-- Extends the campaigns table to support two distinct use-cases:
--   'recognition' — existing multiplier campaigns (boost employee points)
--   'sponsor'     — paid sponsor advertising (banner shown in mobile app)
--   'both'        — combines a multiplier boost WITH a sponsor banner
--
-- Sponsor workflow:
--   1. Admin creates a campaign with type='sponsor' or 'both'.
--   2. Sponsor's banner_url is uploaded to the campaign-media Storage bucket.
--   3. Mobile app displays the banner during [start_date, end_date].
--   4. Employees can tap the banner to open banner_link_url (optional).
--   5. voucher_description describes the reward/voucher the sponsor provides.
-- =============================================================================


-- ── 1. Extend campaigns table ─────────────────────────────────────────────────

ALTER TABLE public.campaigns
  ADD COLUMN IF NOT EXISTS type text NOT NULL DEFAULT 'recognition'
    CHECK (type IN ('recognition', 'sponsor', 'both')),
  ADD COLUMN IF NOT EXISTS sponsor_name        text,
  ADD COLUMN IF NOT EXISTS banner_url          text,
  ADD COLUMN IF NOT EXISTS banner_link_url     text,
  ADD COLUMN IF NOT EXISTS voucher_description text;

COMMENT ON COLUMN public.campaigns.type IS
  'recognition = points multiplier only; sponsor = ad banner only; both = multiplier + banner.';
COMMENT ON COLUMN public.campaigns.sponsor_name IS
  'Display name of the sponsor (e.g. "Coca-Cola"). Shown below the banner.';
COMMENT ON COLUMN public.campaigns.banner_url IS
  'Public URL of the sponsor banner image in the campaign-media bucket.';
COMMENT ON COLUMN public.campaigns.banner_link_url IS
  'Optional URL opened when the employee taps the banner.';
COMMENT ON COLUMN public.campaigns.voucher_description IS
  'Description of the voucher/reward the sponsor is providing.';


-- ── 2. Update get_active_campaigns() to include sponsor fields ────────────────

DROP FUNCTION IF EXISTS public.get_active_campaigns(text);

CREATE OR REPLACE FUNCTION public.get_active_campaigns(
  p_hotel text DEFAULT NULL
)
RETURNS TABLE (
  id                   uuid,
  title                text,
  description          text,
  type                 text,
  points_multiplier    integer,
  hotel                text,
  start_date           date,
  end_date             date,
  days_remaining       integer,
  sponsor_name         text,
  banner_url           text,
  banner_link_url      text,
  voucher_description  text
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT
    id,
    title,
    description,
    type,
    points_multiplier,
    hotel,
    start_date,
    end_date,
    (end_date - CURRENT_DATE)::integer AS days_remaining,
    sponsor_name,
    banner_url,
    banner_link_url,
    voucher_description
  FROM public.campaigns
  WHERE start_date <= CURRENT_DATE
    AND end_date   >= CURRENT_DATE
    AND (p_hotel IS NULL OR hotel = p_hotel)
  ORDER BY
    CASE type WHEN 'sponsor' THEN 0 WHEN 'both' THEN 1 ELSE 2 END,
    points_multiplier DESC,
    end_date ASC;
$$;

GRANT EXECUTE ON FUNCTION public.get_active_campaigns(text) TO anon;
GRANT EXECUTE ON FUNCTION public.get_active_campaigns(text) TO authenticated;


-- ── 3. get_all_campaigns RPC — returns active + upcoming for the mobile list ──

CREATE OR REPLACE FUNCTION public.get_campaigns_for_hotel(
  p_hotel text
)
RETURNS TABLE (
  id                   uuid,
  title                text,
  description          text,
  type                 text,
  points_multiplier    integer,
  hotel                text,
  start_date           date,
  end_date             date,
  days_remaining       integer,
  is_active            boolean,
  sponsor_name         text,
  banner_url           text,
  banner_link_url      text,
  voucher_description  text
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT
    id,
    title,
    description,
    type,
    points_multiplier,
    hotel,
    start_date,
    end_date,
    GREATEST(0, (end_date - CURRENT_DATE)::integer) AS days_remaining,
    (start_date <= CURRENT_DATE AND end_date >= CURRENT_DATE) AS is_active,
    sponsor_name,
    banner_url,
    banner_link_url,
    voucher_description
  FROM public.campaigns
  WHERE hotel      = p_hotel
    AND end_date  >= CURRENT_DATE     -- exclude ended
  ORDER BY
    is_active DESC,
    CASE type WHEN 'sponsor' THEN 0 WHEN 'both' THEN 1 ELSE 2 END,
    start_date ASC;
$$;

GRANT EXECUTE ON FUNCTION public.get_campaigns_for_hotel(text) TO anon;
GRANT EXECUTE ON FUNCTION public.get_campaigns_for_hotel(text) TO authenticated;

COMMENT ON FUNCTION public.get_campaigns_for_hotel IS
  'Returns active and upcoming campaigns for a hotel. Excludes ended campaigns. '
  'Ordered: active first, then sponsor types before recognition-only.';


-- ── 4. campaign-media Storage bucket ─────────────────────────────────────────

INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES (
  'campaign-media',
  'campaign-media',
  true,
  10485760,   -- 10 MB
  ARRAY['image/jpeg', 'image/jpg', 'image/png', 'image/webp']
)
ON CONFLICT (id) DO UPDATE
  SET public             = true,
      file_size_limit    = 10485760,
      allowed_mime_types = ARRAY['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];

DROP POLICY IF EXISTS "campaign_media_select_public" ON storage.objects;

CREATE POLICY "campaign_media_select_public"
  ON storage.objects
  FOR SELECT TO public
  USING (bucket_id = 'campaign-media');
