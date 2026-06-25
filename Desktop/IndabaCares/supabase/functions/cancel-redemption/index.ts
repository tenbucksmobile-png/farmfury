/**
 * Edge Function: cancel-redemption
 *
 * Allows an employee to cancel their own pending redemption order.
 *   - Only pending orders can be self-cancelled
 *   - Points are refunded atomically via process_refund()
 *   - Action is recorded in audit_logs
 *
 * Security:
 *   - Employee session required (withEmployeeAuth)
 *   - Ownership enforced: redemption must match employee_id AND hotel from session
 *   - Only pending status can be cancelled (others are rejected with a clear message)
 */

import {
  withEmployeeAuth,
  jsonResponse,
  errorResponse,
  type EmployeeAuthContext,
} from "../_shared/auth-middleware.ts";
import { writeAuditLog } from "../_shared/audit.ts";
import { enforceRateLimit } from "../_shared/rate-limit.ts";

interface CancelRequest {
  redemptionId: string;
}

Deno.serve(
  withEmployeeAuth(async (req: Request, ctx: EmployeeAuthContext): Promise<Response> => {
    if (req.method !== "POST") {
      return errorResponse("Method not allowed", 405);
    }

    const { employee, adminClient } = ctx;

    // ── Rate limit: max 10 cancellations per employee per hour ───────
    const rateLimited = await enforceRateLimit(adminClient, {
      identifier:    employee.employee_id,
      action:        "cancel_redemption",
      maxAttempts:   10,
      windowMinutes: 60,
    });
    if (rateLimited) return rateLimited;

    const body: CancelRequest = await req.json();

    if (!body.redemptionId) {
      return errorResponse("redemptionId is required");
    }

    // ── Fetch the redemption — ownership + hotel enforced ────────────
    const { data: redemption, error: fetchError } = await adminClient
      .from("redemptions")
      .select("id, employee_id, reward_id, status, hotel, reward:rewards ( name, points_cost )")
      .eq("id", body.redemptionId)
      .eq("employee_id", employee.employee_id)
      .eq("hotel",       employee.hotel)
      .single();

    if (fetchError || !redemption) {
      return errorResponse("Redemption not found", 404);
    }

    // ── Only pending orders can be self-cancelled ────────────────────
    if (redemption.status !== "pending") {
      return errorResponse(
        `Cannot cancel an order with status "${redemption.status}". Only pending orders can be cancelled.`
      );
    }

    // ── Process refund atomically ────────────────────────────────────
    const { error: refundError } = await adminClient.rpc("process_refund", {
      p_redemption_id: body.redemptionId,
      p_hotel:         employee.hotel,
      p_reason:        "Cancelled by employee",
    });

    if (refundError) {
      console.error("Refund failed:", refundError);
      return errorResponse("Failed to process refund. Please try again.", 500);
    }

    // ── Update redemption status ─────────────────────────────────────
    const { error: updateError } = await adminClient
      .from("redemptions")
      .update({
        status:       "cancelled",
        cancelled_at: new Date().toISOString(),
      })
      .eq("id", body.redemptionId);

    if (updateError) {
      console.error("Status update failed:", updateError);
      return errorResponse("Refund processed but status update failed.", 500);
    }

    // ── Audit log ────────────────────────────────────────────────────
    await writeAuditLog(adminClient, {
      hotel:      employee.hotel,
      actorId:    employee.employee_id,
      action:     "redemption.cancel",
      targetType: "redemption",
      targetId:   body.redemptionId,
      metadata:   {
        reward_name:  redemption.reward?.name,
        points_cost:  redemption.reward?.points_cost,
      },
      req,
    });

    // ── Fetch updated points balance ─────────────────────────────────
    const { data: updatedEmployee } = await adminClient
      .from("employees")
      .select("points_balance")
      .eq("id", employee.employee_id)
      .single();

    return jsonResponse({
      redemptionId:  body.redemptionId,
      status:        "cancelled",
      pointsRefunded: redemption.reward?.points_cost ?? 0,
      pointsBalance:  updatedEmployee?.points_balance ?? 0,
      message:        `Order cancelled. Points have been refunded to your balance.`,
    });
  })
);
