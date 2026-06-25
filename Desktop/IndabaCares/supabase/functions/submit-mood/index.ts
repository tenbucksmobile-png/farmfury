/**
 * Edge Function: submit-mood
 *
 * Daily mood check-in. Once-per-day enforced at DB level via the submit_mood
 * procedure (error code P3001) and a UNIQUE constraint on (employee_id, entry_date).
 *
 * Security:
 *   - Employee session required (withEmployeeAuth)
 *   - hotel sourced from session — not trusted from the client
 *   - Individual moods are never exposed to peers (service_role reads only)
 */

import {
  withEmployeeAuth,
  jsonResponse,
  errorResponse,
  type EmployeeAuthContext,
} from "../_shared/auth-middleware.ts";
import { enforceRateLimit } from "../_shared/rate-limit.ts";

const VALID_MOODS = ["awful", "bad", "okay", "good", "amazing"] as const;
type MoodValue = typeof VALID_MOODS[number];

interface SubmitMoodRequest {
  mood:  MoodValue;
  note?: string;
}

Deno.serve(
  withEmployeeAuth(async (req: Request, ctx: EmployeeAuthContext): Promise<Response> => {
    if (req.method !== "POST") {
      return errorResponse("Method not allowed", 405);
    }

    const { employee, adminClient } = ctx;

    // ── Rate limit: max 5 mood submissions per employee per hour ─────
    const rateLimited = await enforceRateLimit(adminClient, {
      identifier:    employee.employee_id,
      action:        "submit_mood",
      maxAttempts:   5,
      windowMinutes: 60,
    });
    if (rateLimited) return rateLimited;

    const body: SubmitMoodRequest = await req.json();

    // ── Validate mood value ──────────────────────────────────────────
    if (!body.mood || !VALID_MOODS.includes(body.mood)) {
      return errorResponse(
        `Invalid mood. Must be one of: ${VALID_MOODS.join(", ")}`
      );
    }

    // ── Sanitize optional note ───────────────────────────────────────
    const note = body.note
      ? body.note.replace(/<[^>]*>/g, "").trim().substring(0, 500)
      : null;

    // ── Call atomic Postgres procedure ───────────────────────────────
    const { data: entryId, error: procError } = await adminClient.rpc(
      "submit_mood",
      {
        p_employee_id: employee.employee_id,
        p_hotel:       employee.hotel,
        p_mood:        body.mood,
        p_note:        note,
      }
    );

    if (procError) {
      if (procError.code === "P3001") {
        return errorResponse("You've already submitted your mood today", 409);
      }
      // UNIQUE violation (belt-and-suspenders)
      if (procError.code === "23505") {
        return errorResponse("You've already submitted your mood today", 409);
      }
      console.error("submit_mood failed:", procError);
      return errorResponse("Failed to submit mood. Please try again.", 500);
    }

    return jsonResponse(
      {
        moodEntryId: entryId,
        mood:        body.mood,
        message:     "Mood recorded. Thank you for sharing!",
      },
      201
    );
  })
);
