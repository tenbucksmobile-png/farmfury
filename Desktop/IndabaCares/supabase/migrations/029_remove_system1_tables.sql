-- =============================================================================
-- Migration 029 — Remove System 1 (Supabase Auth) tables
--
-- The app has migrated entirely to custom employee authentication.
-- System 1 (Supabase Auth JWT + profiles/companies/departments) is fully
-- replaced by System 2 (employees + employee_active_sessions + bcrypt RPCs).
--
-- These tables are no longer read or written by any active code path.
-- =============================================================================

-- Drop in dependency order (FK children before parents)

DROP TABLE IF EXISTS public.point_transactions   CASCADE;
DROP TABLE IF EXISTS public.invite_tokens        CASCADE;
DROP TABLE IF EXISTS public.employee_codes       CASCADE;
DROP TABLE IF EXISTS public.leaderboard_cache    CASCADE;  -- references profiles.user_id
DROP TABLE IF EXISTS public.profiles             CASCADE;
DROP TABLE IF EXISTS public.departments          CASCADE;
DROP TABLE IF EXISTS public.companies            CASCADE;

-- Drop System 1 trigger functions that reference auth.users
DROP FUNCTION IF EXISTS public.handle_new_user()            CASCADE;
DROP FUNCTION IF EXISTS public.sync_role_to_jwt()           CASCADE;
DROP FUNCTION IF EXISTS public.enforce_active_user()        CASCADE;
DROP FUNCTION IF EXISTS public.track_login(uuid)            CASCADE;
DROP FUNCTION IF EXISTS public.current_company_id()         CASCADE;
DROP FUNCTION IF EXISTS public.current_user_role()          CASCADE;
DROP FUNCTION IF EXISTS public.has_role(public.app_role)    CASCADE;

-- Drop System 1 enum types (only if no remaining tables reference them)
DROP TYPE IF EXISTS public.app_role     CASCADE;
DROP TYPE IF EXISTS public.star_tx_type CASCADE;
-- Keep: redemption_status, notification_type, recognition_visibility, mood_value
-- (still used by System 2 tables)
