-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 074 — Fix mood_entries company_id NOT NULL constraint
--
-- mood_entries was created in migration 004 with company_id uuid NOT NULL
-- (legacy multi-tenant schema). The company/profiles layer was removed in
-- migrations 029/030, but the NOT NULL constraint was never relaxed.
--
-- Migration 069 replaced submit_mood with an employee-schema version that
-- inserts (employee_id, mood, note, entry_date) without company_id, causing:
--   "null value in column 'company_id' violates not-null constraint"
--
-- Fix: make company_id nullable so the new insert path works.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.mood_entries
  ALTER COLUMN company_id DROP NOT NULL;
