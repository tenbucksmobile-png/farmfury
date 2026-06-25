import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { searchEmployeesQuery, employeeDetailQuery, updateEmployeeProfile } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';
import { useUIStore } from '@/stores/ui-store';

/** Search employees within the same hotel. */
export function useSearchProfiles(search: string) {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: QUERY_KEYS.profiles(search),
    queryFn: async () => {
      if (!employee || !search) return [];
      const { data, error } = await searchEmployeesQuery(employee.hotel, search);
      if (error) throw error;
      return data ?? [];
    },
    enabled: !!employee && search.length >= 2,
    staleTime: 2 * 60 * 1000,
  });
}

export function useProfileDetail(id: string) {
  return useQuery({
    queryKey: QUERY_KEYS.profile(id),
    queryFn: async () => {
      const { data, error } = await employeeDetailQuery(id);
      if (error) throw error;
      return data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();
  const showToast = useUIStore((s) => s.showToast);

  return useMutation({
    mutationFn: async (data: { full_name?: string; hotel?: string }) => {
      if (!employee) throw new Error('Not authenticated');
      const { error } = await updateEmployeeProfile(employee.employee_id, data);
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.me });
      showToast({ type: 'success', message: 'Profile updated' });
    },
    onError: (error: Error) => {
      showToast({ type: 'error', message: error.message });
    },
  });
}
