/**
 * Auth middleware for Edge Functions.
 *
 * Replaces the old JWT-based withAuth middleware with withEmployeeAuth,
 * which validates the x-session-token header against employee_active_sessions.
 *
 * Auth model:
 *   - No Supabase Auth JWT — employees authenticate via employee_code + password
 *   - Sessions are stored in employee_active_sessions (migration 032)
 *   - x-session-token header carries the session UUID on every request
 *   - validate_session() RPC resolves the employee and checks expiry
 *   - All DB writes use adminClient (service_role) — RLS is enforced at the
 *     DB layer via current_employee_hotel() / current_employee_id()
 */

import { createAdminClient } from "./supabase-client.ts";
import type { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";

// ─── Types ───────────────────────────────────────────────────────────────────

export interface AuthenticatedEmployee {
  employee_id:   string;
  full_name:     string;
  employee_code: string;
  hotel:         string;
}

export interface EmployeeAuthContext {
  employee:    AuthenticatedEmployee;
  adminClient: SupabaseClient;  // service_role — bypasses RLS for server-side ops
}

// ─── CORS ─────────────────────────────────────────────────────────────────────
//
// x-session-token must be listed here so the browser's preflight check passes.

const CORS_HEADERS: Record<string, string> = {
  "Access-Control-Allow-Origin":  "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type, x-session-token",
  "Access-Control-Allow-Methods": "GET, POST, PUT, PATCH, DELETE, OPTIONS",
};

function corsResponse(): Response {
  return new Response(null, { status: 204, headers: CORS_HEADERS });
}

// ─── Response helpers ─────────────────────────────────────────────────────────

export function errorResponse(message: string, status = 400): Response {
  return new Response(
    JSON.stringify({ error: message }),
    { status, headers: { ...CORS_HEADERS, "Content-Type": "application/json" } }
  );
}

export function jsonResponse(data: unknown, status = 200): Response {
  return new Response(
    JSON.stringify(data),
    { status, headers: { ...CORS_HEADERS, "Content-Type": "application/json" } }
  );
}

// ─── Core middleware ──────────────────────────────────────────────────────────

/**
 * Wraps an Edge Function handler with employee session authentication,
 * CORS handling, and top-level error catching.
 *
 * Usage:
 *   Deno.serve(withEmployeeAuth(async (req, ctx) => {
 *     const { employee, adminClient } = ctx;
 *     // ... business logic ...
 *     return jsonResponse({ ok: true });
 *   }));
 *
 * Rejects with 401 when:
 *   - x-session-token header is missing
 *   - Token is not found in employee_active_sessions
 *   - Token has expired
 *   - The bound employee account is inactive
 */
export function withEmployeeAuth(
  handler: (req: Request, ctx: EmployeeAuthContext) => Promise<Response>
): (req: Request) => Promise<Response> {

  return async (req: Request): Promise<Response> => {
    // ── CORS preflight ────────────────────────────────────────────────────
    if (req.method === "OPTIONS") {
      return corsResponse();
    }

    try {
      // ── 1. Extract session token ──────────────────────────────────────
      const sessionToken = req.headers.get("x-session-token");

      if (!sessionToken) {
        return errorResponse("Missing x-session-token header", 401);
      }

      // ── 2. Validate UUID format (fast fail before DB round-trip) ──────
      const uuidPattern =
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
      if (!uuidPattern.test(sessionToken)) {
        return errorResponse("Invalid session token format", 401);
      }

      // ── 3. Validate against employee_active_sessions ──────────────────
      //    validate_session() checks expiry and employee.status = 'active'.
      //    It JOINs to employees so hotel is resolved in one query.
      const adminClient = createAdminClient();

      const { data: session, error: rpcError } = await adminClient.rpc(
        "validate_session",
        { p_session_token: sessionToken }
      );

      if (rpcError) {
        console.error("validate_session RPC error:", rpcError);
        return errorResponse("Authentication failed", 500);
      }

      if (!session?.ok) {
        return errorResponse(session?.error ?? "Session expired or invalid", 401);
      }

      // ── 4. Build employee context and invoke handler ──────────────────
      const employee: AuthenticatedEmployee = {
        employee_id:   session.employee_id,
        full_name:     session.full_name,
        employee_code: session.employee_code,
        hotel:         session.hotel,
      };

      return await handler(req, { employee, adminClient });

    } catch (err) {
      console.error("Unhandled error in withEmployeeAuth:", err);
      return errorResponse("Internal server error", 500);
    }
  };
}
