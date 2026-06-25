/**
 * Edge Function: daily-celebrations
 *
 * Runs once per day (scheduled via Supabase cron — see README).
 *
 * Steps:
 *   1. Call generate_today_celebrations() to insert birthday/anniversary rows.
 *   2. Query the new rows joined to push_tokens.
 *   3. Send Expo push notifications to each affected employee.
 *
 * Trigger: POST (no body required — can be called by cron or manually).
 */

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const supabase = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
);

const EXPO_PUSH_URL = "https://exp.host/--/api/v2/push/send";

interface PushMessage {
  to:    string;
  title: string;
  body:  string;
  data?: Record<string, unknown>;
}

async function sendPushNotifications(messages: PushMessage[]): Promise<void> {
  if (messages.length === 0) return;

  // Expo accepts up to 100 messages per request
  for (let i = 0; i < messages.length; i += 100) {
    const batch = messages.slice(i, i + 100);
    await fetch(EXPO_PUSH_URL, {
      method:  "POST",
      headers: { "Content-Type": "application/json" },
      body:    JSON.stringify(batch),
    });
  }
}

Deno.serve(async (req: Request): Promise<Response> => {
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }

  // ── 1. Generate today's celebrations ──────────────────────────────────────
  const { data: insertedCount, error: genError } = await supabase
    .rpc("generate_today_celebrations");

  if (genError) {
    console.error("generate_today_celebrations failed:", genError);
    return new Response(
      JSON.stringify({ error: genError.message }),
      { status: 500, headers: { "Content-Type": "application/json" } },
    );
  }

  console.log(`Celebrations generated: ${insertedCount}`);

  if (insertedCount === 0) {
    return new Response(
      JSON.stringify({ ok: true, celebrations: 0, notificationsSent: 0 }),
      { headers: { "Content-Type": "application/json" } },
    );
  }

  // ── 2. Fetch today's celebrations with employee push tokens ───────────────
  const today = new Date().toISOString().split("T")[0]; // YYYY-MM-DD

  const { data: rows, error: fetchError } = await supabase
    .from("celebrations")
    .select(`
      id,
      type,
      milestone,
      employee_id,
      employees!inner (
        full_name,
        push_tokens ( token )
      )
    `)
    .eq("celebrated_on", today);

  if (fetchError) {
    console.error("Fetch celebrations failed:", fetchError);
    return new Response(
      JSON.stringify({ error: fetchError.message }),
      { status: 500, headers: { "Content-Type": "application/json" } },
    );
  }

  // ── 3. Build push notifications ───────────────────────────────────────────
  const messages: PushMessage[] = [];

  for (const row of rows ?? []) {
    const employee  = (row as any).employees;
    const tokens    = employee?.push_tokens ?? [];
    const firstName = (employee?.full_name ?? "").split(" ")[0];

    let title: string;
    let body:  string;

    if (row.type === "birthday") {
      title = "🎂 Happy Birthday!";
      body  = `Wishing ${firstName} a wonderful birthday today!`;
    } else {
      const years = row.milestone ?? 1;
      const label = years === 1 ? "1 year" : `${years} years`;
      title = "🏆 Work Anniversary!";
      body  = `${firstName} is celebrating ${label} with the team today!`;
    }

    for (const pt of tokens) {
      if (pt.token) {
        messages.push({
          to:    pt.token,
          title,
          body,
          data:  { type: row.type, celebrationId: row.id },
        });
      }
    }
  }

  // ── 4. Send push notifications ────────────────────────────────────────────
  await sendPushNotifications(messages);

  console.log(`Notifications sent: ${messages.length}`);

  return new Response(
    JSON.stringify({
      ok:                true,
      celebrations:      insertedCount,
      notificationsSent: messages.length,
    }),
    { headers: { "Content-Type": "application/json" } },
  );
});
