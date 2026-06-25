/**
 * Edge Function: claim-employee-code
 *
 * Links an authenticated but unlinked user to a hotel by validating
 * their employee code and writing company_id to their profile and JWT.
 *
 * This function intentionally does NOT use the withAuth middleware because
 * withAuth rejects users without company_id in their JWT — which is
 * exactly the state of a user who needs to claim their code.
 *
 * Race condition protection:
 *   The claim is executed as a single atomic UPDATE on employee_codes
 *   with claimed_at IS NULL in the WHERE clause. If two concurrent
 *   requests attempt to claim the same code, only one UPDATE will
 *   affect a row. The second will receive 0 rows and be rejected.
 *
 * Flow:
 *   1. Validate JWT and extract authenticated user (no company_id required)
 *   2. Rate limit by user ID
 *   3. Parse and normalise employee_code input
 *   4. Atomically claim the code (UPDATE with guard clause)
 *   5. Update profiles.company_id
 *   6. Update auth.users app_metadata (company_id + role)
 *   7. Write audit log
 *   8. Return company info for the client to display confirmation
 */

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";
import { createAdminClient } from "../_shared/supabase-client.ts";
import { errorResponse, jsonResponse } from "../_shared/auth-middleware.ts";
import { writeAuditLog } from "../_shared/audit.ts";

// ─── Constants ───────────────────────────────────────────────────────────────

const CORS_HEADERS: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

const RATE_LIMIT_MAX      = 5;   // max claim attempts
const RATE_LIMIT_WINDOW   = 15;  // minutes
const CODE_REGEX          = /^[A-Z0-9]{4,16}$/;

// ─── Types ────────────────────────────────────────────────────────────────────

interface ClaimRequest {
  employee_code: string;
}

interface EmployeeCode {
  id: string;
  code: string;
  company_id: string;
  assigned_to_email: string;
  claimed_by: string | null;
  claimed_at: string | null;
  expires_at: string | null;
  is_active: boolean;
}

interface Company {
  id: string;
  name: string;
  slug: string;
  logo_url: string | null;
  primary_color: string;
}

// ─── Main Handler ─────────────────────────────────────────────────────────────

