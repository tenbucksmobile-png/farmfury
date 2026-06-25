/**
 * Notification dispatch helper.
 *
 * Writes rows to public.notifications (migration 023 schema):
 *   employee_id, hotel, type, title, message, read, created_at
 *
 * Realtime delivery: the notifications table is in the supabase_realtime
 * publication.  Clients subscribed to the hotel channel receive INSERTs
 * filtered by the notifications_own_select RLS policy.
 *
 * NOTE: notifyAdmins / notifyManager have been removed — they queried the
 * profiles table which was dropped in migration 030.  Admin alerts should
 * be handled via the admin panel using the service_role key directly.
 */

import type { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";

// Types aligned with the notifications.type CHECK constraint (migration 023).
type NotificationType =
  | "recognition_received"
  | "reward_approved"
  | "reward_rejected"
  | "admin_announcement";

interface NotificationPayload {
  employeeId:     string;   // recipient — maps to notifications.employee_id
  hotel:          string;   // tenant scope
  type:           NotificationType;
  title:          string;
  message?:       string;
  referenceType?: string;   // e.g. 'recognition', 'redemption'
  referenceId?:   string;
}

/**
 * Create a single in-app notification for an employee.
 */
export async function notify(
  adminClient: SupabaseClient,
  payload: NotificationPayload
): Promise<void> {
  const { error } = await adminClient.from("notifications").insert({
    employee_id:    payload.employeeId,
    hotel:          payload.hotel,
    type:           payload.type,
    title:          payload.title,
    message:        payload.message   ?? null,
    reference_type: payload.referenceType ?? null,
    reference_id:   payload.referenceId   ?? null,
    read:           false,
  });

  if (error) {
    console.error(`Notification failed for employee ${payload.employeeId}:`, error);
  }
}

/**
 * Create notifications for multiple employees at once.
 * Uses a single INSERT for efficiency.
 */
export async function notifyMany(
  adminClient: SupabaseClient,
  payloads: NotificationPayload[]
): Promise<void> {
  if (payloads.length === 0) return;

  const rows = payloads.map((p) => ({
    employee_id:    p.employeeId,
    hotel:          p.hotel,
    type:           p.type,
    title:          p.title,
    message:        p.message         ?? null,
    reference_type: p.referenceType   ?? null,
    reference_id:   p.referenceId     ?? null,
    read:           false,
  }));

  const { error } = await adminClient.from("notifications").insert(rows);

  if (error) {
    console.error(`Bulk notification failed (${rows.length} rows):`, error);
  }
}
