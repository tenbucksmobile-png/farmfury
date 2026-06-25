/**
 * remove-background Edge Function (internal)
 *
 * Called server-to-server by refresh-leaderboard when processing the top-3
 * podium employees. Downloads the employee's existing photo, strips the
 * background via remove.bg, uploads the transparent PNG to the avatars bucket,
 * and updates employees.podium_photo_url.
 *
 * NOT called by the mobile app — profile uploads use direct storage upload.
 *
 * Request body (JSON):
 *   { employee_id: string, photo_url: string }
 *
 * Response (JSON):
 *   { ok: true,  podium_photo_url: string }
 *   { ok: false, error: string }
 *
 * Auth:
 *   Authorization: Bearer <SUPABASE_SERVICE_ROLE_KEY>
 *
 * Required secret:
 *   REMOVE_BG_API_KEY — set via: supabase secrets set REMOVE_BG_API_KEY=<key>
 */

import { createAdminClient } from "../_shared/supabase-client.ts";
import { errorResponse, jsonResponse } from "../_shared/auth-middleware.ts";

const REMOVE_BG_URL = "https://api.remove.bg/v1.0/removebg";

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204 });
  }

  if (req.method !== "POST") {
    return errorResponse("Method not allowed", 405);
  }

  // ── Auth: service_role key only ───────────────────────────────────────────
  const serviceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  const authHeader = req.headers.get("Authorization");

  if (!authHeader || !serviceKey || authHeader !== `Bearer ${serviceKey}`) {
    return errorResponse("Unauthorized", 401);
  }

  // ── Parse request ─────────────────────────────────────────────────────────
  const { employee_id, photo_url } = await req.json() as {
    employee_id?: string;
    photo_url?: string;
  };

  if (!employee_id || !photo_url) {
    return errorResponse("Missing employee_id or photo_url", 400);
  }

  const apiKey = Deno.env.get("REMOVE_BG_API_KEY");
  if (!apiKey) {
    return errorResponse("REMOVE_BG_API_KEY secret is not set", 500);
  }

  // ── 1. Fetch the existing photo ───────────────────────────────────────────
  let imageBytes: ArrayBuffer;
  try {
    const fetchRes = await fetch(photo_url);
    if (!fetchRes.ok) {
      return errorResponse(`Failed to fetch photo: ${fetchRes.status}`, 502);
    }
    imageBytes = await fetchRes.arrayBuffer();
  } catch (e) {
    return errorResponse(`Photo fetch error: ${e instanceof Error ? e.message : String(e)}`, 502);
  }

  // ── 2. Send to remove.bg ──────────────────────────────────────────────────
  const form = new FormData();
  form.append("image_file", new Blob([imageBytes]), "avatar.jpg");
  form.append("size", "auto");
  form.append("format", "png");

  const bgRes = await fetch(REMOVE_BG_URL, {
    method: "POST",
    headers: { "X-Api-Key": apiKey },
    body: form,
  });

  if (!bgRes.ok) {
    const errText = await bgRes.text();
    console.error("remove.bg error:", bgRes.status, errText);
    return errorResponse(`Background removal failed: ${bgRes.status}`, 502);
  }

  // ── 3. Upload transparent PNG to avatars bucket ───────────────────────────
  const pngBytes = await bgRes.arrayBuffer();
  const filePath = `${employee_id}/podium.png`;

  const adminClient = createAdminClient();

  const { error: storageError } = await adminClient.storage
    .from("avatars")
    .upload(filePath, pngBytes, { contentType: "image/png", upsert: true });

  if (storageError) {
    console.error("Storage upload error:", storageError);
    return errorResponse("Failed to store processed image", 500);
  }

  // ── 4. Build public URL with cache-bust ───────────────────────────────────
  const { data: urlData } = adminClient.storage
    .from("avatars")
    .getPublicUrl(filePath);

  const podium_photo_url = `${urlData.publicUrl}?t=${Date.now()}`;

  // ── 5. Write podium_photo_url to employees record ─────────────────────────
  const { error: updateError } = await adminClient
    .from("employees")
    .update({ podium_photo_url })
    .eq("id", employee_id);

  if (updateError) {
    console.error("Employee update error:", updateError);
    return errorResponse("Failed to update employee record", 500);
  }

  return jsonResponse({ ok: true, podium_photo_url });
});
