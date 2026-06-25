-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 084 — Drop NOT NULL on notifications.company_id
--
-- notifications was created in migration 005 with company_id uuid NOT NULL.
-- Migration 023 rewrote the table to use employee_id + hotel instead, but
-- never dropped the NOT NULL constraint on company_id.
--
-- The trg_notify_redemption trigger (023) inserts without company_id, causing:
--   "null value in column "company_id" violates not-null constraint"
-- whenever approve_redemption() or reject_redemption() is called.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.notifications
  ALTER COLUMN company_id DROP NOT NULL;
