-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 021 — Points Ledger + Gamified Leaderboard
--
-- points_ledger is the immutable append-only source of truth for every point
-- event.  It feeds the live leaderboard via get_leaderboard().
--
-- Sources
--   recognition_received  — 10 pts per recognition received (trigger)
--   admin_bonus           — one-off bonus granted by admin
--   campaign_reward       — points awarded by a campaign / challenge
--
-- employees.points_balance remains the fast running-total used by redemptions.
-- The updated award_recognition_points() trigger now writes to both.
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. points_ledger ─────────────────────────────────────────────────────────

CREATE TABLE public.points_ledger (
  id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id uuid        NOT NULL REFERENCES public.employees(id) ON DELETE CASCADE,
  points      integer     NOT NULL,
  source      text        NOT NULL CHECK (source IN (
                            'recognition_received',
                            'admin_bonus',
                            'campaign_reward'
                          )),
  hotel       text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  created_at  timestamptz NOT NULL DEFAULT now(),

  CONSTRAINT chk_ledger_points_nonzero CHECK (points <> 0)
);

CREATE INDEX idx_ledger_employee ON public.points_ledger (employee_id, created_at DESC);
CREATE INDEX idx_ledger_hotel    ON public.points_ledger (hotel,       created_at DESC);

COMMENT ON TABLE public.points_ledger IS
  'Immutable append-only log of all point events.  Negative values represent '
  'deductions (e.g. future point-spend features).  Source of truth for the leaderboard.';


-- ── 2. RLS ────────────────────────────────────────────────────────────────────

ALTER TABLE public.points_ledger ENABLE ROW LEVEL SECURITY;

CREATE POLICY "ledger_hotel_select"
  ON public.points_ledger
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());


-- ── 3. Updated trigger: recognition → ledger + balance ───────────────────────
--
-- Replaces the version from migration 019 (CREATE OR REPLACE updates in-place).
-- Now also appends to points_ledger for leaderboard aggregation.

CREATE OR REPLACE FUNCTION public.award_recognition_points()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  -- Fast running total (used by redemption balance checks)
  UPDATE public.employees
  SET    points_balance = points_balance + 10
  WHERE  id = NEW.receiver_id;

  -- Immutable ledger entry (source of truth for the leaderboard)
  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (NEW.receiver_id, 10, 'recognition_received', NEW.hotel);

  RETURN NEW;
END;
$$;

COMMENT ON FUNCTION public.award_recognition_points IS
  'Awards 10 points per recognition received.  Updates employees.points_balance '
  '(fast lookup) and inserts an audit row into points_ledger (leaderboard source).';


-- ── 4. admin_grant_points RPC ─────────────────────────────────────────────────
-- Allows admins to credit admin_bonus or campaign_reward points.
-- Negative values are accepted (point corrections / deductions).

CREATE OR REPLACE FUNCTION public.admin_grant_points(
  p_employee_id uuid,
  p_points      integer,
  p_source      text DEFAULT 'admin_bonus'
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_hotel text;
BEGIN
  IF p_points = 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Points must be non-zero.');
  END IF;

  IF p_source NOT IN ('admin_bonus', 'campaign_reward') THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Source must be admin_bonus or campaign_reward.');
  END IF;

  SELECT hotel INTO v_hotel
  FROM   public.employees
  WHERE  id = p_employee_id AND status = 'active';

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Active employee not found.');
  END IF;

  -- Update running balance (floor at 0 to prevent negatives)
  UPDATE public.employees
  SET    points_balance = GREATEST(0, points_balance + p_points)
  WHERE  id = p_employee_id;

  -- Immutable ledger row
  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (p_employee_id, p_points, p_source, v_hotel);

  RETURN jsonb_build_object(
    'ok',     true,
    'points', p_points,
    'source', p_source
  );
END;
$func$;

GRANT EXECUTE ON FUNCTION public.admin_grant_points(uuid, integer, text) TO anon;
GRANT EXECUTE ON FUNCTION public.admin_grant_points(uuid, integer, text) TO authenticated;

COMMENT ON FUNCTION public.admin_grant_points IS
  'Admin: credit or deduct points via admin_bonus / campaign_reward sources.';


-- ── 5. get_leaderboard — live leaderboard query ───────────────────────────────
--
-- Aggregates points_ledger within an optional date window.
-- Returns ranked active employees who have at least one ledger entry.
--
-- Call with NULL p_start / p_end for all-time ranking.

CREATE OR REPLACE FUNCTION public.get_leaderboard(
  p_hotel  text,
  p_start  timestamptz DEFAULT NULL,
  p_end    timestamptz DEFAULT NULL,
  p_limit  integer     DEFAULT 50
)
RETURNS TABLE (
  rank          bigint,
  employee_id   uuid,
  full_name     text,
  employee_code text,
  department    text,
  total_points  bigint,
  points_balance integer   -- all-time balance; used client-side for badge level
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT
    ROW_NUMBER() OVER (ORDER BY COALESCE(SUM(pl.points), 0) DESC) AS rank,
    e.id            AS employee_id,
    e.full_name,
    e.employee_code,
    e.department,
    COALESCE(SUM(pl.points), 0)                                   AS total_points,
    e.points_balance
  FROM  public.employees e
  LEFT  JOIN public.points_ledger pl
         ON  pl.employee_id = e.id
        AND  (p_start IS NULL OR pl.created_at >= p_start)
        AND  (p_end   IS NULL OR pl.created_at <  p_end)
  WHERE e.hotel  = p_hotel
    AND e.status = 'active'
  GROUP BY e.id, e.full_name, e.employee_code, e.department, e.points_balance
  HAVING COALESCE(SUM(pl.points), 0) > 0
  ORDER BY total_points DESC
  LIMIT  p_limit;
$$;

GRANT EXECUTE ON FUNCTION public.get_leaderboard(text, timestamptz, timestamptz, integer)
  TO anon;
GRANT EXECUTE ON FUNCTION public.get_leaderboard(text, timestamptz, timestamptz, integer)
  TO authenticated;

COMMENT ON FUNCTION public.get_leaderboard IS
  'Returns hotel-scoped ranked employees by total points within an optional date window. '
  'Pass NULL for p_start / p_end to get all-time rankings.';
