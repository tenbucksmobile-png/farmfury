-- ============================================================================
-- IndabaCares — Migration 012: Employee Codes & Onboarding Flow
--
-- Changes:
--   1. Drop NOT NULL on profiles.company_id to allow unlinked users
--   2. Update handle_new_user trigger to support null company_id
--   3. Update current_company_id() to handle null gracefully
--   4. Create employee_codes table
--   5. Indexes and RLS policies for employee_codes
-- ============================================================================

-- --------------------------------------------------------------------------
-- 1. Allow profiles to exist without a company (unlinked state)
-- --------------------------------------------------------------------------
alter table public.profiles
  alter column company_id drop not null;

comment on column public.profiles.company_id is
  'Hotel this employee belongs to. NULL means the user has not yet '
  'claimed their employee code. All RLS policies treat NULL as no-access '
  'until company_id is set and the JWT is refreshed.';

-- --------------------------------------------------------------------------
-- 2. Update handle_new_user to support creation without company_id
--
-- Previous versions required either an invite_token or a company_id.
-- With the employee code flow, users are created by Supabase admin
-- (or via the auth-signup Edge Function) without company context.
-- company_id will be null until the employee claims their code.
-- --------------------------------------------------------------------------
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
  _company_id    uuid;
  _full_name     text;
  _role          public.app_role;
  _department_id uuid;
  _manager_id    uuid;
  _invite        public.invite_tokens%rowtype;
  _invite_token  text;
begin
  _invite_token := new.raw_user_meta_data ->> 'invite_token';
  _full_name    := coalesce(
    new.raw_user_meta_data ->> 'full_name',
    split_part(new.email, '@', 1)
  );

  -- Path A: Invite-based signup (admin-to-admin or pre-linked onboarding)
  if _invite_token is not null then
    select * into _invite
    from public.invite_tokens
    where token      = _invite_token
      and email      = new.email
      and claimed_at is null
      and expires_at > now();

    if not found then
      raise exception 'Invalid or expired invite token'
        using errcode = 'P0003';
    end if;

    _company_id    := _invite.company_id;
    _role          := _invite.role;
    _department_id := _invite.department_id;
    _manager_id    := _invite.manager_id;

    update public.invite_tokens
    set claimed_at = now()
    where id = _invite.id;

  -- Path B: Employee code flow — company_id is null, set after code claim
  else
    _company_id := (new.raw_user_meta_data ->> 'company_id')::uuid;
    _role       := coalesce(
      (new.raw_user_meta_data ->> 'role')::public.app_role,
      'employee'
    );
    -- company_id may legitimately be null here — employee will claim code in-app
  end if;

  -- Create profile (company_id may be null for employee code flow)
  insert into public.profiles (
    id, company_id, email, full_name, role, department_id, manager_id
  ) values (
    new.id, _company_id, new.email, _full_name, _role, _department_id, _manager_id
  );

  -- Write claims to app_metadata only if company_id is known
  if _company_id is not null then
    update auth.users
    set raw_app_meta_data = coalesce(raw_app_meta_data, '{}'::jsonb)
      || jsonb_build_object('company_id', _company_id::text)
      || jsonb_build_object('role', _role::text)
    where id = new.id;
  end if;

  return new;
end;
$$;

-- --------------------------------------------------------------------------
-- 3. Update current_company_id() to handle unlinked users safely
--
-- Previously returned a zero UUID as sentinel. Now returns NULL explicitly
-- so that all RLS policies (company_id = current_company_id()) evaluate
-- to NULL = NULL = false, granting no access to unlinked users.
-- --------------------------------------------------------------------------
create or replace function public.current_company_id()
returns uuid
language sql
stable
as $$
  select (auth.jwt() -> 'app_metadata' ->> 'company_id')::uuid;
$$;

comment on function public.current_company_id() is
  'Reads company_id from the JWT app_metadata. Returns NULL if the user '
  'has not yet claimed an employee code. RLS policies that use '
  'company_id = current_company_id() will return no rows for unlinked users '
  'because NULL = NULL evaluates to false in SQL.';

-- --------------------------------------------------------------------------
-- 4. Employee codes table
-- --------------------------------------------------------------------------
create table public.employee_codes (
  id                 uuid        primary key default gen_random_uuid(),
  code               text        not null unique,
  company_id         uuid        not null references public.companies(id) on delete cascade,
  assigned_to_email  text        not null,
  claimed_by         uuid        references auth.users(id) on delete set null,
  claimed_at         timestamptz,
  expires_at         timestamptz,           -- null = never expires
  is_active          boolean     not null default true,
  created_by         uuid        references public.profiles(id) on delete set null,
  created_at         timestamptz not null default now(),

  -- A code can only be claimed once
  constraint chk_claim_consistency check (
    (claimed_by is null and claimed_at is null)
    or
    (claimed_by is not null and claimed_at is not null)
  ),

  -- Code format: uppercase alphanumeric only
  constraint chk_code_format check (code ~ '^[A-Z0-9]{4,16}$')
);

comment on table public.employee_codes is
  'Single-use codes created by admins. Each code maps an employee email '
  'to a specific hotel. The employee enters this code in the app after '
  'logging in to permanently link their profile to the hotel.';

comment on column public.employee_codes.code is
  'Uppercase alphanumeric code between 4 and 16 characters. Must be unique.';

comment on column public.employee_codes.assigned_to_email is
  'The email address this code was created for. Validated at claim time '
  'against auth.users.email to prevent code sharing.';

comment on column public.employee_codes.claimed_by is
  'auth.users.id of the employee who claimed this code. Null until claimed.';

comment on column public.employee_codes.expires_at is
  'Optional expiry. If null, the code never expires. If set, the claim '
  'Edge Function will reject the code after this timestamp.';

comment on column public.employee_codes.is_active is
  'Admins can revoke a code by setting this to false before it is claimed.';

-- --------------------------------------------------------------------------
-- 5. Indexes
-- --------------------------------------------------------------------------

-- Primary lookup path: validate a code at claim time
create index idx_employee_codes_code
  on public.employee_codes(code)
  where is_active = true and claimed_at is null;

-- Admin management: list codes by company
create index idx_employee_codes_company
  on public.employee_codes(company_id, created_at desc);

-- Find unclaimed codes for a specific email
create index idx_employee_codes_email
  on public.employee_codes(assigned_to_email)
  where is_active = true and claimed_at is null;

-- --------------------------------------------------------------------------
-- 6. Row Level Security for employee_codes
-- --------------------------------------------------------------------------
alter table public.employee_codes enable row level security;

-- Admins can read all codes for their company (claimed or not)
create policy "employee_codes_select_admin"
  on public.employee_codes for select
  to authenticated
  using (
    company_id = public.current_company_id()
    and public.has_role('admin')
  );

-- INSERT and UPDATE are performed exclusively by the claim-employee-code
-- Edge Function using the service_role key, which bypasses RLS.
-- No client-side insert or update is permitted.
