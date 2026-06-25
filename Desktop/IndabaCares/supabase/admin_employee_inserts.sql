-- ─────────────────────────────────────────────────────────────────────────────
-- Admin Employee Upload Examples
--
-- Run these in the Supabase SQL Editor (uses service_role → bypasses RLS).
-- All inserts leave password_hash = NULL.
-- The employee sets their password on first login via EmployeeAuthScreen.
-- ─────────────────────────────────────────────────────────────────────────────


-- ═══════════════════════════════════════════════════════════════════════════════
-- SECTION 1 — COMPLETE TABLE SCHEMA (reference)
-- ═══════════════════════════════════════════════════════════════════════════════

/*
CREATE TABLE public.employees (
  id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  full_name     text        NOT NULL,
  employee_code text        NOT NULL,
  hotel         text        NOT NULL,
  department    text,                          -- optional
  position      text,                          -- optional
  status        text        NOT NULL DEFAULT 'active',
  password_hash text,                          -- NULL until first login
  created_at    timestamptz NOT NULL DEFAULT now(),

  CONSTRAINT employees_employee_code_hotel_unique
    UNIQUE (employee_code, hotel),             -- code unique PER hotel

  CONSTRAINT chk_hotel CHECK (
    hotel IN (
      'Indaba Hotel',
      'Indaba Lodge Richards Bay',
      'Indaba Lodge Gaborone',
      'Chobe Safari Lodge',
      'Chobe Bush Lodge',
      'Nata Lodge',
      'African Procurement Agencies'
    )
  ),

  CONSTRAINT chk_status CHECK (
    status IN ('active', 'inactive', 'suspended')
  )
);
*/


-- ═══════════════════════════════════════════════════════════════════════════════
-- SECTION 2 — METHOD A: Direct INSERT (Supabase SQL Editor / service_role)
-- ═══════════════════════════════════════════════════════════════════════════════
--
-- Fastest path for administrators. Run directly in the Supabase dashboard.
-- password_hash is omitted — it defaults to NULL automatically.

-- ── Indaba Hotel ──────────────────────────────────────────────────────────────

INSERT INTO public.employees (full_name, employee_code, hotel, department, position)
VALUES
  ('Sarah Mokoena',    'IH001', 'Indaba Hotel', 'Front Office',   'Receptionist'),
  ('James Dlamini',    'IH002', 'Indaba Hotel', 'Food & Beverage','Waiter'),
  ('Priya Naidoo',     'IH003', 'Indaba Hotel', 'Housekeeping',   'Room Attendant'),
  ('Thomas Khumalo',   'IH004', 'Indaba Hotel', 'Maintenance',    'Technician'),
  ('Lerato Sithole',   'IH005', 'Indaba Hotel', 'Management',     'Duty Manager');

-- ── Indaba Lodge Richards Bay ─────────────────────────────────────────────────

INSERT INTO public.employees (full_name, employee_code, hotel, department, position)
VALUES
  ('Zanele Mbatha',    'RB001', 'Indaba Lodge Richards Bay', 'Front Office',   'Guest Relations'),
  ('Brian Zulu',       'RB002', 'Indaba Lodge Richards Bay', 'Food & Beverage','Chef de Partie'),
  ('Nomsa Dube',       'RB003', 'Indaba Lodge Richards Bay', 'Housekeeping',   'Supervisor');

-- ── Indaba Lodge Gaborone ─────────────────────────────────────────────────────

INSERT INTO public.employees (full_name, employee_code, hotel, department, position)
VALUES
  ('Kabo Molefe',      'GB001', 'Indaba Lodge Gaborone', 'Front Office',   'Night Auditor'),
  ('Mpho Setlhare',    'GB002', 'Indaba Lodge Gaborone', 'Housekeeping',   'Room Attendant'),
  ('Tiro Kepaletswe',  'GB003', 'Indaba Lodge Gaborone', 'Management',     'General Manager');

-- ── Chobe Safari Lodge ────────────────────────────────────────────────────────

INSERT INTO public.employees (full_name, employee_code, hotel, department, position)
VALUES
  ('Goitseone Moeti',  'CS001', 'Chobe Safari Lodge', 'Guiding',        'Safari Guide'),
  ('Oarabile Tau',     'CS002', 'Chobe Safari Lodge', 'Food & Beverage','Bartender'),
  ('Neo Seretse',      'CS003', 'Chobe Safari Lodge', 'Housekeeping',   'Room Attendant');

-- ── Chobe Bush Lodge ──────────────────────────────────────────────────────────

INSERT INTO public.employees (full_name, employee_code, hotel, department, position)
VALUES
  ('Lesedi Mothibi',   'CB001', 'Chobe Bush Lodge', 'Guiding',        'Head Guide'),
  ('Kefilwe Gaolathe', 'CB002', 'Chobe Bush Lodge', 'Front Office',   'Receptionist'),
  ('Thabo Rankhumise', 'CB003', 'Chobe Bush Lodge', 'Maintenance',    'Groundsman');

-- ── Nata Lodge ────────────────────────────────────────────────────────────────

INSERT INTO public.employees (full_name, employee_code, hotel, department, position)
VALUES
  ('Masego Kgosi',     'NL001', 'Nata Lodge', 'Front Office',   'Receptionist'),
  ('Boineelo Mosu',    'NL002', 'Nata Lodge', 'Food & Beverage','Cook'),
  ('Ditshebo Gaone',   'NL003', 'Nata Lodge', 'Housekeeping',   'Room Attendant');

-- ── African Procurement Agencies ─────────────────────────────────────────────

