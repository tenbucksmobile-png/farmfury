-- =============================================================================
-- Migration 031 — RLS Policy Cleanup
--
-- Removes every policy that references System 1 auth primitives:
--   auth.jwt() / auth.uid() / request.jwt.claims
--   current_company_id() / current_user_role() / has_role()
--
-- These functions return NULL or 'employee' for employee-auth sessions,
-- so every policy that uses them silently denies all access — they are
-- dead weight that creates confusion and hides the real security model.
--
-- Security model after this migration:
--   Authentication  — employee_active_sessions + x-session-token header
--   Tenant isolation — current_employee_hotel()  (USING hotel = ...)
--   Identity scope  — current_employee_id()       (USING employee_id = ...)
--   Writes          — SECURITY DEFINER RPCs (bypass RLS entirely)
--
-- Policies retained (correct, already use session-based helpers):
--   employees_hotel_select           (017)
--   recognitions_hotel_select/insert (018)
--   likes_hotel_select/insert/delete (018)
--   rec_comments_hotel_select/insert/delete (018)
--   rewards_hotel_select             (017)
--   redemptions_hotel_select/insert  (017)
--   messages_hotel_select/insert     (017)
--   ledger_hotel_select              (021)
--   notifications_own_select/update  (025)
--
-- Policies already removed by earlier migrations via CASCADE:
--   All policies on: profiles, companies, departments, employee_codes,
--   invite_tokens, point_transactions, star_transactions, leaderboard_cache
--   → Dropped in migration 030 (DROP TABLE ... CASCADE)
--
--   All policies on: recognition_recipients, reactions, comments,
--   thumbs_up_types, company_values, recognitions (original)
--   → Dropped in migration 018 (DROP TABLE ... CASCADE)
--
--   All policies on: reward_categories
--   → Dropped in migration 019 (DROP TABLE ... CASCADE)
--
-- All DROPs use IF EXISTS — safe to re-run.
-- =============================================================================


-- ─── 1. employees — JWT-based admin write policies (migration 016) ────────────
--
-- These checked request.jwt.claims → app_metadata → role, which is always
-- empty for employee-auth sessions.  Admin operations on employees are
-- performed exclusively via SECURITY DEFINER RPCs (admin_insert_employee,
-- admin_bulk_insert_employees, admin_deactivate_employee, etc.) which bypass
-- RLS entirely through the service_role key.
--
-- employees_select_own / employees_select_admin were already dropped in
-- migration 017.  Listed here for documentation completeness.

DROP POLICY IF EXISTS "employees_select_own"         ON public.employees;  -- 014, already gone (017)
DROP POLICY IF EXISTS "employees_select_admin"       ON public.employees;  -- 014, already gone (017)
DROP POLICY IF EXISTS "employees_insert_admin"       ON public.employees;  -- 016, jwt.claims
DROP POLICY IF EXISTS "employees_update_admin"       ON public.employees;  -- 016, jwt.claims
DROP POLICY IF EXISTS "employees_delete_super_admin" ON public.employees;  -- 016, jwt.claims


-- ─── 2. rewards — residual company_id / has_role policies (migration 006) ─────
--
-- Migration 017 dropped rewards_select and rewards_insert_admin (via DROP POLICY
-- IF EXISTS) and added rewards_hotel_select.  rewards_update_admin from 006 was
-- never explicitly dropped.

DROP POLICY IF EXISTS "rewards_select"        ON public.rewards;  -- 006, current_company_id()
DROP POLICY IF EXISTS "rewards_insert_admin"  ON public.rewards;  -- 006, has_role()
DROP POLICY IF EXISTS "rewards_update_admin"  ON public.rewards;  -- 006, has_role()


-- ─── 3. redemptions — residual auth.uid() / has_role policies (migration 006) ─
--
-- Migration 017 dropped redemptions_select_own and redemptions_insert_own (the
-- 005 policy names) and added redemptions_hotel_select/insert.  The three 006
-- policies below used different names and were never explicitly dropped.

DROP POLICY IF EXISTS "redemptions_select"        ON public.redemptions;  -- 006, auth.uid()
DROP POLICY IF EXISTS "redemptions_update_cancel" ON public.redemptions;  -- 006, auth.uid()
DROP POLICY IF EXISTS "redemptions_update_admin"  ON public.redemptions;  -- 006, has_role()


-- ─── 4. skill_categories — company_id / has_role policies (migration 006) ──────
--
-- skill_categories still exists but its company_id FK was CASCADE-dropped when
-- public.companies was dropped in migration 030.  The RLS policies check
-- company_id = current_company_id() which returns NULL → permanent deny.
-- Writes go through service_role (admin panel); no replacement policies needed.

DROP POLICY IF EXISTS "skill_categories_select"        ON public.skill_categories;
DROP POLICY IF EXISTS "skill_categories_insert_admin"  ON public.skill_categories;
DROP POLICY IF EXISTS "skill_categories_update_admin"  ON public.skill_categories;


