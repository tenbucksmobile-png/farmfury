-- =============================================================================
-- Migration 030 — Schema Cleanup: Remove System 1, Enforce Final employees Schema
--
-- Drops all Supabase Auth (System 1) tables, functions, and types.
-- Ensures public.employees is the sole employee identity table.
-- Replaces UNIQUE CONSTRAINT on employees(employee_code, hotel) with an index.
-- Removes stale columns (department, position) not in the final schema.
--
-- Safe to re-run: all drops are IF EXISTS; all creates are IF NOT EXISTS.
-- =============================================================================


-- ─── 1. Drop System 1 tables ─────────────────────────────────────────────────
-- Order: children before parents (FK dependencies).

-- Ledgers that reference profiles / companies
DROP TABLE IF EXISTS public.star_transactions  CASCADE;  -- references profiles.user_id
DROP TABLE IF EXISTS public.point_transactions CASCADE;  -- references profiles.user_id

-- Onboarding / invite tables
DROP TABLE IF EXISTS public.invite_tokens      CASCADE;
DROP TABLE IF EXISTS public.employee_codes     CASCADE;

-- Leaderboard: references profiles.user_id (broken without profiles)
DROP TABLE IF EXISTS public.leaderboard_cache  CASCADE;

-- Core System 1 identity tables (drop profiles before departments/companies
-- because profiles has FK → departments and FK → companies)
DROP TABLE IF EXISTS public.profiles           CASCADE;
DROP TABLE IF EXISTS public.departments        CASCADE;
DROP TABLE IF EXISTS public.companies          CASCADE;


-- ─── 2. Drop System 1 trigger functions ──────────────────────────────────────

-- Fired on auth.users INSERT — creates a profiles row
DROP FUNCTION IF EXISTS public.handle_new_user()         CASCADE;

-- Fired on profiles.role UPDATE — syncs role to JWT app_metadata
DROP FUNCTION IF EXISTS public.sync_role_to_jwt()        CASCADE;

-- Fired on auth.users — blocked inactive accounts from logging in
DROP FUNCTION IF EXISTS public.enforce_active_user()     CASCADE;

-- Called on login to track streak (referenced profiles / auth.users)
DROP FUNCTION IF EXISTS public.track_login(uuid)         CASCADE;

-- JWT-claim helpers (return NULL / 'employee' fallback now that there is no JWT)
DROP FUNCTION IF EXISTS public.current_company_id()      CASCADE;
DROP FUNCTION IF EXISTS public.current_user_role()       CASCADE;
DROP FUNCTION IF EXISTS public.has_role(public.app_role) CASCADE;


-- ─── 3. Drop System 1 enum types ─────────────────────────────────────────────
-- CASCADE removes any columns / functions that reference these types.
-- All tables that used them have already been dropped above.

DROP TYPE IF EXISTS public.app_role      CASCADE;  -- 'employee','manager','admin','super_admin'
DROP TYPE IF EXISTS public.star_tx_type  CASCADE;  -- 'receive','boost_bonus','redeem','refund','adjust'
DROP TYPE IF EXISTS public.point_tx_type CASCADE;  -- 'give','receive','react','comment', ...

-- Retained types (still used by System 2 tables):
--   redemption_status     — used by public.redemptions
--   notification_type     — used by public.notifications
--   recognition_visibility — used by public.recognitions
--   mood_value            — used by public.mood_entries


-- ─── 4. Clean up public.employees ────────────────────────────────────────────
--
-- Final schema:
--   id            uuid  PK
--   full_name     text  NOT NULL
--   employee_code text  NOT NULL
--   hotel         text  NOT NULL
--   password_hash text  (NULL = first-login not yet completed)
--   status        text  DEFAULT 'active'
--   created_at    timestamptz DEFAULT now()
--
-- NOTE: points_balance (added in migration 019) is retained — it is required
-- by the award_recognition_points() trigger and the redeem_reward() RPC.

-- 4a. Drop employees_admin_view — it depends on the department and position
--     columns we are about to remove.  The view is recreated below without them.
DROP VIEW IF EXISTS public.employees_admin_view;

-- 4b. Remove columns not in the final schema
ALTER TABLE public.employees
  DROP COLUMN IF EXISTS department,
  DROP COLUMN IF EXISTS position;

-- 4c. Drop the old hotel CHECK constraint (was on an inline list of hotel names;
--     validation now lives in is_valid_hotel() used by employee_active_sessions)
ALTER TABLE public.employees
  DROP CONSTRAINT IF EXISTS chk_hotel;

-- 4d. Drop the old status CHECK constraint
ALTER TABLE public.employees
  DROP CONSTRAINT IF EXISTS chk_status;

-- 4e. Drop any remaining global UNIQUE on employee_code alone (from migration 014)
ALTER TABLE public.employees
  DROP CONSTRAINT IF EXISTS employees_employee_code_key;

-- 4f. Drop the CONSTRAINT-based composite unique (from migration 015)
--     It will be replaced by the index below.
ALTER TABLE public.employees
  DROP CONSTRAINT IF EXISTS employees_employee_code_hotel_unique;


-- ─── 5. Final unique index on employees(employee_code, hotel) ────────────────
--
-- The same employee code can exist at different hotels.
-- The same code cannot appear twice within the same hotel.

CREATE UNIQUE INDEX IF NOT EXISTS employee_code_hotel_idx
  ON public.employees (employee_code, hotel);


-- ─── 6. Update table comment ──────────────────────────────────────────────────

COMMENT ON TABLE public.employees IS
  'Sole employee identity table. '
  'Employees are pre-loaded by administrators. '
  'Authentication: employee_code + hotel + bcrypt password (password_hash). '
  'points_balance is a denormalized running total maintained by DB triggers.';


-- ─── 7. Recreate employees_admin_view without dropped columns ─────────────────
--
-- Migration 016 defined this view including department and position.
-- Those columns are removed above, so the view is rebuilt here with
-- only the columns that remain in the final schema.

CREATE OR REPLACE VIEW public.employees_admin_view AS
  SELECT
    id,
    full_name,
    employee_code,
    hotel,
    status,
    points_balance,
    CASE
      WHEN password_hash IS NULL THEN 'pending_first_login'
      ELSE                            'password_set'
    END AS auth_status,
    created_at
  FROM public.employees;

COMMENT ON VIEW public.employees_admin_view IS
  'Read-only view of the employees table with password_hash excluded.
   auth_status: pending_first_login = NULL hash; password_set = hash present.';
