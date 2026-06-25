-- =============================================================================
-- Migration 024 — Recognition Campaigns
--
-- Campaigns let admins boost recognition point awards for a hotel during a
-- defined window (e.g. "Customer Service Week — 2× points").
--
-- Design decisions
-- ────────────────
-- • Multiplier applies only during [start_date, end_date] inclusive (UTC date).
-- • When multiple campaigns overlap for the same hotel, the highest multiplier
--   wins (conservative: never penalise employees for overlapping campaigns).
-- • Base 10 pts are still recorded as  recognition_received.
-- • Bonus pts  ( 10 × (multiplier−1) ) are recorded as  campaign_reward  with
--   a reference to the campaign row — keeping the ledger fully auditable.
-- • If multiplier = 1 (no boost) no extra ledger row is written.
-- • award_recognition_points() is replaced in-place via CREATE OR REPLACE.
-- =============================================================================


-- ── 1. campaigns table ────────────────────────────────────────────────────────

CREATE TABLE public.campaigns (
  id                uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  title             text        NOT NULL CHECK (char_length(trim(title)) >= 3),
  description       text,
  points_multiplier integer     NOT NULL DEFAULT 2
                    CHECK (points_multiplier >= 1),
  hotel             text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  start_date        date        NOT NULL,
  end_date          date        NOT NULL,
  created_at        timestamptz NOT NULL DEFAULT now(),
  updated_at        timestamptz NOT NULL DEFAULT now(),

  CONSTRAINT chk_campaign_dates CHECK (end_date >= start_date)
);

CREATE INDEX idx_campaigns_hotel_dates
  ON public.campaigns (hotel, start_date, end_date);

COMMENT ON TABLE public.campaigns IS
  'Recognition multiplier campaigns scoped to a hotel and date window. '
  'Recognitions sent during the window earn base × points_multiplier points.';


-- ── 2. Add nullable campaign reference to points_ledger ───────────────────────
--
-- Allows tracing exactly which campaign generated a campaign_reward row.

ALTER TABLE public.points_ledger
  ADD COLUMN IF NOT EXISTS campaign_id uuid
    REFERENCES public.campaigns(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_ledger_campaign
  ON public.points_ledger (campaign_id)
  WHERE campaign_id IS NOT NULL;


-- ── 3. RLS — campaigns ────────────────────────────────────────────────────────

ALTER TABLE public.campaigns ENABLE ROW LEVEL SECURITY;

-- Hotel-matched employees can read campaigns (for in-app banner)
CREATE POLICY "campaigns_hotel_select"
  ON public.campaigns
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

-- Only service_role (admin portal) may write campaigns (no client writes)


-- ── 4. Helper: active_campaign_multiplier ────────────────────────────────────
--
-- Returns the highest active multiplier for a hotel on a given date,
-- or 1 if no campaign is running.

CREATE OR REPLACE FUNCTION public.active_campaign_multiplier(
  p_hotel text,
  p_date  date DEFAULT CURRENT_DATE
)
RETURNS integer
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT COALESCE(
    (
      SELECT points_multiplier
      FROM   public.campaigns
      WHERE  hotel      = p_hotel
        AND  start_date <= p_date
        AND  end_date   >= p_date
      ORDER  BY points_multiplier DESC
      LIMIT  1
    ),
    1
  );
$$;

COMMENT ON FUNCTION public.active_campaign_multiplier IS
  'Returns the highest active campaign multiplier for a hotel on the given date. '
  'Returns 1 when no campaign is running (neutral multiplier).';


-- ── 5. Helper: active_campaign_id ─────────────────────────────────────────────
--
-- Returns the id of the campaign driving the highest multiplier,
-- NULL if none is active.

CREATE OR REPLACE FUNCTION public.active_campaign_id(
  p_hotel text,
  p_date  date DEFAULT CURRENT_DATE
)
RETURNS uuid
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT id
  FROM   public.campaigns
  WHERE  hotel      = p_hotel
    AND  start_date <= p_date
    AND  end_date   >= p_date
  ORDER  BY points_multiplier DESC
  LIMIT  1;
$$;


-- ── 6. Updated trigger: award_recognition_points ──────────────────────────────
--
-- Replaces migration 021 version.
-- Now applies campaign multiplier and writes a separate campaign_reward row
-- for the bonus portion so the ledger remains fully auditable.

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
  -- Determine campaign multiplier (1 = no campaign, no change)
  v_multiplier := public.active_campaign_multiplier(NEW.hotel);

  -- ── Base award ────────────────────────────────────────────────────────────

  -- Update fast running total used by redemption balance checks
  UPDATE public.employees
  SET    points_balance = points_balance + v_base_points
  WHERE  id = NEW.receiver_id;

  -- Immutable base ledger row
  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (NEW.receiver_id, v_base_points, 'recognition_received', NEW.hotel);

  -- ── Campaign bonus ────────────────────────────────────────────────────────

  IF v_multiplier > 1 THEN
    v_bonus_points := v_base_points * (v_multiplier - 1);
    v_campaign_id  := public.active_campaign_id(NEW.hotel);

    -- Update running total with bonus
    UPDATE public.employees
    SET    points_balance = points_balance + v_bonus_points
    WHERE  id = NEW.receiver_id;

    -- Separate campaign_reward ledger row — links back to the campaign
    INSERT INTO public.points_ledger
      (employee_id, points, source, hotel, campaign_id)
    VALUES
      (NEW.receiver_id, v_bonus_points, 'campaign_reward', NEW.hotel, v_campaign_id);
  END IF;

  RETURN NEW;
END;
$$;

COMMENT ON FUNCTION public.award_recognition_points IS
  'Awards base 10 pts per recognition received (recognition_received). '
  'If an active campaign exists for the hotel, also awards bonus pts '
  '(10 × (multiplier−1)) as campaign_reward, linked to the campaign row.';


-- ── 7. updated_at trigger for campaigns ──────────────────────────────────────

CREATE OR REPLACE FUNCTION public.set_updated_at()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  NEW.updated_at := now();
  RETURN NEW;
END;
$$;

CREATE TRIGGER campaigns_updated_at
  BEFORE UPDATE ON public.campaigns
  FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();


-- ── 8. get_active_campaigns RPC ───────────────────────────────────────────────
--
-- Called by the mobile app to show an in-app campaign banner.

CREATE OR REPLACE FUNCTION public.get_active_campaigns(
  p_hotel text DEFAULT NULL
)
RETURNS TABLE (
  id                uuid,
  title             text,
  description       text,
  points_multiplier integer,
  hotel             text,
  start_date        date,
  end_date          date,
  days_remaining    integer
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
    points_multiplier,
    hotel,
    start_date,
    end_date,
    (end_date - CURRENT_DATE)::integer AS days_remaining
  FROM public.campaigns
  WHERE start_date <= CURRENT_DATE
    AND end_date   >= CURRENT_DATE
    AND (p_hotel IS NULL OR hotel = p_hotel)
  ORDER BY points_multiplier DESC, end_date ASC;
$$;

GRANT EXECUTE ON FUNCTION public.get_active_campaigns(text) TO anon;
GRANT EXECUTE ON FUNCTION public.get_active_campaigns(text) TO authenticated;

COMMENT ON FUNCTION public.get_active_campaigns IS
  'Returns currently active campaigns with days_remaining. '
  'Pass a hotel to scope. Used by the mobile app for campaign banners.';
