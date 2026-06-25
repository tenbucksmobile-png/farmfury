/**
 * Notification Service
 *
 * Typed queries for the notifications table (schema from migration 023).
 *
 * Columns: id, employee_id, title, message, type, read, hotel, created_at
 *
 * Types:
 *   recognition_received  — recognition trigger
 *   reward_approved       — redemption trigger
 *   reward_rejected       — redemption trigger
 *   admin_announcement    — notify_all() RPC
 */

import { supabase } from '@/lib/supabase';

// ─── Types ────────────────────────────────────────────────────────────────────

export type NotificationType =
  | 'recognition_received'
  | 'reward_approved'
  | 'reward_rejected'
  | 'admin_announcement';

export interface AppNotification {
  id:          string;
  employee_id: string;
  title:       string;
  message:     string | null;
  type:        NotificationType;
  read:        boolean;
  hotel:       string;
  created_at:  string;
}

// ─── Queries ──────────────────────────────────────────────────────────────────

/** Fetch all notifications for an employee, newest first. */
export async function getNotifications(
  employeeId: string,
  limit = 50,
): Promise<AppNotification[]> {
  const { data, error } = await supabase
    .from('notifications')
    .select('id, employee_id, title, message, type, read, hotel, created_at')
    .eq('employee_id', employeeId)
    .order('created_at', { ascending: false })
    .limit(limit);

  if (error) throw new Error(error.message);
  return (data ?? []) as AppNotification[];
}

/** Count unread notifications for an employee (head-only, no row transfer). */
export async function getUnreadCount(employeeId: string): Promise<number> {
  const { count, error } = await supabase
    .from('notifications')
    .select('*', { count: 'exact', head: true })
    .eq('employee_id', employeeId)
    .eq('read', false);

  if (error) return 0;
  return count ?? 0;
}

/** Mark a single notification as read via SECURITY DEFINER RPC. */
export async function markRead(id: string): Promise<void> {
  const { error } = await supabase.rpc('mark_notification_read', { p_id: id });
  if (error) throw new Error(error.message);
}

/** Mark all notifications for an employee as read. */
export async function markAllRead(employeeId: string): Promise<void> {
  const { error } = await supabase.rpc('mark_all_notifications_read', {
    p_employee_id: employeeId,
  });
  if (error) throw new Error(error.message);
}
