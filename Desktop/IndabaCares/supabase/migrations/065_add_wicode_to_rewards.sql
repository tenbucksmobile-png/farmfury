-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 065 — Add wicode column to rewards table
--
-- WiCode is the redemption code used for marketplace (retail) rewards.
-- Hotel rewards do not use a WiCode — they are redeemed via voucher email.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.rewards
  ADD COLUMN IF NOT EXISTS wicode text;

COMMENT ON COLUMN public.rewards.wicode IS
  'WiCode redemption code for marketplace (retail) rewards. Null for hotel rewards.';
