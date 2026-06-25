/**
 * Edge Function: boost-recognition
 *
 * Awards bonus points to the receiver of a recognition post.
 * Intended for use by managers/admins via the admin panel (service_role).
 * When called from the mobile app, any authenticated employee may boost —
 * role-based gating should be enforced at the admin panel layer until a
 * role column is added to the employees table.
 *
 * What it does:
 *   1. Verifies the recognition belongs to the caller's hotel
 *   2. Rejects if caller is the sender (cannot boost own recognition)
 *   3. Awards BOOST_BONUS_POINTS to the receiver via admin_grant_points()
 *   4. Sends a notification to the receiver
 *
 * NOTE: The recognitions table (migration 018) does not have is_boosted /
 * boosted_by / boosted_at columns.  A future schema migration should add
 * them to prevent a recognition being boosted more than once.
 *
 * Security:
 *   - Employee session required (withEmployeeAuth)
 *   - hotel sourced from session — recognition must be in the same hotel
 *   - Sender cannot boost their own recognition
 */

import {
  withEmployeeAuth,
  jsonResponse,
  errorResponse,
  type EmployeeAuthContext,
} from "../_shared/auth-middleware.ts";
import { notify } from "../_shared/notifications.ts";
import { enforceRateLimit } from "../_shared/rate-limit.ts";

interface BoostRequest {
  recognitionId: string;
}

const BOOST_BONUS_POINTS = 25;

Deno.serve(
  withEmployeeAuth(async (req: Request, ctx: EmployeeAuthContext): Promise<Response> => {
    if (req.method !== "POST") {
      return errorResponse("Method not allowed", 405);
    }

    const { employee, adminClient } = ctx;

    // ── Rate limit: max 20 boosts per employee per hour ──────────────
    const rateLimited = await enforceRateLimit(adminClient, {
      identifier:    employee.employee_id,
      action:        "boost",
      maxAttempts:   20,
      windowMinutes: 60,
    });
    if (rateLimited) return rateLimited;

    const body: BoostRequest = await req.json();

    if (!body.recognitionId) {
      return errorResponse("recognitionId is required");
    }

    // ── Fetch the recognition — hotel-scoped ─────────────────────────
    const { data: recognition, error: fetchError } = await adminClient
      .from("recognitions")
      .select("id, sender_id, receiver_id, message, badge")
      .eq("id", body.recognitionId)
      .eq("hotel", employee.hotel)
      .single();

    if (fetchError || !recognition) {
      return errorResponse("Recognition not found", 404);
    }

    if (recognition.sender_id === employee.employee_id) {
      return errorResponse("You cannot boost your own recognition");
    }

    // ── Award bonus points to the receiver ───────────────────────────
    // admin_grant_points() handles the points_balance update and
    // appends a row to points_ledger (source = 'admin_bonus').
    const { error: pointsError } = await adminClient.rpc("admin_grant_points", {
      p_employee_id: recognition.receiver_id,
      p_points:      BOOST_BONUS_POINTS,
      p_source:      "admin_bonus",
    });

    if (pointsError) {
      console.error("admin_grant_points failed:", pointsError);
      return errorResponse("Failed to award boost points. Please try again.", 500);
    }

    // ── Notify the receiver ──────────────────────────────────────────
    await notify(adminClient, {
      employeeId:    recognition.receiver_id,
      hotel:         employee.hotel,
      type:          "recognition_received",
      title:         `${employee.full_name} boosted your recognition!`,
      message:       `You earned +${BOOST_BONUS_POINTS} bonus points for your "${recognition.badge}" recognition.`,
      referenceType: "recognition",
      referenceId:   body.recognitionId,
    });

    return jsonResponse({
      recognitionId: body.recognitionId,
      boostedBy:     employee.employee_id,
      receiverId:    recognition.receiver_id,
      bonusPoints:   BOOST_BONUS_POINTS,
      message:       "Recognition boosted!",
    });
  })
);
