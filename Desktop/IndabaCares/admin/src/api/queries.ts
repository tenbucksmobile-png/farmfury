import { createClient } from '@/lib/supabase/client';
import { PAGE_SIZE } from '@/lib/constants';

function supabase() {
  return createClient();
}

// ─── Users ──────────────────────────────────────────────────────────────────

export function usersListQuery(params: {
  search?: string;
  role?: string;
  departmentId?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}) {
  const { search, role, departmentId, isActive, page = 0, pageSize = PAGE_SIZE } = params;
  const from = page * pageSize;
  const to = from + pageSize - 1;

  let query = supabase()
    .from('profiles')
    .select(
      'id, email, full_name, display_name, avatar_url, role, department_id, job_title, points_balance, stars_balance, giving_balance, is_active, created_at, departments:department_id ( id, name )',
      { count: 'exact' }
    )
    .order('created_at', { ascending: false })
    .range(from, to) as any;

  if (search) query = query.or(`full_name.ilike.%${search}%,email.ilike.%${search}%`);
  if (role) query = query.eq('role', role);
  if (departmentId) query = query.eq('department_id', departmentId);
  if (typeof isActive === 'boolean') query = query.eq('is_active', isActive);

  return query;
}

export function userDetailQuery(id: string) {
  return supabase()
    .from('profiles')
    .select('*, departments:department_id ( id, name )')
    .eq('id', id)
    .single() as any;
}

// ─── Departments ────────────────────────────────────────────────────────────

export function departmentsQuery() {
  return supabase()
    .from('departments')
    .select('id, name, parent_id, created_at')
    .order('name', { ascending: true }) as any;
}

// ─── Recognition Analytics ──────────────────────────────────────────────────

export function recognitionsTimeSeriesQuery(dateFrom: string, dateTo: string) {
  return supabase()
    .from('recognitions')
    .select('id, created_at, is_boosted, thumbs_up_type_id, stars_per_recipient, sender_id')
    .gte('created_at', dateFrom)
    .lte('created_at', dateTo)
    .order('created_at', { ascending: true }) as any;
}

export function recognitionRecipientsQuery(dateFrom: string, dateTo: string) {
  return supabase()
    .from('recognition_recipients')
    .select('recipient_id, recognition:recognitions!inner ( created_at )')
    .gte('recognition.created_at', dateFrom)
    .lte('recognition.created_at', dateTo) as any;
}

export function topSendersQuery(dateFrom: string, dateTo: string) {
  return supabase()
    .from('recognitions')
    .select('sender_id, sender:profiles!sender_id ( id, full_name, avatar_url )')
    .gte('created_at', dateFrom)
    .lte('created_at', dateTo) as any;
}

// ─── Mood Analytics ─────────────────────────────────────────────────────────

export function happinessScoresQuery(dateFrom: string, dateTo: string) {
  return supabase()
    .from('happiness_scores')
    .select('*')
    .gte('entry_date', dateFrom)
    .lte('entry_date', dateTo)
    .order('entry_date', { ascending: true }) as any;
}

export function moodEntriesQuery(dateFrom: string, dateTo: string) {
  return supabase()
    .from('mood_entries')
    .select('id, mood, entry_date, user_id')
    .gte('entry_date', dateFrom)
    .lte('entry_date', dateTo) as any;
}

export function activeUserCountQuery() {
  return supabase()
    .from('profiles')
    .select('id', { count: 'exact', head: true })
    .eq('is_active', true) as any;
}

// ─── Gamification Config ────────────────────────────────────────────────────

export function thumbsUpTypesAdminQuery() {
  return supabase()
    .from('thumbs_up_types')
    .select('*')
    .order('sort_order', { ascending: true }) as any;
}

export function companyValuesAdminQuery() {
  return supabase()
    .from('company_values')
    .select('*')
    .order('sort_order', { ascending: true }) as any;
}

export function badgesAdminQuery() {
  return supabase()
    .from('badges')
    .select('*')
    .order('created_at', { ascending: false }) as any;
}

export function skillCategoriesAdminQuery() {
  return supabase()
    .from('skill_categories')
    .select('*, indicators:skill_indicators ( id, name, description, sort_order, is_active )')
    .order('sort_order', { ascending: true }) as any;
}

export function budgetConfigsQuery() {
  return supabase()
    .from('budget_configs')
    .select('*, department:departments ( id, name )')
    .order('effective_from', { ascending: false }) as any;
}

// ─── Rewards ────────────────────────────────────────────────────────────────

export function rewardsAdminQuery() {
  return supabase()
    .from('rewards')
    .select('*, category:reward_categories ( id, name )')
    .order('sort_order', { ascending: true }) as any;
}

export function rewardCategoriesAdminQuery() {
  return supabase()
    .from('reward_categories')
    .select('*')
    .order('sort_order', { ascending: true }) as any;
}

export function redemptionsAdminQuery(params: {
  status?: string;
  page?: number;
  pageSize?: number;
}) {
  const { status, page = 0, pageSize = PAGE_SIZE } = params;
  const from = page * pageSize;
  const to = from + pageSize - 1;

  let query = supabase()
    .from('redemptions')
    .select(
      `id, points_used, status, rejection_reason, hotel, created_at, approved_at, rejected_at, fulfilled_at,
       employee:employees!employee_id ( id, full_name, photo_url, employee_code ),
       reward:rewards!reward_id ( id, title, image_url, points_required )`,
      { count: 'exact' }
    )
    .order('created_at', { ascending: false })
    .range(from, to) as any;

  if (status && status !== 'all') query = query.eq('status', status);
  return query;
}

// ─── Audit Logs ─────────────────────────────────────────────────────────────

export function auditLogsQuery(params: {
  action?: string;
  actorId?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}) {
  const { action, actorId, dateFrom, dateTo, page = 0, pageSize = 50 } = params;
  const from = page * pageSize;
  const to = from + pageSize - 1;

  let query = supabase()
    .from('audit_logs')
    .select(
      `id, action, target_type, target_id, metadata, ip_address, user_agent, created_at,
       actor:profiles!actor_id ( id, full_name, email )`,
      { count: 'exact' }
    )
    .order('created_at', { ascending: false })
    .range(from, to) as any;

  if (action) query = query.ilike('action', `%${action}%`);
  if (actorId) query = query.eq('actor_id', actorId);
  if (dateFrom) query = query.gte('created_at', dateFrom);
  if (dateTo) query = query.lte('created_at', dateTo);

  return query;
}
