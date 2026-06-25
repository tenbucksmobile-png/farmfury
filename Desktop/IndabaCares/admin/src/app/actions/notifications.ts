'use server';

import { createAdminClient } from '@/lib/supabase/admin';
import { announcementSchema } from '@/lib/validation';

/**
 * Send an admin_announcement to every active employee in a hotel.
 * Calls notify_all() SECURITY DEFINER RPC (requires service_role key).
 * Returns the number of notifications created.
 */
export async function sendAnnouncement(raw: unknown): Promise<number> {
  const payload = announcementSchema.parse(raw);

  const db = createAdminClient();
  const { data, error } = await db.rpc('notify_all', {
    p_hotel:   payload.hotel,
    p_title:   payload.title,
    p_message: payload.message,
  });

  if (error) throw new Error(error.message);
  return (data as number) ?? 0;
}
