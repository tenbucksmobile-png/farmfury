import { useQuery } from '@tanstack/react-query';
import { getLeaderboard, type PeriodType } from '@/api/leaderboard-service';
import { useEmployee } from '@/providers/EmployeeContext';

export function useLeaderboard(hotel: string, period: PeriodType) {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: ['leaderboard', hotel, period],
    queryFn:  () => getLeaderboard(hotel, period),
    enabled:  !!employee && !!hotel,
    staleTime: 5 * 60 * 1000,
  });
}
