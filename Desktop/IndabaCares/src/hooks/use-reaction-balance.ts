import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ReactionBalance {
  hearts_remaining: number;
  smiles_remaining: number;
  thumbs_remaining: number;
  total_remaining:  number;
}

// Display totals reflect the proportional split of 100
export const REACTION_TOTALS = {
  heart:     34,
  smile:     33,
  thumbs_up: 33,
  total:     100,
} as const;

const DEFAULTS: ReactionBalance = {
  hearts_remaining: REACTION_TOTALS.heart,
  smiles_remaining: REACTION_TOTALS.smile,
  thumbs_remaining: REACTION_TOTALS.thumbs_up,
  total_remaining:  REACTION_TOTALS.total,
};

// ─── Hook ─────────────────────────────────────────────────────────────────────

export function useReactionBalance() {
  const { employee } = useEmployee();
  const now   = new Date();
  const month = now.getMonth() + 1;
  const year  = now.getFullYear();

  return useQuery({
    queryKey: QUERY_KEYS.reactionBalance(employee?.employee_id ?? ''),
    queryFn: async (): Promise<ReactionBalance> => {
      if (!employee) return DEFAULTS;

      const { data, error } = await supabase
        .from('employee_reaction_allocations')
        .select('hearts_remaining, smiles_remaining, thumbs_remaining, total_remaining')
        .eq('employee_id', employee.employee_id)
        .eq('month', month)
        .eq('year', year)
        .maybeSingle();

      if (error) throw error;
      return (data as ReactionBalance | null) ?? DEFAULTS;
    },
    enabled:   !!employee,
    staleTime: 30 * 1000,
  });
}
