import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { moodHistoryQuery } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';
import { useUIStore } from '@/stores/ui-store';
import { supabase } from '@/lib/supabase';
import type { SubmitMoodRequest } from '@/types/api';

export function useMoodHistory() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: [...QUERY_KEYS.moodHistory, employee?.employee_id],
    queryFn: async () => {
      if (!employee) return [];
      const { data, error } = await moodHistoryQuery(employee.employee_id);
      if (error) throw error;
      return data ?? [];
    },
    enabled: !!employee,
    staleTime: 30 * 60 * 1000,
  });
}

export function useSubmitMood() {
  const queryClient  = useQueryClient();
  const showToast    = useUIStore((s) => s.showToast);
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: async (body: SubmitMoodRequest) => {
      if (!employee) throw new Error('Not authenticated');

      const { data, error } = await supabase.rpc('submit_mood', {
        p_employee_id: employee.employee_id,
        p_hotel:       employee.hotel,
        p_mood:        body.mood,
        p_note:        body.note ?? null,
      });

      if (error) {
        if (error.code === 'P3001' || error.code === '23505') {
          throw new Error("You've already submitted your mood today");
        }
        throw new Error(error.message);
      }

      return { moodEntryId: data, mood: body.mood, message: 'Mood recorded!', pointsEarned: 5 };
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.moodHistory });
      if (employee) {
        queryClient.invalidateQueries({ queryKey: QUERY_KEYS.employeeProfile(employee.employee_id) });
      }
      showToast({ type: 'success', message: `${data.message} (+${data.pointsEarned} pts)` });
    },
    onError: (error: Error) => {
      showToast({ type: 'error', message: error.message });
    },
  });
}
