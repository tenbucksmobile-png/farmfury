-- =============================================================================
-- Migration 026 — Fix points_balance guard trigger
--
-- Problem (migration 025):
--   trg_guard_points_balance was defined as BEFORE UPDATE ON employees FOR EACH ROW.
--   This fires on EVERY column update — including the password_hash update inside
--   set_employee_password().  The trigger checks the indabacares.allow_points_update
--   GUC, which is only set by award_recognition_points() and admin_grant_points().
--   set_employee_password() never sets this GUC, so if the outer
--   NEW.points_balance IS DISTINCT FROM OLD.points_balance condition evaluates
--   TRUE in any edge case (e.g. a manually inserted row with unusual state),
--   the trigger raises an exception and set_employee_password() fails with
--   "Could not save password."
--
-- Fix:
--   Replace the trigger with BEFORE UPDATE OF points_balance ON employees.
--   PostgreSQL only fires column-level triggers when the named column appears
--   in the UPDATE's SET clause.  set_employee_password() sets password_hash
--   only — the trigger never fires, and no GUC check is needed.
--   award_recognition_points() and admin_grant_points() set points_balance
--   explicitly — the trigger fires and the GUC check protects against rogue
--   direct updates.
-- =============================================================================

-- Drop the over-broad trigger from migration 025
DROP TRIGGER IF EXISTS trg_guard_points_balance ON public.employees;

-- Recreate scoped to the points_balance column only
CREATE TRIGGER trg_guard_points_balance
  BEFORE UPDATE OF points_balance ON public.employees
  FOR EACH ROW EXECUTE FUNCTION public.guard_points_balance();

COMMENT ON FUNCTION public.guard_points_balance IS
  'Blocks direct UPDATE of employees.points_balance unless the transaction-local
   GUC indabacares.allow_points_update is set to ''true''.
   Only award_recognition_points() and admin_grant_points() set that GUC.
   Trigger fires only when points_balance is explicitly in the SET clause.';
