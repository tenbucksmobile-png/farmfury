-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 070: Multi-hotel rewards
--
-- Allows a single reward to be valid across multiple hotels.
--
-- Changes:
--   1. Add hotels text[] column to rewards (non-breaking alongside hotel)
--   2. Backfill hotels from existing hotel column
--   3. Update rewards_hotel_select RLS to use ANY(hotels)
--
-- The hotel column is kept for admin filtering and backwards compatibility.
-- New writes set hotel = hotels[0] (primary) and hotels = full array.
-- ─────────────────────────────────────────────────────────────────────────────

-- ── 1. Add hotels array column ───────────────────────────────────────────────

ALTER TABLE public.rewards
  ADD COLUMN IF NOT EXISTS hotels text[] NOT NULL DEFAULT '{}';

-- ── 2. Backfill existing rows ────────────────────────────────────────────────

UPDATE public.rewards
SET hotels = ARRAY[hotel]
WHERE array_length(hotels, 1) IS NULL;

-- ── 3. Update RLS to use hotels array ────────────────────────────────────────
--
-- Previous policy (migration 066): hotel = current_employee_hotel() OR is_apa()
-- New policy: current_employee_hotel() = ANY(hotels) OR is_apa()

DROP POLICY IF EXISTS "rewards_hotel_select" ON public.rewards;

CREATE POLICY "rewards_hotel_select"
  ON public.rewards
  FOR SELECT
  TO authenticated, anon
  USING (
    public.current_employee_is_apa()
    OR current_employee_hotel() = ANY(hotels)
  );
