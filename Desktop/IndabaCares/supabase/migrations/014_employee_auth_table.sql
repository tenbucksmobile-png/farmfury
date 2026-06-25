-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 014 — Employee Authentication Table
--
-- Employees are pre-loaded by administrators.
-- Authentication: full_name + employee_code + hotel must all match an active row.
--
-- Security model
-- ──────────────
-- The table is RLS-protected. Anonymous (pre-login) users may call the
-- authenticate_employee() RPC function only — they cannot SELECT the table
-- directly. The RPC is SECURITY DEFINER so it runs with elevated privileges,
-- queries the table internally, and returns only the minimum data needed.
-- This prevents row enumeration and partial-match fishing attacks.
-- ─────────────────────────────────────────────────────────────────────────────

-- ─── 1. Table ─────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.employees (
  id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  full_name     text        NOT NULL,
  employee_code text        NOT NULL UNIQUE,
  hotel         text        NOT NULL,
  department    text,
  position      text,
  status        text        NOT NULL DEFAULT 'active',
  created_at    timestamptz NOT NULL DEFAULT now(),

  -- Enforce only known hotel names
  CONSTRAINT chk_hotel CHECK (
    hotel IN (
      'Indaba Hotel',
      'Indaba Lodge Richards Bay',
      'Indaba Lodge Gaborone',
      'Chobe Safari Lodge',
      'Safari Bush Lodge',
      'Nata Lodge',
      'African Procurement Agencies'
    )
  ),

  -- Enforce known status values
  CONSTRAINT chk_status CHECK (
    status IN ('active', 'inactive', 'suspended')
  )
);

-- ─── 2. Indexes ───────────────────────────────────────────────────────────────

-- Fast lookup during authentication
CREATE INDEX IF NOT EXISTS idx_employees_employee_code
  ON public.employees (employee_code);

-- Filter / reporting by hotel
CREATE INDEX IF NOT EXISTS idx_employees_hotel
  ON public.employees (hotel);

-- Composite index — covers the full auth query in one scan
CREATE INDEX IF NOT EXISTS idx_employees_auth
  ON public.employees (employee_code, status)
  WHERE status = 'active';

-- ─── 3. Row Level Security ────────────────────────────────────────────────────

ALTER TABLE public.employees ENABLE ROW LEVEL SECURITY;

-- Administrators (service_role) have full unrestricted access — bypasses RLS.

-- Authenticated users may read their own record only.
CREATE POLICY employees_select_own
  ON public.employees
  FOR SELECT
  TO authenticated
  USING (
    -- Match on employee_code stored in the user's JWT app_metadata
    employee_code = (
      current_setting('request.jwt.claims', true)::jsonb -> 'app_metadata' ->> 'employee_code'
    )
  );

-- Administrators (role = 'admin' or 'super_admin') may read all records.
CREATE POLICY employees_select_admin
  ON public.employees
  FOR SELECT
  TO authenticated
  USING (
    (current_setting('request.jwt.claims', true)::jsonb -> 'app_metadata' ->> 'role')
    IN ('admin', 'super_admin')
  );

-- No direct anon SELECT on the table.
-- Authentication goes through authenticate_employee() RPC instead.

-- ─── 4. Authentication RPC ────────────────────────────────────────────────────
--
-- Called by the React Native app BEFORE any session exists.
-- SECURITY DEFINER lets it bypass RLS and query the table internally.
-- Granted to anon so unauthenticated clients can call it.
--
-- Returns:
--   { found: true,  id, full_name, hotel, department, position }  on success
--   { found: false, error: '...' }                                 on failure

CREATE OR REPLACE FUNCTION public.authenticate_employee(
  p_full_name     text,
  p_employee_code text,
  p_hotel         text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_row public.employees%ROWTYPE;
BEGIN
  SELECT *
  INTO v_row
  FROM public.employees
  WHERE employee_code = TRIM(p_employee_code)
    AND LOWER(full_name) = LOWER(TRIM(p_full_name))
    AND hotel            = TRIM(p_hotel)
    AND status           = 'active'
  LIMIT 1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'found', false,
      'error', 'No active employee record matched. Check your name, code, and hotel.'
    );
  END IF;

  RETURN jsonb_build_object(
    'found',      true,
    'id',         v_row.id,
    'full_name',  v_row.full_name,
    'hotel',      v_row.hotel,
    'department', v_row.department,
    'position',   v_row.position
  );
END;
$func$;

-- Allow unauthenticated (anon) clients to call this function
GRANT EXECUTE ON FUNCTION public.authenticate_employee(text, text, text) TO anon;
GRANT EXECUTE ON FUNCTION public.authenticate_employee(text, text, text) TO authenticated;

-- ─── 5. Comment ───────────────────────────────────────────────────────────────

COMMENT ON TABLE  public.employees IS
  'Pre-loaded employee directory. Authentication uses full_name + employee_code + hotel.';

COMMENT ON FUNCTION public.authenticate_employee IS
  'Verifies employee credentials without exposing the raw table to anon clients.
   Returns found:true with employee info on success, found:false with error on failure.';
