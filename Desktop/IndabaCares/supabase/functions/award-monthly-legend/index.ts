/**
 * Edge Function: award-monthly-legend
 *
 * Runs on the last day of each month (triggered by pg_cron).
 * Can also be called manually for backfill by passing { month, year, hotel }.
 *
 * For each hotel (or a specific hotel if supplied):
 *   1. Finds the #1 employee on the leaderboard for that month
 *   2. Awards 500 points via points_ledger
 *   3. Creates a "Legend of the Month" recognition card in the feed
 *   4. Inserts a row into monthly_legends (idempotent — ON CONFLICT DO NOTHING)
 *   5. Sends a notification to the winner
 *
 * Security: protected by x-cron-secret header — NOT employee auth.
 */

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const SUPABASE_URL            = Deno.env.get("SUPABASE_URL")!;
const SUPABASE_SERVICE_KEY    = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const CRON_SECRET             = Deno.env.get("CRON_SECRET") ?? "";
const POINTS_AWARDED          = 250;
const LEGEND_BADGE            = "Legend of the Month";

const CORS = {
  "Access-Control-Allow-Origin":  "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type, x-cron-secret",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

function json(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { ...CORS, "Content-Type": "application/json" },
  });
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function firstDayOfMonth(year: number, month: number): string {
  return `${year}-${String(month).padStart(2, "0")}-01T00:00:00.000Z`;
}

function lastDayOfMonth(year: number, month: number): string {
  const d = new Date(Date.UTC(year, month, 0, 23, 59, 59, 999)); // month is 1-based; Date(y, m, 0) = last day
  return d.toISOString();
}

const MONTH_NAMES = [
  "", "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

// ─── Main handler ─────────────────────────────────────────────────────────────

Deno.serve(async (req: Request): Promise<Response> => {
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS });
  }

  if (req.method !== "POST") {
    return json({ error: "Method not allowed" }, 405);
  }

  // ── Auth: cron secret ──────────────────────────────────────────────────────
  const secret = req.headers.get("x-cron-secret") ?? "";
  if (CRON_SECRET && secret !== CRON_SECRET) {
    return json({ error: "Unauthorized" }, 401);
  }

  const adminClient = createClient(SUPABASE_URL, SUPABASE_SERVICE_KEY);

  // ── Resolve target month (default = previous calendar month) ──────────────
  let body: { month?: number; year?: number; hotel?: string } = {};
  try { body = await req.json(); } catch { /* empty body is fine */ }

  const now = new Date();
  const targetMonth = body.month ?? (now.getUTCMonth() === 0 ? 12 : now.getUTCMonth());
  const targetYear  = body.year  ?? (now.getUTCMonth() === 0 ? now.getUTCFullYear() - 1 : now.getUTCFullYear());

  const periodStart = firstDayOfMonth(targetYear, targetMonth);
  const periodEnd   = lastDayOfMonth(targetYear, targetMonth);

  // ── Fetch all hotels (or a specific one) ──────────────────────────────────
  let hotels: string[] = [];

  if (body.hotel) {
    hotels = [body.hotel];
  } else {
    const { data: empRows } = await adminClient
      .from("employees")
      .select("hotel")
      .eq("status", "active");

    const unique = new Set((empRows ?? []).map((r: { hotel: string }) => r.hotel));
    hotels = Array.from(unique);
  }

  const results: Array<{ hotel: string; winner?: string; skipped?: string; error?: string }> = [];

  for (const hotel of hotels) {
    try {
      // ── 1. Get #1 employee for this hotel this month ─────────────────────
      const { data: leaderboard, error: lbErr } = await adminClient.rpc(
        "get_leaderboard",
        { p_hotel: hotel, p_start: periodStart, p_end: periodEnd, p_limit: 1 }
      );

      if (lbErr || !leaderboard?.length) {
        results.push({ hotel, skipped: "No leaderboard data" });
        continue;
      }

      const winner = leaderboard[0] as {
        employee_id: string;
        full_name:   string;
        job_title:   string | null;
        avatar_url:  string | null;
        total_points: number;
      };

      // ── 2. Already awarded? (idempotency check) ───────────────────────────
      const { data: existing } = await adminClient
        .from("monthly_legends")
        .select("id")
        .eq("hotel", hotel)
        .eq("month", targetMonth)
        .eq("year", targetYear)
        .maybeSingle();

      if (existing) {
        results.push({ hotel, skipped: `Already awarded to ${winner.full_name}` });
        continue;
      }

      // ── 3. Award 250 points via points_ledger ─────────────────────────────
      await adminClient.from("points_ledger").insert({
        employee_id: winner.employee_id,
        points:      POINTS_AWARDED,
        source:      "legend_of_month",
        hotel,
      });

      // Update points_balance on employees table
      await adminClient.rpc("increment_points_balance", {
        p_employee_id: winner.employee_id,
        p_points:      POINTS_AWARDED,
      }).maybeSingle();

      // ── 4. Find a sender for the recognition card ─────────────────────────
      //    Prefer a manager; fall back to any other active employee.
      const { data: senderRows } = await adminClient
        .from("employees")
        .select("id")
        .eq("hotel", hotel)
        .eq("status", "active")
        .neq("id", winner.employee_id)
        .limit(5);

      const senderId = senderRows?.[0]?.id ?? null;

      // ── 5. Create recognition card ────────────────────────────────────────
      let recognitionId: string | null = null;

      if (senderId) {
        const monthName = MONTH_NAMES[targetMonth];
        const { data: rec } = await adminClient
          .from("recognitions")
          .insert({
            sender_id:   senderId,
            receiver_id: winner.employee_id,
            badge:       LEGEND_BADGE,
            message:     `🏆 ${monthName} ${targetYear} — Legend of the Month! Your outstanding performance and dedication this month put you at the top of the leaderboard. Congratulations, ${winner.full_name}!`,
            hotel,
          })
          .select("id")
          .single();

        recognitionId = rec?.id ?? null;
      }

      // ── 6. Insert into monthly_legends ────────────────────────────────────
      await adminClient.from("monthly_legends").insert({
        hotel,
        employee_id:    winner.employee_id,
        full_name:      winner.full_name,
        job_title:      winner.job_title,
        avatar_url:     winner.avatar_url,
        month:          targetMonth,
        year:           targetYear,
        total_points:   winner.total_points,
        points_awarded: POINTS_AWARDED,
        recognition_id: recognitionId,
      });

      // ── 7. Send notification ──────────────────────────────────────────────
      await adminClient.from("notifications").insert({
        employee_id: winner.employee_id,
        hotel,
        type:        "legend_of_month",
        title:       "🏆 Legend of the Month!",
        body:        `Congratulations! You are the ${MONTH_NAMES[targetMonth]} ${targetYear} Legend of the Month. You've been awarded ${POINTS_AWARDED} bonus points!`,
        data:        { month: targetMonth, year: targetYear },
      }).maybeSingle();

      results.push({ hotel, winner: winner.full_name });

    } catch (err) {
      results.push({ hotel, error: String(err) });
    }
  }

  return json({ ok: true, period: `${targetYear}-${targetMonth}`, results });
});
