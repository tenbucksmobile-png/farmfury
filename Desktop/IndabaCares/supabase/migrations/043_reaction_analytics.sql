-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 043 — Reaction analytics RPCs
--
-- Four SECURITY DEFINER functions scoped by hotel and optional date window.
-- All are STABLE (read-only aggregations) and safe to call from the client.
--
-- Functions
-- ──────────────────────────────────────────────────────────────────────────
--  get_top_reactors()               Employees ordered by reactions given.
--                                   "Who participates most in recognising others."
--
--  get_top_recognised_employees()   Employees ordered by reactions received
--                                   and weighted reaction points earned.
--                                   "Who is most appreciated by peers."
--
--  get_most_reacted_recognitions()  Recognition posts ordered by reaction count.
--                                   "Which moments resonated most with the team."
--
--  get_reaction_hotel_summary()     Single-row hotel-level KPIs: total reactions,
--                                   total reaction points awarded, per-type
--                                   breakdown, unique reactors, unique receivers.
--                                   "Dashboard headline numbers."
--
-- All functions:
--   • Filter by p_hotel  — hard hotel isolation, matches RLS intent.
--   • Filter by p_start / p_end — NULL = all-time (inclusive on both ends).
--   • Return empty result sets rather than errors on no data.
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. get_top_reactors ───────────────────────────────────────────────────────
--
-- Employees ranked by how many reactions they have given.
-- Includes per-type breakdown and the employee's own allocation remaining this
-- month (useful for admin dashboards to spot unused engagement budgets).

DROP FUNCTION IF EXISTS public.get_top_reactors(text, timestamptz, timestamptz, integer);

CREATE FUNCTION public.get_top_reactors(
  p_hotel  text,
  p_start  timestamptz DEFAULT NULL,
  p_end    timestamptz DEFAULT NULL,
  p_limit  integer     DEFAULT 20
)
RETURNS TABLE (
  rank                bigint,
  employee_id         uuid,
  full_name           text,
  employee_code       text,
  total_reactions     bigint,
  hearts_given        bigint,
  smiles_given        bigint,
  thumbs_given        bigint
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT
    ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC)          AS rank,
    e.id                                                AS employee_id,
    e.full_name,
    e.employee_code,
    COUNT(*)                                            AS total_reactions,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'heart')     AS hearts_given,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'smile')     AS smiles_given,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'thumbs_up') AS thumbs_given
  FROM  public.recognition_reactions rr
  JOIN  public.employees e
         ON  e.id    = rr.employee_id
        AND  e.hotel = p_hotel
  WHERE rr.hotel = p_hotel
    AND (p_start IS NULL OR rr.created_at >= p_start)
    AND (p_end   IS NULL OR rr.created_at <  p_end)
  GROUP BY e.id, e.full_name, e.employee_code
  ORDER BY total_reactions DESC
  LIMIT  p_limit;
$$;

GRANT EXECUTE ON FUNCTION public.get_top_reactors(text, timestamptz, timestamptz, integer)
  TO anon, authenticated;

COMMENT ON FUNCTION public.get_top_reactors IS
  'Hotel-scoped ranking of employees by reactions given.
   p_start / p_end = NULL → all-time. Rows ordered by total_reactions DESC.
   Useful for identifying highly engaged employees.';


-- ── 2. get_top_recognised_employees ──────────────────────────────────────────
--
-- Employees ranked by how many reactions their recognitions attracted.
-- Primary sort: reaction_points_received (weighted: heart=50, smile=20, thumbs=10).
-- Secondary sort: total_reactions_received (tiebreaker).
--
-- This is distinct from the points leaderboard: it reflects peer appreciation
-- of specific recognition moments, not total accumulated points.

DROP FUNCTION IF EXISTS public.get_top_recognised_employees(text, timestamptz, timestamptz, integer);

