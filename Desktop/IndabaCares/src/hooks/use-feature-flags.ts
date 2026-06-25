/**
 * Feature flags — Supabase-driven, per-hotel.
 *
 * Flags are stored in hotel_settings.feature_flags (JSONB).
 * If no row exists for the hotel, all flags default to true.
 * Cached for 5 minutes via React Query; falls back to all-true on error.
 */

import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { useEmployee } from '@/providers/EmployeeContext';

// ─── Types ────────────────────────────────────────────────────────────────────

export type FeatureFlag =
  | 'moods_enabled'
  | 'rewards_enabled'
  | 'skills_enabled'
  | 'leaderboards_enabled'
  | 'custom_hashtags_enabled'
  | 'boost_enabled';

const FLAG_DEFAULTS: Record<FeatureFlag, boolean> = {
  moods_enabled:           true,
  rewards_enabled:         true,
  skills_enabled:          true,
  leaderboards_enabled:    true,
  custom_hashtags_enabled: true,
  boost_enabled:           true,
};

// ─── Hook: useFeatureFlags ────────────────────────────────────────────────────

export function useFeatureFlags(): Record<FeatureFlag, boolean> {
  const { employee } = useEmployee();

  const { data } = useQuery({
    queryKey: ['feature-flags', employee?.hotel],
    queryFn:  async (): Promise<Record<FeatureFlag, boolean>> => {
      if (!employee?.hotel) return { ...FLAG_DEFAULTS };

      const { data, error } = await supabase.rpc('get_hotel_settings', {
        p_hotel: employee.hotel,
      });

      if (error || !data) return { ...FLAG_DEFAULTS };

      return {
        ...FLAG_DEFAULTS,
        ...(data as Record<string, boolean>),
      };
    },
    enabled:   !!employee?.hotel,
    staleTime: 5 * 60 * 1000,   // 5 minutes
    gcTime:    10 * 60 * 1000,  // keep in cache 10 minutes
  });

  return data ?? { ...FLAG_DEFAULTS };
}

export function useFeatureFlag(flag: FeatureFlag): boolean {
  const flags = useFeatureFlags();
  return flags[flag] ?? true;
}
