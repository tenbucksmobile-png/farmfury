-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 066 — APA Cross-Hotel Visibility
--
-- Employees logged in as 'African Procurement Agencies' are group directors
-- and executives who require full visibility across all hotels.
--
-- Changes:
--   1. current_employee_is_apa() helper function
--   2. employees      SELECT — APA sees all hotels
--   3. recognitions   SELECT — APA sees all hotel feeds
--   4. leaderboard    SELECT — APA sees all hotel leaderboards
--   5. rewards        SELECT — APA sees all hotel reward catalogues
--
-- Write policies (INSERT) are unchanged — recognitions sent by APA are
-- tagged to the recipient's hotel by the send-recognition edge function.
-- ─────────────────────────────────────────────────────────────────────────────

-- ── 1. Helper: is the current session an APA employee? ────────────────────────

CREATE OR REPLACE FUNCTION public.current_employee_is_apa()
RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT public.current_employee_hotel() = 'African Procurement Agencies';
$$;

COMMENT ON FUNCTION public.current_employee_is_apa IS
  'Returns true when the current session belongs to an APA employee.
   Used to grant cross-hotel read visibility to group directors/executives.';


-- ── 2. employees — APA sees all ───────────────────────────────────────────────

DROP POLICY IF EXISTS "employees_hotel_select" ON public.employees;

CREATE POLICY "employees_hotel_select"
  ON public.employees
  FOR SELECT
  TO anon, authenticated
  USING (
    hotel = public.current_employee_hotel()
    OR public.current_employee_is_apa()
  );


-- ── 3. recognitions — APA sees all ───────────────────────────────────────────

DROP POLICY IF EXISTS "recognitions_hotel_select" ON public.recognitions;

CREATE POLICY "recognitions_hotel_select"
  ON public.recognitions
  FOR SELECT
  TO anon, authenticated
  USING (
    hotel = public.current_employee_hotel()
    OR public.current_employee_is_apa()
  );


-- ── 4. leaderboard — fully dynamic via get_leaderboard() RPC ────────────────
-- leaderboard_cache was dropped in migrations 029/030.
-- The leaderboard is now a live aggregation function — no RLS policy needed.
-- APA cross-hotel leaderboard access is handled in the mobile app by passing
-- hotel = NULL (or all hotels) to get_leaderboard().


-- ── 5. rewards — APA sees all ─────────────────────────────────────────────────

DROP POLICY IF EXISTS "rewards_hotel_select" ON public.rewards;

CREATE POLICY "rewards_hotel_select"
  ON public.rewards
  FOR SELECT
  TO anon, authenticated
  USING (
    hotel = public.current_employee_hotel()
    OR public.current_employee_is_apa()
  );
