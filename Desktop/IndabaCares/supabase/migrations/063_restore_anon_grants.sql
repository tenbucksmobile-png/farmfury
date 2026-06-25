-- =============================================================================
-- Migration 063 — Restore anon role grants
-- =============================================================================
--
-- Problem introduced by migration 059:
--   REVOKE ALL ON ALL TABLES IN SCHEMA public FROM anon;
--
-- The mobile app uses the Supabase JS client with the anon key.  PostgREST
-- executes all anon-key requests as the `anon` PostgreSQL role.  With all
-- table grants revoked the app cannot read or write any data — every query
-- returns an empty result set regardless of RLS policies.
--
-- Fix:
--   Re-grant the minimum privileges needed for direct PostgREST queries.
--   RLS policies (current_employee_hotel / current_employee_id) remain the
--   security boundary — grants only allow the role to attempt a query;
--   RLS decides which rows are visible.
--
-- Tables written via Edge Functions (service_role) are SELECT-only here:
--   recognitions (send-recognition EF), mood_entries (submit-mood EF),
--   push_tokens (upsert_push_token RPC), redemptions (redeem-reward EF)
--
-- Tables written directly by the JS client get INSERT / DELETE too:
--   recognition_likes, recognition_comments, recognition_reactions,
--   skill_ratings, messages, employees (profile update only)
-- =============================================================================


-- ── Read-only tables (SELECT only) ────────────────────────────────────────────

GRANT SELECT ON public.recognitions                  TO anon;
GRANT SELECT ON public.rewards                       TO anon;
GRANT SELECT ON public.redemptions                   TO anon;
GRANT SELECT ON public.points_ledger                 TO anon;
GRANT SELECT ON public.mood_entries                  TO anon;
GRANT SELECT ON public.notifications                 TO anon;
GRANT SELECT ON public.badges                        TO anon;
GRANT SELECT ON public.user_badges                   TO anon;
GRANT SELECT ON public.skill_categories              TO anon;
GRANT SELECT ON public.monthly_legends               TO anon;
GRANT SELECT ON public.hotel_settings                TO anon;
GRANT SELECT ON public.employee_reaction_allocations TO anon;
GRANT SELECT ON public.initiatives                   TO anon;


-- ── employees — SELECT + UPDATE (profile name/hotel) ─────────────────────────
--
-- RLS policy "employees_read_hotel" already scopes SELECT to active employees
-- in the same hotel.  UPDATE is needed for updateEmployeeProfile() in queries.ts.

GRANT SELECT, UPDATE ON public.employees TO anon;


-- ── recognition_likes — SELECT + INSERT + DELETE ──────────────────────────────
--
-- Likes are toggled directly via the JS client (addLike / removeLike).

GRANT SELECT, INSERT, DELETE ON public.recognition_likes TO anon;


-- ── recognition_comments — SELECT + INSERT + DELETE ──────────────────────────
--
-- Comments are posted and deleted directly via the JS client.

GRANT SELECT, INSERT, DELETE ON public.recognition_comments TO anon;


-- ── recognition_reactions — SELECT + INSERT + DELETE ─────────────────────────
--
-- Reactions are inserted/deleted directly; the enforce_reaction_allocation
-- and award_reaction_points triggers run as SECURITY DEFINER.

GRANT SELECT, INSERT, DELETE ON public.recognition_reactions TO anon;


-- ── skill_ratings — SELECT + INSERT ──────────────────────────────────────────

GRANT SELECT, INSERT ON public.skill_ratings TO anon;


-- ── messages (hotel chat) — SELECT + INSERT ───────────────────────────────────

GRANT SELECT, INSERT ON public.messages TO anon;


-- ── Sequences used by INSERT statements ──────────────────────────────────────
--
-- gen_random_uuid() does not need a sequence grant, but belt-and-suspenders:
-- restore usage on all sequences the above tables may reference.

GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO anon;
