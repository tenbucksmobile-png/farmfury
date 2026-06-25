/**
 * employee-auth-helpers.ts
 *
 * Authentication functions for the EmployeeAuthScreen.
 *
 * All auth goes through SECURITY DEFINER RPCs, not direct table queries.
 * This is required because the employees table is protected by hotel-level
 * RLS (migration 017): any direct SELECT by an unauthenticated user returns
 * zero rows, making login impossible.  The RPCs bypass RLS by design.
 *
 * Flow:
 *   First time:  first_time_authenticate (atomic — identity + password + session)
 *   Returning:   authenticate_employee
 */

import { supabase } from '@/lib/supabase';

// ─── Types ────────────────────────────────────────────────────────────────────

export type AuthSuccess = {
  ok:            true;
  employee_id:   string;
  full_name:     string;
  employee_code: string;
  hotel:         string;
  department:    string | null;
  token:         string;
};

export type AuthFailure = {
  ok:    false;
  error: string;
};

export type AuthResult = AuthSuccess | AuthFailure;

// ─── firstAuthentication ──────────────────────────────────────────────────────
//
// Called when an employee logs in for the first time.
//
// Uses the atomic first_time_authenticate RPC (migration 027) which does
// identity verification, password hashing, and session creation in one
// DB transaction — eliminating the fragility of the old three-step flow.

export async function firstAuthentication(
  fullName:     string,
  employeeCode: string,
  hotel:        string,
  password:     string,
): Promise<AuthResult> {

  const { data, error } = await supabase.rpc('first_time_authenticate', {
    p_employee_code: employeeCode.trim().toUpperCase(),
    p_hotel:         hotel.trim(),
    p_full_name:     fullName.trim(),
    p_new_password:  password,
  });

  if (error) {
    return { ok: false, error: 'Authentication failed. Please try again.' };
  }

  if (!data?.ok) {
    return { ok: false, error: data?.error ?? 'Authentication failed.' };
  }

  return {
    ok:            true,
    employee_id:   data.id,
    full_name:     data.full_name,
    employee_code: employeeCode.trim().toUpperCase(),
    hotel:         data.hotel,
    department:    data.department ?? null,
    token:         data.token,
  };
}

// ─── returningLogin ───────────────────────────────────────────────────────────
//
// Called for employees who have already set a password.
// authenticate_employee is atomic: it verifies credentials and creates the
// session in a single DB transaction with built-in rate limiting.

export async function returningLogin(
  employeeCode: string,
  hotel:        string,
  password:     string,
): Promise<AuthResult> {
  const { data, error } = await supabase.rpc('authenticate_employee', {
    p_employee_code: employeeCode.trim().toUpperCase(),
    p_hotel:         hotel.trim(),
    p_password:      password,
  });

  if (error) {
    return { ok: false, error: 'Login failed. Please try again.' };
  }

  if (!data?.ok) {
    return { ok: false, error: data?.error ?? 'Invalid employee code or password.' };
  }

  return {
    ok:            true,
    employee_id:   data.id,
    full_name:     data.full_name,
    employee_code: employeeCode.trim().toUpperCase(),
    hotel:         data.hotel,
    department:    data.department ?? null,
    token:         data.token,
  };
}
