-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 075 — Add has_seen_welcome flag to employees
--
-- Tracks whether an employee has seen the welcome/onboarding video on first
-- login. Stored in DB so the flag persists across devices and reinstalls.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS has_seen_welcome boolean NOT NULL DEFAULT false;