-- ─── 5. skill_indicators — company_id / has_role policies (migration 006) ──────

DROP POLICY IF EXISTS "skill_indicators_select"        ON public.skill_indicators;
DROP POLICY IF EXISTS "skill_indicators_insert_admin"  ON public.skill_indicators;
DROP POLICY IF EXISTS "skill_indicators_update_admin"  ON public.skill_indicators;


-- ─── 6. skill_ratings — auth.uid() policies (migration 006) ──────────────────
--
-- skill_ratings.rater_id and recipient_id referenced public.profiles (dropped
-- in migration 030).  The FKs are gone; the data is stale.  The policies that
-- checked auth.uid() against those columns are permanently broken.

DROP POLICY IF EXISTS "skill_ratings_select_recipient" ON public.skill_ratings;
DROP POLICY IF EXISTS "skill_ratings_insert"           ON public.skill_ratings;


-- ─── 7. mood_entries — auth.uid() / company_id policies (migration 006) ───────
--
-- mood_entries.user_id referenced public.profiles (dropped).  The column
-- contains stale profile UUIDs.  The policies that checked user_id = auth.uid()
-- always evaluated to false anyway (no JWT session).

DROP POLICY IF EXISTS "mood_entries_select_own"   ON public.mood_entries;
DROP POLICY IF EXISTS "mood_entries_select_admin" ON public.mood_entries;
DROP POLICY IF EXISTS "mood_entries_insert_own"   ON public.mood_entries;


-- ─── 8. badges — company_id / has_role policies (migration 006) ───────────────

DROP POLICY IF EXISTS "badges_select"        ON public.badges;
DROP POLICY IF EXISTS "badges_insert_admin"  ON public.badges;
DROP POLICY IF EXISTS "badges_update_admin"  ON public.badges;


-- ─── 9. user_badges — broken policy (migration 006) ──────────────────────────
--
-- user_badges_select did:
--   EXISTS (SELECT 1 FROM public.profiles WHERE profiles.id = user_badges.user_id ...)
-- public.profiles was dropped in migration 030 → this policy would cause a
-- runtime error on every SELECT attempt.

DROP POLICY IF EXISTS "user_badges_select" ON public.user_badges;


-- ─── 10. budget_configs — has_role policies (migration 006) ──────────────────
--
-- Budget config management is an admin-panel operation via service_role.
-- No client-side policies are needed.

DROP POLICY IF EXISTS "budget_configs_select_admin" ON public.budget_configs;
DROP POLICY IF EXISTS "budget_configs_insert_admin" ON public.budget_configs;
DROP POLICY IF EXISTS "budget_configs_update_admin" ON public.budget_configs;
DROP POLICY IF EXISTS "budget_configs_delete_admin" ON public.budget_configs;


-- ─── 11. audit_logs — has_role policy (migration 006) ────────────────────────
--
-- Audit logs are read by the admin panel via service_role only.

DROP POLICY IF EXISTS "audit_logs_select_super_admin" ON public.audit_logs;


-- =============================================================================
-- Post-cleanup state summary
-- =============================================================================
--
-- Table                    Active policies after this migration
-- ───────────────────────  ──────────────────────────────────────────────────────
-- employees                employees_hotel_select              (hotel = ceh())
-- recognitions             recognitions_hotel_select/insert    (hotel = ceh())
-- recognition_likes        likes_hotel_select/insert/delete    (hotel = ceh())
-- recognition_comments     rec_comments_hotel_select/insert/delete (hotel = ceh())
-- rewards                  rewards_hotel_select                (hotel = ceh())
-- redemptions              redemptions_hotel_select/insert     (hotel = ceh())
-- messages                 messages_hotel_select/insert        (hotel = ceh())
-- points_ledger            ledger_hotel_select                 (hotel = ceh())
-- notifications            notifications_own_select/update     (hotel + ceid())
-- employee_active_sessions (no client policies — SECURITY DEFINER only)
--
-- skill_categories         NO POLICIES — service_role access only (*)
-- skill_indicators         NO POLICIES — service_role access only (*)
-- skill_ratings            NO POLICIES — service_role access only (*)
-- mood_entries             NO POLICIES — service_role access only (*)
-- badges                   NO POLICIES — service_role access only
-- user_badges              NO POLICIES — service_role access only
-- budget_configs           NO POLICIES — service_role access only
-- audit_logs               NO POLICIES — service_role access only
--
-- (*) These tables require a schema migration to add employee_id / hotel
--     columns before hotel-based RLS policies can be written for them.
--     Until then, all reads must go through SECURITY DEFINER RPCs or
--     the service_role key (admin panel).
--
-- ceh() = public.current_employee_hotel()
-- ceid() = public.current_employee_id()
-- =============================================================================
