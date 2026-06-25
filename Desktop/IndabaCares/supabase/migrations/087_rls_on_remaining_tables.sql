-- =============================================================================
-- Migration 087 — Enable RLS on auth_rate_limits and notifications_archive
--
-- Both tables were intentionally left without RLS in their original migrations
-- because they are only accessed via SECURITY DEFINER functions (which run as
-- the postgres superuser and bypass RLS). Supabase security advisories flag
-- any public table without RLS enabled, so we enable it here with no
-- client-facing policies — effectively blocking direct PostgREST access while
-- preserving all internal function access unchanged.
-- =============================================================================


-- ── auth_rate_limits ─────────────────────────────────────────────────────────
-- Created in migration 007. Written by record_rate_limit() and cleaned by
-- cleanup_rate_limits() — both SECURITY DEFINER. No client access needed.

ALTER TABLE public.auth_rate_limits ENABLE ROW LEVEL SECURITY;
-- No policies: SECURITY DEFINER functions run as postgres (bypasses RLS).
-- Direct anon/authenticated access is blocked by having no SELECT/INSERT policy.


-- ── notifications_archive ────────────────────────────────────────────────────
-- Created in migration 011. Populated exclusively by archive_old_notifications()
-- SECURITY DEFINER function (pg_cron). No direct client access needed.

ALTER TABLE public.notifications_archive ENABLE ROW LEVEL SECURITY;
-- No policies: same reasoning as auth_rate_limits above.
