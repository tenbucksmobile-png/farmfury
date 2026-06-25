import { useQuery } from '@tanstack/react-query';
import { badgesQuery, userBadgesQuery } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

/** All badges available to this hotel (plus global badges). */
export function useAllBadges() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: [...QUERY_KEYS.badges, employee?.hotel],
    queryFn: async () => {
      if (!employee) return [];
      const { data, error } = await badgesQuery(employee.hotel);
      if (error) throw error;
      return data ?? [];
    },
    enabled: !!employee,
    staleTime: Infinity,
  });
}

/** Badges earned by a specific employee. */
export function useUserBadges(employeeId: string) {
  return useQuery({
    queryKey: QUERY_KEYS.userBadges(employeeId),
    queryFn: async () => {
      const { data, error } = await userBadgesQuery(employeeId);
      if (error) throw error;
      return data ?? [];
    },
    staleTime: 5 * 60 * 1000,
  });
}
