/**
 * Typed PostgREST query builders — hotel-scoped.
 *
 * All queries that return multi-tenant data now accept a `hotel` string
 * (from EmployeeContext) and filter rows with .eq('hotel', hotel).
 *
 * Queries scoped to a single employee (mood, redemptions, etc.) accept
 * `employeeId` (employee.employee_id from EmployeeContext).
 *
 * The admin dashboard has its own queries.ts and is unaffected.
 */

import { supabase } from '@/lib/supabase';
import { PAGE_SIZE } from '@/lib/constants';

// ─── Feed ─────────────────────────────────────────────────────────────────────

export const RECOGNITION_SELECT = `
  id, message, badge, hotel, created_at,
  recipient_response, recipient_responded_at,
  sender:employees!sender_id   ( id, full_name, employee_code, position, department, photo_url ),
  receiver:employees!receiver_id ( id, full_name, employee_code, position, department, photo_url ),
  likes_count:recognition_likes ( count ),
  comments_count:recognition_comments ( count )
` as const;

export interface RecognitionFeedItem {
  id: string;
  message: string;
  badge: string;
  hotel: string;
  created_at: string;
  recipient_response:     string | null;
  recipient_responded_at: string | null;
  sender:   { id: string; full_name: string; employee_code: string; position: string | null; department: string | null; photo_url: string | null };
  receiver: { id: string; full_name: string; employee_code: string; position: string | null; department: string | null; photo_url: string | null };
  likes_count:    Array<{ count: number }>;
  comments_count: Array<{ count: number }>;
}

/** Paginated feed scoped to the employee's hotel, newest first. */
export function feedQuery(hotel: string, cursor?: string) {
  let query = supabase
    .from('recognitions')
    .select(RECOGNITION_SELECT)
    .eq('hotel', hotel)
    .order('created_at', { ascending: false })
    .limit(PAGE_SIZE);

  if (cursor) {
    query = query.lt('created_at', cursor);
  }

  return query as any;
}

export function recognitionDetailQuery(id: string) {
  return supabase
    .from('recognitions')
    .select(RECOGNITION_SELECT)
    .eq('id', id)
    .single() as any;
}

/** Post a new recognition. Returns the inserted row. */
export function postRecognition(
  senderId: string,
  receiverId: string,
  message: string,
  badge: string,
  hotel: string,
  cardType: 'recognition' | 'skills' = 'recognition',
) {
  return (supabase.from('recognitions') as any)
    .insert({ sender_id: senderId, receiver_id: receiverId, message, badge, hotel, card_type: cardType })
    .select(RECOGNITION_SELECT)
    .single();
}

// ─── Likes ────────────────────────────────────────────────────────────────────

export function likesQuery(recognitionId: string) {
  return supabase
    .from('recognition_likes')
    .select('id, employee_id, created_at')
    .eq('recognition_id', recognitionId) as any;
}

export function addLike(recognitionId: string, employeeId: string, hotel: string) {
  return (supabase.from('recognition_likes') as any).insert({
    recognition_id: recognitionId,
    employee_id: employeeId,
    hotel,
  });
}

export function removeLike(likeId: string) {
  return supabase.from('recognition_likes').delete().eq('id', likeId) as any;
}

// ─── Comments ─────────────────────────────────────────────────────────────────

export function recognitionCommentsQuery(recognitionId: string) {
  return supabase
    .from('recognition_comments')
    .select('id, body, created_at, employee:employees!employee_id ( id, full_name, position )')
    .eq('recognition_id', recognitionId)
    .order('created_at', { ascending: true }) as any;
}

export function addRecognitionComment(
  recognitionId: string,
  employeeId: string,
  hotel: string,
  body: string,
) {
  return (supabase.from('recognition_comments') as any).insert({
    recognition_id: recognitionId,
    employee_id: employeeId,
    hotel,
    body,
  });
}

export function deleteRecognitionComment(commentId: string) {
  return supabase.from('recognition_comments').delete().eq('id', commentId) as any;
}

// ─── Legacy stubs (kept so old imports don't break during migration) ──────────
/** @deprecated Use likesQuery instead */
export const reactionsQuery = likesQuery;
/** @deprecated Use addLike instead */
export const addReaction = (recognitionId: string, hotel: string, _emoji: string) =>
  addLike(recognitionId, '', hotel);
/** @deprecated Use removeLike instead */
export const removeReaction = removeLike;
/** @deprecated Use recognitionCommentsQuery instead */
export const commentsQuery = recognitionCommentsQuery;
/** @deprecated Use addRecognitionComment instead */
export const addComment = (recognitionId: string, hotel: string, body: string) =>
  addRecognitionComment(recognitionId, '', hotel, body);
/** @deprecated Use deleteRecognitionComment instead */
export const deleteComment = deleteRecognitionComment;

// ─── Notifications ────────────────────────────────────────────────────────────

/** Notifications scoped to the logged-in employee. */
export function notificationsQuery(employeeId: string, limit = 50) {
  return supabase
    .from('notifications')
    .select('id, employee_id, title, message, type, read, hotel, created_at')
    .eq('employee_id', employeeId)
    .order('created_at', { ascending: false })
    .limit(limit) as any;
}

export function markNotificationRead(id: string) {
  return supabase.rpc('mark_notification_read', { p_id: id }) as any;
}

export function markAllNotificationsRead(employeeId: string) {
  return supabase.rpc('mark_all_notifications_read', {
    p_employee_id: employeeId,
  }) as any;
}

// leaderboardQuery removed — leaderboard_cache was dropped in migration 030.
// Use the get_leaderboard() RPC via leaderboard-service.ts instead.

