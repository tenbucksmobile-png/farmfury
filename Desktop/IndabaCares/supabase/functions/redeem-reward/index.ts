/**
 * Edge Function: redeem-reward
 *
 * Processes a reward redemption:
 *   1. Validates reward exists, is active, and belongs to the employee's hotel
 *   2. Calls redeem_reward() — atomic Postgres procedure that checks balance,
 *      deducts points, decrements stock, and creates the redemption row
 *   3. Returns the created redemption and updated points balance
 *
 * DB triggers handle the points_ledger entry and the employee's points_balance
 * deduction (via deduct_points_for_redemption / redeem_reward RPC).
 *
 * Security:
 *   - Employee session required (withEmployeeAuth)
 *   - hotel sourced from session — reward must belong to the same hotel
 *   - Inventory lock and balance check handled atomically by the DB procedure
 */

import {
  withEmployeeAuth,
  jsonResponse,
  errorResponse,
  type EmployeeAuthContext,
} from "../_shared/auth-middleware.ts";

interface RedeemRequest {
  rewardId: string;
}

Deno.serve(
  withEmployeeAuth(async (req: Request, ctx: EmployeeAuthContext): Promise<Response> => {
    if (req.method !== "POST") {
      return errorResponse("Method not allowed", 405);
    }

    const { employee, adminClient } = ctx;
    const body: RedeemRequest = await req.json();

    if (!body.rewardId) {
      return errorResponse("rewardId is required");
    }

    const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!UUID_RE.test(body.rewardId)) {
      return errorResponse("rewardId must be a valid UUID", 400);
    }

    // ── Rate limit: max 5 redemptions per employee per hour ──────────
    const { data: withinLimit } = await adminClient.rpc("check_rate_limit", {
      p_identifier:    employee.employee_id,
      p_action:        "redeem",
      p_max_attempts:  5,
      p_window_minutes: 60,
    });

    if (withinLimit === false) {
      return errorResponse("Too many redemption attempts. Try again later.", 429);
    }

    await adminClient.rpc("record_rate_limit", {
      p_identifier: employee.employee_id,
      p_action:     "redeem",
    });

    // ── Call atomic Postgres procedure ───────────────────────────────
    const { data: redemptionId, error: procError } = await adminClient.rpc(
      "redeem_reward",
      {
        p_employee_id: employee.employee_id,
        p_hotel:       employee.hotel,
        p_reward_id:   body.rewardId,
      }
    );

    if (procError) {
      const errorMap: Record<string, { msg: string; status: number }> = {
        P2001: { msg: "Reward not found or no longer available", status: 404 },
        P2002: { msg: "This reward is out of stock",             status: 409 },
        P2003: { msg: procError.message,                         status: 400 },
      };

      const mapped = errorMap[procError.code ?? ""];
      if (mapped) {
        return errorResponse(mapped.msg, mapped.status);
      }

      console.error("redeem_reward failed:", procError);
      return errorResponse("Redemption failed. Please try again.", 500);
    }

    // ── Fetch the created redemption for the response ────────────────
    const { data: redemption } = await adminClient
      .from("redemptions")
      .select("id, status, created_at, reward:rewards ( id, name, points_cost )")
      .eq("id", redemptionId)
      .single();

    // ── Fetch updated points balance ─────────────────────────────────
    const { data: updatedEmployee } = await adminClient
      .from("employees")
      .select("points_balance")
      .eq("id", employee.employee_id)
      .single();

    return jsonResponse(
      {
        redemption,
        pointsBalance: updatedEmployee?.points_balance ?? 0,
        message:       "Reward redeemed! Your order is pending approval.",
      },
      201
    );
  })
);