INSERT INTO public.employees (full_name, employee_code, hotel, department, position)
VALUES
  ('Ayanda Mthembu',   'AP001', 'African Procurement Agencies', 'Procurement', 'Buyer'),
  ('Sibusiso Nkosi',   'AP002', 'African Procurement Agencies', 'Logistics',   'Coordinator'),
  ('Lungelo Hadebe',   'AP003', 'African Procurement Agencies', 'Finance',     'Accountant');


-- ═══════════════════════════════════════════════════════════════════════════════
-- SECTION 3 — METHOD B: Via admin_insert_employee() RPC
-- ═══════════════════════════════════════════════════════════════════════════════
--
-- Safer for programmatic inserts from an admin web app.
-- Returns { ok, id, message } or { ok: false, error }.
-- Can be called with the authenticated role (admin JWT) or service_role.

SELECT public.admin_insert_employee(
  'Sarah Mokoena',   -- full_name
  'IH001',           -- employee_code  (auto-uppercased inside RPC)
  'Indaba Hotel',    -- hotel
  'Front Office',    -- department  (optional)
  'Receptionist'     -- position    (optional)
);

-- Without optional fields:
SELECT public.admin_insert_employee(
  'James Dlamini',
  'IH002',
  'Indaba Hotel'
);


-- ═══════════════════════════════════════════════════════════════════════════════
-- SECTION 4 — METHOD C: admin_bulk_insert_employees() — JSON batch upload
-- ═══════════════════════════════════════════════════════════════════════════════
--
-- Pass the entire roster as a JSON array in one call.
-- Duplicates (same employee_code + hotel) are silently skipped.
-- Returns { inserted, skipped, errors[] }.

SELECT public.admin_bulk_insert_employees(
  '[
    {
      "full_name":     "Sarah Mokoena",
      "employee_code": "IH001",
      "hotel":         "Indaba Hotel",
      "department":    "Front Office",
      "position":      "Receptionist"
    },
    {
      "full_name":     "James Dlamini",
      "employee_code": "IH002",
      "hotel":         "Indaba Hotel",
      "department":    "Food & Beverage",
      "position":      "Waiter"
    },
    {
      "full_name":     "Kabo Molefe",
      "employee_code": "GB001",
      "hotel":         "Indaba Lodge Gaborone",
      "department":    "Front Office",
      "position":      "Night Auditor"
    }
  ]'::jsonb
);

-- Expected response:
-- { "inserted": 3, "skipped": 0, "errors": [] }


-- ═══════════════════════════════════════════════════════════════════════════════
-- SECTION 5 — ADMIN MANAGEMENT QUERIES
-- ═══════════════════════════════════════════════════════════════════════════════

-- View all employees (password_hash excluded)
SELECT * FROM public.employees_admin_view
ORDER BY hotel, employee_code;

-- View employees who have NOT yet set a password
SELECT id, full_name, employee_code, hotel, created_at
FROM   public.employees_admin_view
WHERE  auth_status = 'pending_first_login'
ORDER BY hotel, employee_code;

-- View employees who HAVE set a password
SELECT id, full_name, employee_code, hotel, auth_status
FROM   public.employees_admin_view
WHERE  auth_status = 'password_set'
ORDER BY hotel;

-- Count per hotel
SELECT hotel, COUNT(*) AS total,
       SUM(CASE WHEN auth_status = 'pending_first_login' THEN 1 ELSE 0 END) AS pending,
       SUM(CASE WHEN auth_status = 'password_set'        THEN 1 ELSE 0 END) AS active_logins
FROM   public.employees_admin_view
GROUP  BY hotel
ORDER  BY hotel;

-- Deactivate an employee (they can no longer log in)
SELECT public.admin_deactivate_employee('uuid-goes-here');

-- Reactivate
SELECT public.admin_reactivate_employee('uuid-goes-here');

-- Reset password (employee must set a new one on next login)
SELECT public.admin_reset_employee_password('uuid-goes-here');

-- Hard update (direct SQL — service_role only)
UPDATE public.employees
SET    full_name = 'Sarah M. Mokoena',
       position  = 'Senior Receptionist'
WHERE  employee_code = 'IH001'
  AND  hotel         = 'Indaba Hotel';

-- Soft delete (preferred over DELETE)
UPDATE public.employees
SET    status = 'inactive'
WHERE  employee_code = 'IH001'
  AND  hotel         = 'Indaba Hotel';


-- ═══════════════════════════════════════════════════════════════════════════════
-- SECTION 6 — CONSTRAINT REFERENCE
-- ═══════════════════════════════════════════════════════════════════════════════

/*
  UNIQUE CONSTRAINT
  ─────────────────
  UNIQUE (employee_code, hotel)
  → Same code may exist at different hotels (e.g. IH001 and RB001 can both be 'IH001'
    if they are at different hotels — but IH001 cannot appear twice at Indaba Hotel).

  ALLOWED HOTELS (chk_hotel)
  ──────────────────────────
  'Indaba Hotel'
  'Indaba Lodge Richards Bay'
  'Indaba Lodge Gaborone'
  'Chobe Safari Lodge'
  'Chobe Bush Lodge'
  'Nata Lodge'
  'African Procurement Agencies'

  ALLOWED STATUS VALUES (chk_status)
  ───────────────────────────────────
  'active'     — can log in
  'inactive'   — cannot log in (soft delete / leavers)
  'suspended'  — cannot log in (disciplinary / investigation)

  PASSWORD LIFECYCLE
  ──────────────────
  Admin inserts row  →  password_hash = NULL
  Employee first login  →  enters password  →  bcrypt hash stored
  Admin reset  →  password_hash = NULL  →  employee sets new password on next login
*/