// ─── Rewards ──────────────────────────────────────────────────────────────────

/** Rewards scoped to the employee's hotel, newest first. */
export function rewardsQuery(hotel: string) {
  return supabase
    .from('rewards')
    .select('id, title, description, points_required, image_url, hotel, stock, created_at')
    .eq('hotel', hotel)
    .order('created_at', { ascending: false }) as any;
}

export function rewardDetailQuery(id: string) {
  return supabase
    .from('rewards')
    .select('id, title, description, points_required, image_url, hotel, stock, created_at')
    .eq('id', id)
    .single() as any;
}

// ─── Redemptions ──────────────────────────────────────────────────────────────

/** Redemptions for the authenticated employee. */
export function redemptionsQuery(employeeId: string) {
  return supabase
    .from('redemptions')
    .select(
      `id, points_used, status, hotel, created_at,
       approved_at, rejected_at, fulfilled_at, rejection_reason,
       reward:rewards ( id, title, image_url, points_required )`,
    )
    .eq('employee_id', employeeId)
    .order('created_at', { ascending: false }) as any;
}

/** Employee's current points balance. */
export function employeePointsQuery(employeeId: string) {
  return supabase
    .from('employees')
    .select('points_balance')
    .eq('id', employeeId)
    .single() as any;
}

// ─── Points Ledger ────────────────────────────────────────────────────────────

/**
 * Points ledger history for the authenticated employee.
 * Replaces star_transactions (System 1, dropped in migration 030).
 */
export function pointsLedgerQuery(employeeId: string, limit = 50) {
  return supabase
    .from('points_ledger')
    .select('id, points, source, hotel, created_at')
    .eq('employee_id', employeeId)
    .order('created_at', { ascending: false })
    .limit(limit) as any;
}

// ─── Mood ─────────────────────────────────────────────────────────────────────

/** Mood history for the authenticated employee. */
export function moodHistoryQuery(employeeId: string, days = 30) {
  const since = new Date();
  since.setDate(since.getDate() - days);

  return supabase
    .from('mood_entries')
    .select('id, mood, entry_date, created_at')
    .eq('employee_id', employeeId)
    .gte('entry_date', since.toISOString().split('T')[0])
    .order('entry_date', { ascending: true }) as any;
}

// ─── Skills ───────────────────────────────────────────────────────────────────

/** Skill categories and indicators scoped to the employee's hotel. */
export function skillCategoriesQuery(hotel: string) {
  return supabase
    .from('skill_categories')
    .select('*, indicators:skill_indicators ( id, name, description, sort_order )')
    .eq('hotel', hotel)
    .order('sort_order', { ascending: true }) as any;
}

/** Skill scores received by the authenticated employee. */
export function mySkillScoresQuery(employeeId: string) {
  return supabase
    .from('skill_ratings')
    .select('indicator_id, score, indicator:skill_indicators ( id, name, category:skill_categories ( id, name ) )')
    .eq('recipient_id', employeeId) as any;
}

/** Submit skill ratings. hotel replaces the old company_id. */
export function submitSkillRating(
  hotel: string,
  recipientId: string,
  ratings: Array<{ indicatorId: string; score: number }>,
) {
  return (supabase.from('skill_ratings') as any).insert(
    ratings.map((r) => ({
      hotel,
      recipient_id: recipientId,
      indicator_id: r.indicatorId,
      score: r.score,
    }))
  );
}

// ─── Badges ───────────────────────────────────────────────────────────────────

/** Badges scoped to the employee's hotel (plus global badges with null hotel). */
export function badgesQuery(hotel: string) {
  return supabase
    .from('badges')
    .select('*')
    .or(`hotel.eq.${hotel},hotel.is.null`) as any;
}

/** Badges earned by a specific employee. */
export function userBadgesQuery(employeeId: string) {
  return supabase
    .from('user_badges')
    .select('*, badge:badges ( id, slug, name, description, icon )')
    .eq('employee_id', employeeId)
    .order('earned_at', { ascending: false }) as any;
}

// ─── Employees (search / profile) ────────────────────────────────────────────

/**
 * Search employees within the same hotel.
 * Replaces the old searchProfilesQuery which queried the `profiles` table
 * and scoped by company_id.
 */
export function searchEmployeesQuery(hotel: string, search: string) {
  return supabase
    .from('employees')
    .select('id, full_name, employee_code, hotel, position, department')
    .eq('hotel', hotel)
    .eq('status', 'active')
    .ilike('full_name', `%${search}%`)
    .limit(20) as any;
}

export function employeeDetailQuery(id: string) {
  return supabase
    .from('employees')
    .select('id, full_name, employee_code, hotel, status')
    .eq('id', id)
    .single();
}

export function updateEmployeeProfile(
  id: string,
  data: { full_name?: string; hotel?: string },
) {
  return supabase.from('employees').update(data).eq('id', id);
}

// ─── Recognition Reactions ────────────────────────────────────────────────────

export function recognitionReactionsQuery(recognitionId: string) {
  return supabase
    .from('recognition_reactions')
    .select('id, employee_id, reaction_type, created_at')
    .eq('recognition_id', recognitionId) as any;
}

export function deleteReaction(reactionId: string) {
  return supabase
    .from('recognition_reactions')
    .delete()
    .eq('id', reactionId) as any;
}

// ─── Thumbs Up Types ──────────────────────────────────────────────────────────

/** Recognition types scoped to the employee's hotel. */
export function thumbsUpTypesQuery(hotel: string) {
  return supabase
    .from('thumbs_up_types')
    .select('*')
    .eq('hotel', hotel)
    .eq('is_active', true)
    .order('sort_order', { ascending: true }) as any;
}
