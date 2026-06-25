-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 048 — Add department column to employees table
--
-- The employees table was created without the department column in the live
-- database.  This migration adds it as a nullable text column and adds an
-- index for filtering/reporting by department within a hotel.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS department text;

-- Index for filtering employees by department (e.g. admin views, analytics)
CREATE INDEX IF NOT EXISTS idx_employees_department
  ON public.employees (hotel, department)
  WHERE department IS NOT NULL;

COMMENT ON COLUMN public.employees.department IS
  'Department the employee belongs to (e.g. Front Office, F&B, Housekeeping).
   Populated via CSV upload through admin_bulk_insert_employees.';