Deno.serve(async (req: Request): Promise<Response> => {
  // ── CORS preflight ─────────────────────────────────────────────────────────
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS_HEADERS });
  }

  if (req.method !== "POST") {
    return errorResponse("Method not allowed", 405);
  }

  // ── 1. Authenticate — validate JWT without requiring company_id ────────────
  const authHeader = req.headers.get("Authorization");
  if (!authHeader || !authHeader.startsWith("Bearer ")) {
    return errorResponse("Missing or malformed Authorization header", 401);
  }

  const SUPABASE_URL     = Deno.env.get("SUPABASE_URL")!;
  const SUPABASE_ANON_KEY = Deno.env.get("SUPABASE_ANON_KEY")!;

  // Use the anon key + user JWT to resolve the caller identity.
  // This validates the token signature via Supabase Auth without requiring
  // any app_metadata claims (company_id is intentionally absent at this stage).
  const userClient = createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
    global: { headers: { Authorization: authHeader } },
    auth:   { autoRefreshToken: false, persistSession: false },
  });

  const { data: { user: supabaseUser }, error: authError } =
    await userClient.auth.getUser();

  if (authError || !supabaseUser) {
    return errorResponse("Invalid or expired session. Please log in again.", 401);
  }

  // Guard: if the user already has a company_id, they do not need this function.
  const existingCompanyId = supabaseUser.app_metadata?.company_id;
  if (existingCompanyId) {
    return errorResponse(
      "Your account is already linked to a hotel. This action is not permitted.",
      409
    );
  }

  const userId    = supabaseUser.id;
  const userEmail = supabaseUser.email!.toLowerCase().trim();

  const adminClient = createAdminClient();

  // ── 2. Rate limiting — prevent brute-force code guessing ──────────────────
  const { data: withinLimit } = await adminClient.rpc("check_rate_limit", {
    p_identifier:     userId,
    p_action:         "claim_employee_code",
    p_max_attempts:   RATE_LIMIT_MAX,
    p_window_minutes: RATE_LIMIT_WINDOW,
  });

  if (withinLimit === false) {
    return errorResponse(
      `Too many attempts. Please wait ${RATE_LIMIT_WINDOW} minutes before trying again.`,
      429
    );
  }

  await adminClient.rpc("record_rate_limit", {
    p_identifier: userId,
    p_action:     "claim_employee_code",
  });

  // ── 3. Parse and normalise input ───────────────────────────────────────────
  let body: ClaimRequest;
  try {
    body = await req.json();
  } catch {
    return errorResponse("Request body must be valid JSON", 400);
  }

  if (!body.employee_code || typeof body.employee_code !== "string") {
    return errorResponse("employee_code is required", 400);
  }

  const code = body.employee_code.trim().toUpperCase();

  if (!CODE_REGEX.test(code)) {
    return errorResponse(
      "Invalid code format. Codes are 4–16 uppercase letters and numbers.",
      400
    );
  }

  // ── 4. Fetch and validate the employee code ────────────────────────────────
  const { data: employeeCode, error: fetchError } = await adminClient
    .from("employee_codes")
    .select("id, code, company_id, assigned_to_email, claimed_by, claimed_at, expires_at, is_active")
    .eq("code", code)
    .single<EmployeeCode>();

  if (fetchError || !employeeCode) {
    // Return a generic message to avoid leaking whether a code exists
    return errorResponse("Invalid employee code. Please check and try again.", 400);
  }

  // Validate: code must be active
  if (!employeeCode.is_active) {
    return errorResponse(
      "This employee code has been deactivated. Contact your administrator.",
      400
    );
  }

  // Validate: code must not already be claimed
  if (employeeCode.claimed_at !== null || employeeCode.claimed_by !== null) {
    return errorResponse(
      "This employee code has already been used. Contact your administrator.",
      400
    );
  }

  // Validate: code must not be expired
  if (employeeCode.expires_at !== null) {
    const expiry = new Date(employeeCode.expires_at);
    if (expiry < new Date()) {
      return errorResponse(
        "This employee code has expired. Contact your administrator.",
        400
      );
    }
  }

  // Validate: code must be assigned to this user's email
  if (employeeCode.assigned_to_email.toLowerCase().trim() !== userEmail) {
    return errorResponse(
      "This employee code is not assigned to your email address.",
      400
    );
  }

  // ── 5. Atomically claim the code ──────────────────────────────────────────
  //
  // The WHERE clause includes claimed_at IS NULL to guard against race
  // conditions. If two concurrent requests reach this point simultaneously,
  // only the first UPDATE will match the row. The second will update 0 rows
  // and be caught by the count check below.
  //
  const { data: claimResult, error: claimError } = await adminClient
    .from("employee_codes")
    .update({
      claimed_by: userId,
      claimed_at: new Date().toISOString(),
    })
    .eq("id", employeeCode.id)
    .is("claimed_at", null)       // race condition guard
    .is("claimed_by", null)       // race condition guard
    .eq("is_active", true)        // ensure it hasn't been revoked between fetch and update
    .select("id")
    .returns<{ id: string }[]>();

  if (claimError) {
    console.error("Failed to claim employee code:", claimError);
    return errorResponse("Failed to claim employee code. Please try again.", 500);
  }

  if (!claimResult || claimResult.length === 0) {
    // Another request claimed the code between our SELECT and this UPDATE
    return errorResponse(
      "This employee code has already been used. Contact your administrator.",
      409
    );
  }

  // ── 6. Link user profile to the hotel ─────────────────────────────────────
  const { error: profileError } = await adminClient
    .from("profiles")
    .update({ company_id: employeeCode.company_id })
    .eq("id", userId);

  if (profileError) {
    console.error("Failed to update profile company_id:", profileError);

    // Attempt to roll back the code claim so the user can try again
    await adminClient
      .from("employee_codes")
      .update({ claimed_by: null, claimed_at: null })
      .eq("id", employeeCode.id);

    return errorResponse(
      "Failed to link your account to the hotel. Please try again.",
      500
    );
  }

  // ── 7. Write company_id and role to auth.users app_metadata ───────────────
  //
  // This is what causes Supabase to include company_id in the next JWT.
  // The client must call supabase.auth.refreshSession() after this succeeds.
  //
  const { error: metaError } = await adminClient.auth.admin.updateUser(userId, {
    app_metadata: {
      company_id: employeeCode.company_id,
      role:       "employee",
    },
  });

  if (metaError) {
    console.error("Failed to update user app_metadata:", metaError);

    // Attempt rollback of profile update and code claim
    await adminClient
      .from("profiles")
      .update({ company_id: null })
      .eq("id", userId);

    await adminClient
      .from("employee_codes")
      .update({ claimed_by: null, claimed_at: null })
      .eq("id", employeeCode.id);

    return errorResponse(
      "Failed to activate hotel access. Please try again.",
      500
    );
  }

  // ── 8. Fetch company info for the confirmation response ───────────────────
  const { data: company, error: companyError } = await adminClient
    .from("companies")
    .select("id, name, slug, logo_url, primary_color")
    .eq("id", employeeCode.company_id)
    .single<Company>();

  if (companyError || !company) {
    // Non-fatal: the link succeeded even if we can't return the company details
    console.error("Failed to fetch company for response:", companyError);
  }

  // ── 9. Audit log ──────────────────────────────────────────────────────────
  await writeAuditLog(adminClient, {
    companyId:  employeeCode.company_id,
    actorId:    userId,
    action:     "user.claim_employee_code",
    targetType: "profile",
    targetId:   userId,
    metadata: {
      code:               code,
      employee_code_id:   employeeCode.id,
      assigned_to_email:  employeeCode.assigned_to_email,
    },
    req,
  });

  // ── 10. Respond ────────────────────────────────────────────────────────────
  //
  // The client must call supabase.auth.refreshSession() after receiving this
  // response. Only after the session is refreshed will the new JWT contain
  // company_id and allow the user to access hotel-scoped data.
  //
  return jsonResponse({
    success:         true,
    message:         "Employee code accepted. Your account has been linked.",
    requiresRefresh: true,   // signals the client to call refreshSession()
    company: company
      ? {
          id:           company.id,
          name:         company.name,
          slug:         company.slug,
          logoUrl:      company.logo_url,
          primaryColor: company.primary_color,
        }
      : null,
  }, 200);
});
