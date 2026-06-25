import { useQuery } from '@tanstack/react-query';
import { getMonthlyLegends } from '@/api/legends-service';
import { useEmployee } from '@/providers/EmployeeContext';

export function useMonthlyLegends(year: number) {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: ['monthly-legends', employee?.hotel ?? '', year],
    queryFn:  () => getMonthlyLegends(employee!.hotel, year),
    enabled:  !!employee,
    staleTime: 10 * 60 * 1000,
  });
}
