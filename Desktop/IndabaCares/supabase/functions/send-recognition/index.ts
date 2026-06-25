/**
 * Edge Function: send-recognition
 *
 * Validates and inserts a recognition post.
 *
 * Inserts ONLY: message, sender_id, receiver_id, hotel.
 * No points are awarded at this stage (migration 034 removed that trigger).
 * The receiver notification is handled by trg_notify_recognition (migration 023).
 *
 * Security:
 *   - Employee session required (withEmployeeAuth)
 *   - hotel is sourced from the validated session — never trusted from the client
 *   - sender_id is pinned to the authenticated employee — cannot spoof
 *   - Self-recognition rejected by DB CHECK constraint (chk_no_self_recognition)
 */

import {
  withEmployeeAuth,
  jsonResponse,
  errorResponse,
  type EmployeeAuthContext,
} from "../_shared/auth-middleware.ts";
import { enforceRateLimit } from "../_shared/rate-limit.ts";

const VALID_BADGES = [
  "Team Player",
  "Leadership",
  "Customer Excellence",
  "You Legend",
  "Going the Extra Mile",
  "Hospitality Hero",
] as const;

interface SendRecognitionRequest {
  receiverId: string;
  badge:      typeof VALID_BADGES[number];
  message:    string;
}

Deno.serve(
  withEmployeeAuth(async (req: Request, ctx: EmployeeAuthContext): Promise<Response> => {
    if (req.method !== "POST") {
      return errorResponse("Method not allowed", 405);
    }

    const { employee, adminClient } = ctx;

    // ── Rate limit: max 10 recognitions per employee per hour ────────
    const rateLimited = await enforceRateLimit(adminClient, {
      identifier:    employee.employee_id,
      action:        "send_recognition",
      maxAttempts:   10,
      windowMinutes: 60,
    });
    if (rateLimited) return rateLimited;

    const body: SendRecognitionRequest = await req.json();

    // ── Input validation ─────────────────────────────────────────────
    if (!body.receiverId) {
      return errorResponse("receiverId is required");
    }

    if (body.receiverId === employee.employee_id) {
      return errorResponse("You cannot recognise yourself");
    }

    if (!body.badge || !VALID_BADGES.includes(body.badge)) {
      return errorResponse(
        `Invalid badge. Must be one of: ${VALID_BADGES.join(", ")}`
      );
    }

    if (!body.message || body.message.trim().length < 10) {
      return errorResponse("Message must be at least 10 characters");
    }

    if (body.message.length > 2000) {
      return errorResponse("Message must be 2000 characters or fewer");
    }

    // Sanitize: strip HTML
    const message = body.message.replace(/<[^>]*>/g, "").trim();

    // ── Verify receiver exists ────────────────────────────────────────
    // APA employees (group directors) can recognise across all hotels.
    // All other employees can only recognise within their own hotel.
    const isApa = employee.hotel === "African Procurement Agencies";

    let receiverQuery = adminClient
      .from("employees")
      .select("id, full_name, hotel")
      .eq("id", body.receiverId)
      .eq("status", "active");

    if (!isApa) {
      receiverQuery = receiverQuery.eq("hotel", employee.hotel);
    }

    const { data: receiver, error: receiverError } = await receiverQuery.single();

    if (receiverError || !receiver) {
      return errorResponse(
        isApa
          ? "Recipient not found or is not active"
          : "Recipient not found or is not active at your hotel",
        404
      );
    }

    // Tag the recognition to the recipient's hotel so it appears in their feed.
    const recognitionHotel = isApa ? receiver.hotel : employee.hotel;

    // ── Insert recognition ────────────────────────────────────────────
    const { data: recognition, error: insertError } = await adminClient
      .from("recognitions")
      .insert({
        sender_id:   employee.employee_id,
        receiver_id: body.receiverId,
        message,
        badge:  body.badge,
        hotel:  recognitionHotel,
      })
      .select("id, message, badge, hotel, created_at")
      .single();

    if (insertError) {
      console.error("recognitions INSERT failed:", insertError);
      return errorResponse("Failed to send recognition. Please try again.", 500);
    }

    return jsonResponse(
      {
        recognition: {
          ...recognition,
          sender:   { id: employee.employee_id, full_name: employee.full_name },
          receiver: { id: receiver.id,          full_name: receiver.full_name },
        },
        message: "Recognition sent successfully",
      },
      201
    );
  })
);