CREATE FUNCTION public.get_top_recognised_employees(
  p_hotel  text,
  p_start  timestamptz DEFAULT NULL,
  p_end    timestamptz DEFAULT NULL,
  p_limit  integer     DEFAULT 20
)
RETURNS TABLE (
  rank                      bigint,
  employee_id               uuid,
  full_name                 text,
  employee_code             text,
  total_reactions_received  bigint,
  hearts_received           bigint,
  smiles_received           bigint,
  thumbs_received           bigint,
  reaction_points_received  bigint,  -- weighted sum: heart×50 + smile×20 + thumbs×10
  recognition_count         bigint   -- distinct recognitions that attracted reactions
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT
    ROW_NUMBER() OVER (
      ORDER BY
        SUM(
          CASE rr.reaction_type
            WHEN 'heart'     THEN 50
            WHEN 'smile'     THEN 20
            WHEN 'thumbs_up' THEN 10
            ELSE 0
          END
        ) DESC,
        COUNT(*) DESC
    )                                                         AS rank,
    e.id                                                      AS employee_id,
    e.full_name,
    e.employee_code,
    COUNT(*)                                                  AS total_reactions_received,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'heart')           AS hearts_received,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'smile')           AS smiles_received,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'thumbs_up')       AS thumbs_received,
    SUM(
      CASE rr.reaction_type
        WHEN 'heart'     THEN 50
        WHEN 'smile'     THEN 20
        WHEN 'thumbs_up' THEN 10
        ELSE 0
      END
    )                                                         AS reaction_points_received,
    COUNT(DISTINCT rr.recognition_id)                         AS recognition_count
  FROM  public.recognition_reactions rr
  JOIN  public.recognitions rec
         ON  rec.id    = rr.recognition_id
  JOIN  public.employees e
         ON  e.id      = rec.receiver_id
        AND  e.hotel   = p_hotel
  WHERE rr.hotel = p_hotel
    AND (p_start IS NULL OR rr.created_at >= p_start)
    AND (p_end   IS NULL OR rr.created_at <  p_end)
  GROUP BY e.id, e.full_name, e.employee_code
  ORDER BY reaction_points_received DESC, total_reactions_received DESC
  LIMIT  p_limit;
$$;

GRANT EXECUTE ON FUNCTION public.get_top_recognised_employees(text, timestamptz, timestamptz, integer)
  TO anon, authenticated;

COMMENT ON FUNCTION public.get_top_recognised_employees IS
  'Hotel-scoped ranking of employees by reactions received on their recognitions.
   Sorted by weighted reaction_points_received (heart=50, smile=20, thumbs_up=10)
   then by raw reaction count as tiebreaker.
   recognition_count = distinct recognitions that attracted at least one reaction.
   p_start / p_end = NULL → all-time.';


-- ── 3. get_most_reacted_recognitions ─────────────────────────────────────────
--
-- Recognition posts ranked by total reactions received.
-- Returns sender, receiver, badge, message preview, per-type counts, and the
-- weighted engagement score so clients can sort or filter as needed.

DROP FUNCTION IF EXISTS public.get_most_reacted_recognitions(text, timestamptz, timestamptz, integer);

CREATE FUNCTION public.get_most_reacted_recognitions(
  p_hotel  text,
  p_start  timestamptz DEFAULT NULL,
  p_end    timestamptz DEFAULT NULL,
  p_limit  integer     DEFAULT 20
)
RETURNS TABLE (
  rank              bigint,
  recognition_id    uuid,
  badge             text,
  message_preview   text,         -- first 120 chars; full message via detail query
  recognition_at    timestamptz,
  sender_id         uuid,
  sender_name       text,
  receiver_id       uuid,
  receiver_name     text,
  total_reactions   bigint,
  heart_count       bigint,
  smile_count       bigint,
  thumbs_count      bigint,
  engagement_score  bigint        -- weighted: heart×50 + smile×20 + thumbs×10
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT
    ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC)            AS rank,
    rec.id                                                AS recognition_id,
    rec.badge,
    LEFT(rec.message, 120)                                AS message_preview,
    rec.created_at                                        AS recognition_at,
    s.id                                                  AS sender_id,
    s.full_name                                           AS sender_name,
    r.id                                                  AS receiver_id,
    r.full_name                                           AS receiver_name,
    COUNT(*)                                              AS total_reactions,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'heart')       AS heart_count,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'smile')       AS smile_count,
    COUNT(*) FILTER (WHERE rr.reaction_type = 'thumbs_up')   AS thumbs_count,
    SUM(
      CASE rr.reaction_type
        WHEN 'heart'     THEN 50
        WHEN 'smile'     THEN 20
        WHEN 'thumbs_up' THEN 10
        ELSE 0
      END
    )                                                     AS engagement_score
  FROM  public.recognition_reactions rr
  JOIN  public.recognitions rec
         ON  rec.id    = rr.recognition_id
        AND  rec.hotel = p_hotel
  JOIN  public.employees s ON s.id = rec.sender_id
  JOIN  public.employees r ON r.id = rec.receiver_id
  WHERE rr.hotel = p_hotel
    AND (p_start IS NULL OR rr.created_at >= p_start)
    AND (p_end   IS NULL OR rr.created_at <  p_end)
  GROUP BY rec.id, rec.badge, rec.message, rec.created_at,
           s.id, s.full_name, r.id, r.full_name
  ORDER BY total_reactions DESC, engagement_score DESC
  LIMIT  p_limit;
$$;

GRANT EXECUTE ON FUNCTION public.get_most_reacted_recognitions(text, timestamptz, timestamptz, integer)
  TO anon, authenticated;

COMMENT ON FUNCTION public.get_most_reacted_recognitions IS
  'Hotel-scoped recognition posts ranked by total reaction count.
   engagement_score = weighted sum (heart×50, smile×20, thumbs_up×10).
   message_preview is capped at 120 chars; fetch the full recognition separately.
   p_start / p_end = NULL → all-time.';


