-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 015 — Employee Password Authentication
--
-- Builds on top of migration 014.
--
-- Changes:
--   1. Enable pgcrypto for bcrypt hashing
--   2. Add password_hash column (nullable — NULL means first login not done)
--   3. Drop global UNIQUE on employee_code → replace with UNIQUE per hotel
--   4. Update hotel CHECK constraint (Safari Bush Lodge → Chobe Bush Lodge)
--   5. Add three RPC functions:
--        verify_employee_identity   — first-time: full_name + code + hotel
--        set_employee_password      — first-time: store bcrypt hash
--        login_employee             — returning:  code + hotel + password
-- ─────────────────────────────────────────────────────────────────────────────

-- ─── 1. pgcrypto (bcrypt support) ────────────────────────────────────────────

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ─── 2. Add password_hash column ─────────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS password_hash text;

COMMENT ON COLUMN public.employees.password_hash IS
  'bcrypt hash of the employee password. NULL = password not yet set (first login pending).';

-- ─── 3. Unique constraint: employee_code unique PER hotel ────────────────────

-- Drop the old global unique constraint from migration 014
ALTER TABLE public.employees
  DROP CONSTRAINT IF EXISTS employees_employee_code_key;

-- New composite unique: same code can exist at different hotels
ALTER TABLE public.employees
  ADD CONSTRAINT employees_employee_code_hotel_unique
  UNIQUE (employee_code, hotel);

-- ─── 4. Update hotel CHECK constraint ────────────────────────────────────────

ALTER TABLE public.employees
  DROP CONSTRAINT IF EXISTS chk_hotel;

ALTER TABLE public.employees
  ADD CONSTRAINT chk_hotel CHECK (
    hotel IN (
      'Indaba Hotel',
      'Indaba Lodge Richards Bay',
      'Indaba Lodge Gaborone',
      'Chobe Safari Lodge',
      'Chobe Bush Lodge',
      'Nata Lodge',
      'African Procurement Agencies'
    )
  );

-- ─── 5. Indexes ───────────────────────────────────────────────────────────────

-- Already created in 014 — ensure they exist
CREATE INDEX IF NOT EXISTS idx_employees_employee_code
  ON public.employees (employee_code);

CREATE INDEX IF NOT EXISTS idx_employees_hotel
  ON public.employees (hotel);

-- Composite index supporting the returning login query
CREATE INDEX IF NOT EXISTS idx_employees_login
  ON public.employees (employee_code, hotel, status)
  WHERE status = 'active';

-- ─── 6. RPC: verify_employee_identity ────────────────────────────────────────
--
-- FIRST LOGIN — Step 1
-- Employee enters: full_name, employee_code, hotel
--
-- Returns:
--   { found: true,  needs_password: true,  id, full_name, hotel }  → no password set yet
--   { found: true,  needs_password: false, id, full_name, hotel }  → password already set
--   { found: false, error: '...' }                                  → no match

CREATE OR REPLACE FUNCTION public.verify_employee_identity(
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
  v_id            uuid;
  v_full_name     text;
  v_hotel         text;
  v_password_hash text;
BEGIN
  SELECT id, full_name, hotel, password_hash
  INTO   v_id, v_full_name, v_hotel, v_password_hash
  FROM   public.employees
  WHERE  employee_code        = TRIM(p_employee_code)
    AND  LOWER(full_name)     = LOWER(TRIM(p_full_name))
    AND  hotel                = TRIM(p_hotel)
    AND  status               = 'active'
  LIMIT 1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'found', false,
      'error', 'Employee not recognised. Check your name, code, and hotel.'
    );
  END IF;

  RETURN jsonb_build_object(
    'found',          true,
    'needs_password', (v_password_hash IS NULL),
    'id',             v_id,
    'full_name',      v_full_name,
    'hotel',          v_hotel
  );
END;
$func$;

GRANT EXECUTE ON FUNCTION public.verify_employee_identity(text, text, text) TO anon;
GRANT EXECUTE ON FUNCTION public.verify_employee_identity(text, text, text) TO authenticated;

-- ─── 7. RPC: set_employee_password ───────────────────────────────────────────
--
-- FIRST LOGIN — Step 2
-- Called only when password_hash IS NULL (first time).
-- Hashes the password with bcrypt (cost factor 10) and stores it.
--
-- Returns:
--   { success: true }
--   { success: false, error: '...' }

CREATE OR REPLACE FUNCTION public.set_employee_password(
  p_employee_id uuid,
  p_new_password text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_updated int;
BEGIN
  -- Only update rows where password has never been set
  WITH updated AS (
    UPDATE public.employees
    SET    password_hash = crypt(p_new_password, gen_salt('bf', 10))
    WHERE  id            = p_employee_id
      AND  password_hash IS NULL
      AND  status        = 'active'
    RETURNING id
  )
  SELECT COUNT(*) INTO v_updated FROM updated;

  IF v_updated = 0 THEN
    RETURN jsonb_build_object(
      'success', false,
      'error',   'Password could not be set. The account may already have a password or is inactive.'
    );
  END IF;

  RETURN jsonb_build_object('success', true);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.set_employee_password(uuid, text) TO anon;
GRANT EXECUTE ON FUNCTION public.set_employee_password(uuid, text) TO authenticated;

-- ─── 8. RPC: login_employee ───────────────────────────────────────────────────
--
-- RETURNING LOGIN
-- Employee enters: employee_code, hotel, password
-- pgcrypto verifies the bcrypt hash in a single query.
--
-- Returns:
--   { found: true,  id, full_name, hotel }
--   { found: false, error: '...' }

CREATE OR REPLACE FUNCTION public.login_employee(
  p_employee_code text,
  p_hotel         text,
  p_password      text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_id        uuid;
  v_full_name text;
  v_hotel     text;
BEGIN
  SELECT id, full_name, hotel
  INTO   v_id, v_full_name, v_hotel
  FROM   public.employees
  WHERE  employee_code  = TRIM(p_employee_code)
    AND  hotel          = TRIM(p_hotel)
    AND  status         = 'active'
    AND  password_hash  IS NOT NULL
    AND  password_hash  = crypt(p_password, password_hash)
  LIMIT 1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'found', false,
      'error', 'Invalid employee code or password.'
    );
  END IF;

  RETURN jsonb_build_object(
    'found',      true,
    'id',         v_id,
    'full_name',  v_full_name,
    'hotel',      v_hotel
  );
END;
$func$;

GRANT EXECUTE ON FUNCTION public.login_employee(text, text, text) TO anon;
GRANT EXECUTE ON FUNCTION public.login_employee(text, text, text) TO authenticated;

-- ─── 9. Comments ──────────────────────────────────────────────────────────────

COMMENT ON FUNCTION public.verify_employee_identity IS
  'Step 1 of first login. Verifies full_name + employee_code + hotel.
   Returns needs_password:true if the employee has not yet set a password.';

COMMENT ON FUNCTION public.set_employee_password IS
  'Step 2 of first login. Sets a bcrypt password for an employee whose
   password_hash is currently NULL. Cannot overwrite an existing password.';

COMMENT ON FUNCTION public.login_employee IS
  'Returning login. Verifies employee_code + hotel + password via bcrypt.
   Never returns the hash to the client.';
