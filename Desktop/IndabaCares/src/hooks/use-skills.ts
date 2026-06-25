import { useQuery, useMutation } from '@tanstack/react-query';
import { skillCategoriesQuery, mySkillScoresQuery, submitSkillRating } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';
import { useUIStore } from '@/stores/ui-store';

export function useSkillCategories() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: [...QUERY_KEYS.skillCategories, employee?.hotel],
    queryFn: async () => {
      if (!employee) return [];
      const { data, error } = await skillCategoriesQuery(employee.hotel);
      if (error) throw error;
      return data ?? [];
    },
    enabled: !!employee,
    staleTime: Infinity,
  });
}

export function useMySkillScores() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: ['my-skill-scores', employee?.employee_id],
    queryFn: async () => {
      if (!employee) return [];
      const { data, error } = await mySkillScoresQuery(employee.employee_id);
      if (error) throw error;
      return data ?? [];
    },
    enabled: !!employee,
    staleTime: 5 * 60 * 1000,
  });
}

export function useSubmitSkillRating() {
  const { employee } = useEmployee();
  const showToast = useUIStore((s) => s.showToast);

  return useMutation({
    mutationFn: async ({
      recipientId,
      ratings,
    }: {
      recipientId: string;
      ratings: Array<{ indicatorId: string; score: number }>;
    }) => {
      if (!employee) throw new Error('Not authenticated');
      const { error } = await submitSkillRating(employee.hotel, recipientId, ratings);
      if (error) throw error;
    },
    onSuccess: () => {
      showToast({ type: 'success', message: 'Skill ratings submitted!' });
    },
    onError: (error: Error) => {
      showToast({ type: 'error', message: error.message });
    },
  });
}
