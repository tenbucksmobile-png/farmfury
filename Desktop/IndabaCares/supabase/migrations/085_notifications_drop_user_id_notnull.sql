-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 085 — Drop NOT NULL on notifications.user_id
--
-- notifications was created in migration 005 with both company_id and user_id
-- as NOT NULL. Migration 084 dropped company_id NOT NULL. This drops user_id
-- NOT NULL for the same reason: trg_notify_redemption inserts without user_id.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.notifications
  ALTER COLUMN user_id DROP NOT NULL;