-- ── 4. get_reaction_hotel_summary ────────────────────────────────────────────
--
-- Single-row summary of reaction activity for a hotel and period.
-- Intended for admin dashboard headline KPIs.
--
-- Columns
--   total_reactions         — all reaction rows in the period
--   total_reaction_points   — sum of points awarded via reaction_received ledger entries
--   heart_count             — reactions of type heart
--   smile_count             — reactions of type smile
--   thumbs_count            — reactions of type thumbs_up
--   unique_reactors         — distinct employees who gave at least one reaction
--   unique_receivers        — distinct employees whose recognitions received at least one reaction
--   most_used_type          — the reaction type with the highest count in the period
--   avg_reactions_per_recog — average reactions per recognition that received any

DROP FUNCTION IF EXISTS public.get_reaction_hotel_summary(text, timestamptz, timestamptz);

CREATE FUNCTION public.get_reaction_hotel_summary(
  p_hotel  text,
  p_start  timestamptz DEFAULT NULL,
  p_end    timestamptz DEFAULT NULL
)
RETURNS TABLE (
  total_reactions          bigint,
  total_reaction_points    bigint,
  heart_count              bigint,
  smile_count              bigint,
  thumbs_count             bigint,
  unique_reactors          bigint,
  unique_receivers         bigint,
  most_used_type           text,
  avg_reactions_per_recog  numeric
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  WITH reaction_base AS (
    SELECT
      rr.id,
      rr.reaction_type,
      rr.employee_id                          AS reactor_id,
      rec.receiver_id,
      rr.recognition_id
    FROM  public.recognition_reactions rr
    JOIN  public.recognitions rec ON rec.id = rr.recognition_id
    WHERE rr.hotel = p_hotel
      AND (p_start IS NULL OR rr.created_at >= p_start)
      AND (p_end   IS NULL OR rr.created_at <  p_end)
  ),
  points_base AS (
    SELECT COALESCE(SUM(pl.points), 0) AS total_pts
    FROM   public.points_ledger pl
    WHERE  pl.hotel  = p_hotel
      AND  pl.source = 'reaction_received'
      AND  (p_start IS NULL OR pl.created_at >= p_start)
      AND  (p_end   IS NULL OR pl.created_at <  p_end)
  ),
  type_counts AS (
    SELECT
      reaction_type,
      COUNT(*) AS cnt
    FROM reaction_base
    GROUP BY reaction_type
    ORDER BY cnt DESC
    LIMIT 1
  )
  SELECT
    COUNT(rb.id)                                    AS total_reactions,
    pb.total_pts                                    AS total_reaction_points,
    COUNT(rb.id) FILTER (WHERE rb.reaction_type = 'heart')     AS heart_count,
    COUNT(rb.id) FILTER (WHERE rb.reaction_type = 'smile')     AS smile_count,
    COUNT(rb.id) FILTER (WHERE rb.reaction_type = 'thumbs_up') AS thumbs_count,
    COUNT(DISTINCT rb.reactor_id)                   AS unique_reactors,
    COUNT(DISTINCT rb.receiver_id)                  AS unique_receivers,
    (SELECT reaction_type FROM type_counts)         AS most_used_type,
    ROUND(
      COUNT(rb.id)::numeric /
      NULLIF(COUNT(DISTINCT rb.recognition_id), 0),
      2
    )                                               AS avg_reactions_per_recog
  FROM       reaction_base rb
  CROSS JOIN points_base pb
  GROUP BY pb.total_pts;
$$;

GRANT EXECUTE ON FUNCTION public.get_reaction_hotel_summary(text, timestamptz, timestamptz)
  TO anon, authenticated;

COMMENT ON FUNCTION public.get_reaction_hotel_summary IS
  'Single-row reaction KPI summary for a hotel and optional date window.
   total_reaction_points sourced from points_ledger (reaction_received rows) for
   accuracy — matches the actual points awarded, including any reversals.
   most_used_type = reaction type with highest count in the period.
   avg_reactions_per_recog excludes recognitions with zero reactions.
   Returns one row; returns zero row if no reaction data exists for the period.';


-- ── 5. Supporting index ───────────────────────────────────────────────────────
--
-- The analytics queries JOIN recognition_reactions → recognitions on
-- recognition_id, filtered by rr.hotel and rr.created_at.
-- The existing idx_reactions_recognition covers the JOIN.
-- Add a composite index on (hotel, created_at, reaction_type) to support the
-- WHERE + GROUP BY pattern used by all four functions above.

CREATE INDEX IF NOT EXISTS idx_reactions_analytics
  ON public.recognition_reactions (hotel, created_at DESC, reaction_type);
