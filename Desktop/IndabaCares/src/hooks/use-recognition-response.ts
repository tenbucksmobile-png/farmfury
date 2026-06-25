import { useMutation, useQueryClient } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export const RESPONSE_OPTIONS = [
  'Thank you',
  'It is my pleasure',
  'Here to serve',
  'You are welcome',
];

export function useSubmitResponse(recognitionId: string) {
  const { employee } = useEmployee();
  const queryClient  = useQueryClient();

  return useMutation({
    mutationFn: async (response: string) => {
      const { data, error } = await supabase.rpc('submit_recognition_response', {
        p_recognition_id: recognitionId,
        p_employee_id:    employee?.employee_id,
        p_response:       response,
      });
      if (error) throw error;
      if (!data?.ok) throw new Error(data?.error ?? 'Failed to submit response.');
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.feed });
    },
  });
}
