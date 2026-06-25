/**
 * Edge Function: refresh-leaderboard
 *
 * Refreshes supporting data for the leaderboard:
 *   1. Refreshes the happiness_scores materialized view.
 *   2. Cleans up stale rate-limit rows.
 *   3. For each active hotel, fetches the current top-3 employees and triggers
 *      background removal on their profile photos so the podium renders with
 *      transparent-background PNGs (written to employees.podium_photo_url).
 *
 * The leaderboard itself is fully live (no cache table since migration 033), so
 * there is no per-hotel leaderboard table to refresh.
 *
 * Invocation:
 *   POST /functions/v1/refresh-leaderboard
 *   Authorization: Bearer <service_role_key>
 *
 * Optional body:
 *   { hotel: string }  — process a single hotel instead of all
 */

import { createAdminClient } from "../_shared/supabase-client.ts";
import { errorResponse, jsonResponse } from "../_shared/auth-middleware.ts";

const CORS_HEADERS: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS_HEADERS });
  }

  if (req.method !== "POST") {
    return errorResponse("Method not allowed", 405);
  }

  const authHeader = req.headers.get("Authorization");
  if (!authHeader) {
    return errorResponse("Missing Authorization header", 401);
  }

  const adminClient = createAdminClient();

  // Optional: target a single hotel
  let targetHotel: string | null = null;
  try {
    const body = await req.json();
    targetHotel = body.hotel ?? null;
  } catch {
    // No body — process all hotels
  }

  try {
    // ── 1. Get distinct active hotels ──────────────────────────────────────
    let hotelsQuery = adminClient
      .from("employees")
      .select("hotel")
      .eq("status", "active");

    if (targetHotel) {
      hotelsQuery = hotelsQuery.eq("hotel", targetHotel);
    }

    const { data: hotelRows, error: hotelError } = await hotelsQuery;

    if (hotelError || !hotelRows) {
      return errorResponse("Failed to fetch hotels: " + hotelError?.message, 500);
    }

    const hotels = [...new Set(hotelRows.map((r: { hotel: string }) => r.hotel))];

    // ── 2. Process top-3 podium photos per hotel ──────────────────────────
    const podiumResults: Array<{ hotel: string; status: string }> = [];

    const supabaseUrl = Deno.env.get("SUPABASE_URL") ?? "";
    const serviceKey  = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";

    for (const hotel of hotels) {
      try {
        // Fetch current monthly top-3
        const { data: top3, error: lbError } = await adminClient.rpc("get_leaderboard", {
          p_hotel: hotel,
          p_start: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString(),
          p_end:   null,
          p_limit: 3,
        });

        if (lbError) {
          podiumResults.push({ hotel, status: `leaderboard error: ${lbError.message}` });
          continue;
        }

        const entries = (top3 ?? []) as Array<{
          employee_id: string;
          avatar_url:  string | null;
        }>;

        // Fire background removal for each entry that has a photo
        const removals = await Promise.allSettled(
          entries
            .filter((e) => !!e.avatar_url)
            .map((e) =>
              fetch(`${supabaseUrl}/functions/v1/remove-background`, {
                method: "POST",
                headers: {
                  "Authorization": `Bearer ${serviceKey}`,
                  "Content-Type":  "application/json",
                },
                body: JSON.stringify({
                  employee_id: e.employee_id,
                  photo_url:   e.avatar_url,
                }),
              }).then((r) => r.json())
            )
        );

        const failed = removals.filter((r) => r.status === "rejected").length;
        podiumResults.push({
          hotel,
          status: failed === 0
            ? `ok (${entries.length} processed)`
            : `partial (${failed} failed)`,
        });
      } catch (e) {
        podiumResults.push({
          hotel,
          status: `exception: ${e instanceof Error ? e.message : String(e)}`,
        });
      }
    }

    // ── 3. Refresh happiness scores materialized view ─────────────────────
    let happinessRefresh = "ok";
    try {
      await adminClient.rpc("refresh_materialized_view_concurrently", {
        view_name: "happiness_scores",
      });
    } catch {
      happinessRefresh = "skipped";
    }

    // ── 4. Cleanup old rate limits ────────────────────────────────────────
    try { await adminClient.rpc("cleanup_rate_limits"); } catch { /* non-critical */ }

    return jsonResponse({
      hotelsProcessed: hotels.length,
      podiumResults,
      happinessScoreRefresh: happinessRefresh,
      timestamp: new Date().toISOString(),
    });
  } catch (err) {
    console.error("Leaderboard refresh failed:", err);
    return errorResponse("Internal server error", 500);
  }
});
