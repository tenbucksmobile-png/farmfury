import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

/**
 * Returns the number of badges the current employee has earned.
 * Queries `user_badges` filtered by `user_id = employee.employee_id`.
 * RLS policy "user_badges_own_select" (migration 059) restricts access to
 * the authenticated employee's own rows.
 */
export function useUserBadges() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: QUERY_KEYS.userBadges(employee?.employee_id ?? ''),
    queryFn:  async (): Promise<number> => {
      if (!employee) return 0;

      const { count, error } = await supabase
        .from('user_badges')
        .select('id', { count: 'exact', head: true })
        .eq('user_id', employee.employee_id);

      if (error) throw error;
      return count ?? 0;
    },
    enabled:   !!employee,
    staleTime: 5 * 60 * 1000, // badges don't change frequently
  });
}
