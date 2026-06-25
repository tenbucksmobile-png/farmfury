/**
 * Leaderboard Service
 *
 * Fetches live leaderboard data from get_leaderboard() — a Postgres function
 * that aggregates points_ledger within an optional time window.
 *
 * Badge levels are computed client-side from the employee's all-time
 * points_balance so ranks can shift per-period while badges remain stable.
 */

import { supabase } from '@/lib/supabase';
import { fetchWithGuards } from '@/lib/api-client';

// ─── Types ────────────────────────────────────────────────────────────────────

export type PeriodType = 'all_time' | 'monthly' | 'quarterly' | 'annual';

export interface LeaderboardEntry {
  rank:            number;
  employee_id:     string;
  full_name:       string;
  employee_code:   string;
  total_points:    number;
  points_balance:  number;  // all-time, used for badge level
  job_title?:         string;
  avatar_url?:        string | null;
  podium_photo_url?:  string | null;
  movement_delta?:    number;  // positive = moved up, negative = moved down
  is_manager?:        boolean;
}

export interface BadgeLevel {
  label: string;
  emoji: string;
  color: string;
  minPoints: number;
}

// ─── Badge level ladder ───────────────────────────────────────────────────────

export const BADGE_LEVELS: BadgeLevel[] = [
  { label: 'Legend',      emoji: '👑', color: '#ED6813', minPoints: 500 },
  { label: 'Champion',    emoji: '🏆', color: '#f59e0b', minPoints: 200 },
  { label: 'Star Player', emoji: '🌟', color: '#3b82f6', minPoints: 100 },
  { label: 'Rising Star', emoji: '⭐', color: '#22c55e', minPoints:  50 },
  { label: 'Newcomer',    emoji: '🌱', color: '#94a3b8', minPoints:   0 },
];

/** Returns the badge level for a given all-time points balance. */
export function getBadgeLevel(points: number): BadgeLevel {
  return (
    BADGE_LEVELS.find((b) => points >= b.minPoints) ??
    BADGE_LEVELS[BADGE_LEVELS.length - 1]
  );
}

// ─── Period helpers ───────────────────────────────────────────────────────────

export const PERIOD_LABELS: Record<PeriodType, string> = {
  all_time:  'All Time',
  monthly:   'Employees',
  quarterly: 'Legends',
  annual:    'Management',
};

// Tabs shown in the UI — Employees, Management, Legends
export const PERIOD_TABS: PeriodType[] = ['monthly', 'annual', 'quarterly'];

function getPeriodRange(period: PeriodType): { start: string | null; end: string | null } {
  if (period === 'all_time') return { start: null, end: null };

  const now   = new Date();
  const year  = now.getFullYear();
  const month = now.getMonth(); // 0-indexed

  if (period === 'monthly') {
    return { start: new Date(year, month, 1).toISOString(), end: null };
  }

  if (period === 'quarterly') {
    const qStart = Math.floor(month / 3) * 3;
    return { start: new Date(year, qStart, 1).toISOString(), end: null };
  }

  // annual
  return { start: new Date(year, 0, 1).toISOString(), end: null };
}

// ─── Query ────────────────────────────────────────────────────────────────────

/**
 * Fetch the leaderboard for a hotel and period.
 * Calls the get_leaderboard() SECURITY DEFINER RPC.
 */
export async function getLeaderboard(
  hotel:  string,
  period: PeriodType = 'monthly',
  limit   = 50,
): Promise<LeaderboardEntry[]> {
  const { start, end } = getPeriodRange(period);

  const { data, error } = await fetchWithGuards(
    async () => supabase.rpc('get_leaderboard', {
      p_hotel: hotel,
      p_start: start,
      p_end:   end,
      p_limit: limit,
    }),
    { timeoutMs: 12_000 },
  );

  if (error) throw new Error(error.message);
  return ((data ?? []) as unknown[]) as LeaderboardEntry[];
}
