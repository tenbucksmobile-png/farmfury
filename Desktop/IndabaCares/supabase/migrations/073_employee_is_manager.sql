-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 073 — Employee is_manager flag
--
-- The leaderboard has separate "Employees" and "Management" tabs.
-- Previously is_manager was never stored — the management tab was always empty.
--
-- Changes:
--   1. Add is_manager boolean column to employees (default false)
--   2. Rebuild get_leaderboard() to surface is_manager in its return set
-- ─────────────────────────────────────────────────────────────────────────────

-- ─── 1. Column ────────────────────────────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS is_manager boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN public.employees.is_manager IS
  'True for supervisors, managers, HODs, and directors. '
  'Controls which leaderboard tab the employee appears under: '
  'false → Employees tab, true → Management tab.';

-- ─── 2. Rebuild get_leaderboard() — add is_manager to return set ──────────────

DROP FUNCTION IF EXISTS public.get_leaderboard(text, timestamptz, timestamptz, integer);

CREATE OR REPLACE FUNCTION public.get_leaderboard(
  p_hotel  text,
  p_start  timestamptz DEFAULT NULL,
  p_end    timestamptz DEFAULT NULL,
  p_limit  integer     DEFAULT 50
)
RETURNS TABLE (
  rank              bigint,
  employee_id       uuid,
  full_name         text,
  employee_code     text,
  total_points      bigint,
  points_balance    integer,
  job_title         text,
  avatar_url        text,
  podium_photo_url  text,
  is_manager        boolean
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
    e.points_balance,
    e.job_title,
    e.photo_url                                                     AS avatar_url,
    e.podium_photo_url,
    e.is_manager
  FROM  public.employees e
  LEFT  JOIN public.points_ledger pl
         ON  pl.employee_id = e.id
        AND  pl.hotel       = e.hotel
        AND  (p_start IS NULL OR pl.created_at >= p_start)
        AND  (p_end   IS NULL OR pl.created_at <  p_end)
  WHERE e.hotel  = p_hotel
    AND e.status = 'active'
  GROUP BY e.id, e.full_name, e.employee_code, e.points_balance,
           e.job_title, e.photo_url, e.podium_photo_url, e.is_manager
  HAVING COALESCE(SUM(pl.points), 0) > 0
  ORDER BY total_points DESC
  LIMIT  p_limit;
$$;

GRANT EXECUTE ON FUNCTION public.get_leaderboard(text, timestamptz, timestamptz, integer)
  TO anon;
GRANT EXECUTE ON FUNCTION public.get_leaderboard(text, timestamptz, timestamptz, integer)
  TO authenticated;

COMMENT ON FUNCTION public.get_leaderboard IS
  'Returns hotel-scoped ranked employees by points earned within an optional date window.
   is_manager drives the Employees vs Management tab split in the mobile leaderboard.
   Employees with zero points in the selected period are excluded.';
