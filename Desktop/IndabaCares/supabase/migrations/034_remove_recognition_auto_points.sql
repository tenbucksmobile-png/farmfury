-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 034 — Remove automatic points-on-recognition
--
-- Previously, every INSERT into public.recognitions fired trg_recognition_points
-- which called award_recognition_points().  That function did two things:
--   1. UPDATE employees SET points_balance = points_balance + 10
--   2. INSERT INTO points_ledger (... 'recognition_received' ...)
--   3. Optionally a second ledger entry for campaign multiplier bonus points.
--
-- Recognition creation now performs ONLY:
--   INSERT recognitions (message, sender_id, receiver_id, hotel)
--
-- Points must be granted explicitly via admin_grant_points() or a future
-- dedicated RPC.  The 'recognition_received' source value is retained in
-- the points_ledger CHECK constraint so existing historical rows remain valid.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. Drop the trigger first (it depends on the function)
DROP TRIGGER IF EXISTS trg_recognition_points ON public.recognitions;

-- 2. Drop the function
DROP FUNCTION IF EXISTS public.award_recognition_points();
