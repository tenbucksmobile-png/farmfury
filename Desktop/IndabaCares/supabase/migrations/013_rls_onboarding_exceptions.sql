-- ============================================================================
-- IndabaCares — Migration 013: RLS Onboarding Exceptions
--
-- Context:
--   Migration 006 established RLS for all tables using the pattern:
--     company_id = public.current_company_id()
--
--   Migration 012 introduced two changes that require RLS updates:
--     1. profiles.company_id is now nullable (unlinked users)
--     2. current_company_id() now returns NULL (not a zero UUID) for
--        unlinked users — meaning all existing company-scoped policies
--        already lock out unlinked users correctly (NULL = NULL → false)
--
--   The only gap is that unlinked users need to be able to:
--     a. Read their own profile (to display their name/email on the
--        employee code entry screen)
--     b. Update their own profile minimally (full_name, avatar) before
--        the company is linked, so the onboarding screen can function
--
--   No other table requires an onboarding exception. An unlinked user
--   must not access recognitions, rewards, leaderboards, mood entries,
--   or skills. The existing NULL-guard in current_company_id() ensures
--   this automatically.
--
-- Tables affected:
--   • profiles  — add self-select and self-update exceptions for unlinked users
--
-- Tables NOT changed (policies in 006 remain correct):
--   • companies, departments, recognitions, recognition_recipients
--   • reactions, comments, rewards, reward_categories, redemptions
--   • point_transactions, star_transactions, skill_categories
--   • skill_indicators, skill_ratings, mood_entries, badges, user_badges
--   • leaderboard_cache, notifications, budget_configs, audit_logs
--   • content_flags (migration 011)
--   • employee_codes (migration 012)
-- ============================================================================

-- --------------------------------------------------------------------------
-- 1. profiles — self-select for unlinked users
--
-- The existing policy "profiles_select_company" only allows reads when
-- company_id = current_company_id(). For unlinked users both sides are
-- NULL so the condition evaluates to false — the user cannot see anything.
--
-- This new policy grants a user SELECT on their own profile row
-- unconditionally, regardless of company_id. This is required so the
-- onboarding screen can display the user's name and email.
-- --------------------------------------------------------------------------
create policy "profiles_select_self"
  on public.profiles for select
  to authenticated
  using (id = auth.uid());

-- --------------------------------------------------------------------------
-- 2. profiles — self-update for unlinked users
--
-- The existing policy "profiles_update_own" (migration 006) includes:
--   company_id = public.current_company_id()
-- This blocks all updates for unlinked users, including the legitimate
-- case of setting a display name or avatar during onboarding.
--
-- This policy allows a user to update only their own row, with no
-- company_id check. The WITH CHECK clause enforces that:
--   • The user cannot change their own role
--   • The user cannot change their company_id themselves
--     (company_id is only written by the claim-employee-code Edge Function
--      via service_role, which bypasses RLS entirely)
--   • The user cannot modify balances or is_active
--
-- Once the user is linked (company_id is set and JWT is refreshed),
-- the existing "profiles_update_own" policy in migration 006 takes over
-- for company-scoped updates. Both policies coexist — Postgres RLS uses
-- OR semantics across multiple permissive policies on the same command.
-- --------------------------------------------------------------------------
create policy "profiles_update_self_unlinked"
  on public.profiles for update
  to authenticated
  using (id = auth.uid())
  with check (
    id = auth.uid()
    -- Prevent self-promotion or protected field changes
    and role           = (select role           from public.profiles where id = auth.uid())
    and is_active      = (select is_active      from public.profiles where id = auth.uid())
    and points_balance = (select points_balance from public.profiles where id = auth.uid())
    and stars_balance  = (select stars_balance  from public.profiles where id = auth.uid())
    and giving_balance = (select giving_balance from public.profiles where id = auth.uid())
    -- company_id must remain unchanged — only the Edge Function sets it
    and company_id is not distinct from (select company_id from public.profiles where id = auth.uid())
  );

-- --------------------------------------------------------------------------
-- 3. companies — allow unlinked users to read a company by slug
--
-- During the employee code claim flow, after the Edge Function validates
-- the code it returns the company name and slug to display the confirmation
-- screen ("You belong to Sandton Indaba. Confirm?").
--
-- This read happens server-side inside the Edge Function (service_role),
-- so no client-side RLS exception is needed here. The Edge Function
-- bypasses RLS and returns only the specific company fields needed for
-- the confirmation display.
--
-- No policy change required on companies.
-- --------------------------------------------------------------------------

-- --------------------------------------------------------------------------
-- 4. Verify enforce_active_user hook is safe for unlinked users
--
-- The enforce_active_user() function (migration 007) checks is_active
-- on the profiles table. For a freshly created unlinked user, is_active
-- defaults to true, so this check will pass correctly.
--
-- No change required.
-- --------------------------------------------------------------------------

-- --------------------------------------------------------------------------
-- Summary of RLS state after migration 013
-- --------------------------------------------------------------------------
--
-- Unlinked user (company_id = NULL in JWT):
--   ✓ Can SELECT own profile row (profiles_select_self)
--   ✓ Can UPDATE own profile (display_name, avatar_url) (profiles_update_self_unlinked)
--   ✗ Cannot SELECT any other profile
--   ✗ Cannot SELECT companies, recognitions, rewards, leaderboard, or any other table
--   ✗ Cannot INSERT or UPDATE any table except own profile fields above
--
-- Linked employee (company_id set in JWT):
--   ✓ profiles_select_self still applies (harmless, just adds own row to result)
--   ✓ profiles_select_company applies (see all colleagues in same hotel)
--   ✓ profiles_update_own (006) and profiles_update_admin (006) apply
--   ✓ All other table policies from migration 006 apply normally
--
-- Admin/manager (linked, elevated role):
--   ✓ All policies from 006 that check has_role() apply
--   ✓ Can read/write within their hotel only
