import { useQuery } from '@tanstack/react-query';
import { getTeamByDepartment, getDepartments } from '@/api/team-service';

/**
 * Fetch distinct departments for the given hotel.
 * The caller passes the correct hotel — either employee.hotel for regular
 * users, or the APA-chosen hotel for directors.
 */
export function useDepartments(hotel: string) {
  return useQuery({
    queryKey: ['departments', hotel],
    queryFn:  () => getDepartments(hotel),
    enabled:  !!hotel,
    staleTime: 10 * 60 * 1000,
  });
}

/**
 * Fetch all active employees in a department for the given hotel.
 * The caller passes the correct hotel.
 */
export function useTeamByDepartment(hotel: string, department: string) {
  return useQuery({
    queryKey: ['team', hotel, department],
    queryFn:  () => getTeamByDepartment(hotel, department),
    enabled:  !!hotel && !!department,
    staleTime: 5 * 60 * 1000,
  });
}
