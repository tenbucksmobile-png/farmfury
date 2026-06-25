-- =============================================================================
-- Migration 033 — Rebuild get_leaderboard()
--
-- Problem (migration 021):
--   get_leaderboard() returns a `department` column and groups by e.department.
--   The department column was dropped from public.employees in migration 030.
--   Calling the RPC now throws:
--     ERROR: column e.department does not exist
--
-- Changes:
--   • Drop and recreate get_leaderboard() without the department column.
--   • leaderboard_cache was already dropped in migrations 029/030 — no action
--     needed here. The leaderboard is now fully dynamic (live aggregation from
--     points_ledger JOIN employees).
--   • refresh_leaderboard() (migration 008) and the refresh-leaderboard Edge
--     Function are dead code — leaderboard_cache no longer exists.
--     Drop refresh_leaderboard() to avoid confusion.
--
-- Query design:
--   • LEFT JOIN so employees with 0 ledger rows in the period are included with
--     total_points = 0 (filtered out by HAVING to keep the board meaningful).
--   • Period filtering on pl.created_at allows monthly / quarterly / annual views.
--   • ROW_NUMBER() produces a stable, gapless rank ordered by total_points DESC.
--   • points_balance (all-time running total on employees) is returned separately
--     so the client can compute badge level independently of the selected period.
--
-- Safe to re-run: DROP + CREATE OR REPLACE.
-- =============================================================================


-- ─── 1. Drop the stale department-referencing version ─────────────────────────

DROP FUNCTION IF EXISTS public.get_leaderboard(text, timestamptz, timestamptz, integer);


-- ─── 2. Drop the now-dead refresh_leaderboard function ────────────────────────
--
-- refresh_leaderboard() populated leaderboard_cache (dropped in migrations
-- 029/030). Keeping it would cause a runtime error if ever called.

DROP FUNCTION IF EXISTS public.refresh_leaderboard(uuid);


-- ─── 3. Recreate get_leaderboard() — employees + points_ledger, no cache ──────

CREATE OR REPLACE FUNCTION public.get_leaderboard(
  p_hotel  text,
  p_start  timestamptz DEFAULT NULL,
  p_end    timestamptz DEFAULT NULL,
  p_limit  integer     DEFAULT 50
)
RETURNS TABLE (
  rank           bigint,
  employee_id    uuid,
  full_name      text,
  employee_code  text,
  total_points   bigint,
  points_balance integer   -- all-time balance; used client-side for badge level
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT
    ROW_NUMBER() OVER (ORDER BY COALESCE(SUM(pl.points), 0) DESC)  AS rank,
    e.id                                                            AS employee_id,
    e.full_name,
    e.employee_code,
    COALESCE(SUM(pl.points), 0)                                    AS total_points,
    e.points_balance
  FROM  public.employees e
  LEFT  JOIN public.points_ledger pl
         ON  pl.employee_id = e.id
        AND  pl.hotel       = e.hotel       -- belt-and-suspenders hotel isolation
        AND  (p_start IS NULL OR pl.created_at >= p_start)
        AND  (p_end   IS NULL OR pl.created_at <  p_end)
  WHERE e.hotel  = p_hotel
    AND e.status = 'active'
  GROUP BY e.id, e.full_name, e.employee_code, e.points_balance
  HAVING COALESCE(SUM(pl.points), 0) > 0   -- exclude zero-point employees
  ORDER BY total_points DESC
  LIMIT  p_limit;
$$;

GRANT EXECUTE ON FUNCTION public.get_leaderboard(text, timestamptz, timestamptz, integer)
  TO anon;
GRANT EXECUTE ON FUNCTION public.get_leaderboard(text, timestamptz, timestamptz, integer)
  TO authenticated;

COMMENT ON FUNCTION public.get_leaderboard IS
  'Returns hotel-scoped ranked employees by points earned within an optional date window.
   Computes live from employees JOIN points_ledger — no cache table.
   p_start / p_end = NULL → all-time ranking.
   Employees with zero points in the selected period are excluded (HAVING > 0).
   points_balance is the all-time running total used by the client for badge levels.';
