/**
 * Shared Supabase client factory for Edge Functions.
 *
 * In the employee-auth model there is no user JWT, so only one client type
 * is needed: the admin (service_role) client that bypasses RLS.
 * RLS is still enforced at the DB layer via current_employee_hotel() /
 * current_employee_id(), which read the x-session-token from request.headers.
 * Edge Functions perform all mutations through the adminClient — the session
 * token is already validated by withEmployeeAuth before the handler runs.
 */

import { createClient, SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";

const SUPABASE_URL              = Deno.env.get("SUPABASE_URL")!;
const SUPABASE_SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

/**
 * Creates a Supabase client with service_role privileges.
 * Bypasses RLS — use only in Edge Functions for server-side mutations.
 */
export function createAdminClient(): SupabaseClient {
  return createClient(SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY, {
    auth: {
      autoRefreshToken: false,
      persistSession:   false,
    },
  });
}

/**
 * Structured error with an HTTP status code.
 * Retained for use in middleware and handler error paths.
 */
export class AuthError extends Error {
  status: number;
  constructor(message: string, status = 401) {
    super(message);
    this.name   = "AuthError";
    this.status = status;
  }
}
