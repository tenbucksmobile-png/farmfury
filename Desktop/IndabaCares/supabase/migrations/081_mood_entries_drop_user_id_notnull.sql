-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 081 — Fix mood_entries user_id NOT NULL constraint
--
-- mood_entries was created in migration 004 with user_id uuid NOT NULL
-- referencing public.profiles (legacy auth schema). profiles was removed in
-- migrations 029/030, but the NOT NULL constraint was never relaxed.
--
-- Migration 069 replaced submit_mood with an employee-schema version that
-- inserts (employee_id, mood, note, entry_date) without user_id, causing:
--   "null value in column 'user_id' violates not-null constraint"
--
-- Fix: make user_id nullable (same pattern as migration 074 for company_id).
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.mood_entries
  ALTER COLUMN user_id DROP NOT NULL;
