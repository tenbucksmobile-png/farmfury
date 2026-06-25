import { useMutation, useQueryClient } from '@tanstack/react-query';
import { postRecognition } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';
import type { RecognitionBadge } from '@/lib/constants';

interface PostRecognitionInput {
  receiverId: string;
  message:    string;
  badge:      RecognitionBadge | string;
  cardType?:  'recognition' | 'skills';
}

export function usePostRecognition() {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: async ({ receiverId, message, badge, cardType = 'recognition' }: PostRecognitionInput) => {
      if (!employee) throw new Error('Not authenticated');
      const { data, error } = await postRecognition(
        employee.employee_id,
        receiverId,
        message,
        badge as string,
        employee.hotel,
        cardType,
      );
      if (error) throw error;
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.feed });
    },
  });
}
