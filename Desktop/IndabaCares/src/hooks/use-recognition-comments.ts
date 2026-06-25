import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  recognitionCommentsQuery,
  addRecognitionComment,
  deleteRecognitionComment,
} from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export interface RecognitionComment {
  id: string;
  body: string;
  created_at: string;
  employee: { id: string; full_name: string; position: string | null };
}

export function useRecognitionComments(recognitionId: string) {
  return useQuery({
    queryKey: QUERY_KEYS.recognitionComments(recognitionId),
    queryFn: async () => {
      const { data, error } = await recognitionCommentsQuery(recognitionId);
      if (error) throw error;
      return (data ?? []) as RecognitionComment[];
    },
    staleTime: 60 * 1000,
  });
}

export function useAddRecognitionComment(recognitionId: string) {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: async (body: string) => {
      if (!employee) throw new Error('Not authenticated');
      const { error } = await addRecognitionComment(
        recognitionId,
        employee.employee_id,
        employee.hotel,
        body,
      );
      if (error) throw error;
    },
    onMutate: async (body) => {
      await queryClient.cancelQueries({
        queryKey: QUERY_KEYS.recognitionComments(recognitionId),
      });
      const previous = queryClient.getQueryData<RecognitionComment[]>(
        QUERY_KEYS.recognitionComments(recognitionId),
      );

      queryClient.setQueryData<RecognitionComment[]>(
        QUERY_KEYS.recognitionComments(recognitionId),
        (old) => [
          ...(old ?? []),
          {
            id: `temp-${Date.now()}`,
            body,
            created_at: new Date().toISOString(),
            employee: {
              id: employee?.employee_id ?? '',
              full_name: employee?.full_name ?? '',
              position: null,
            },
          },
        ],
      );

      return { previous };
    },
    onError: (_err, _body, context) => {
      queryClient.setQueryData(
        QUERY_KEYS.recognitionComments(recognitionId),
        context?.previous,
      );
    },
    onSettled: () => {
      queryClient.invalidateQueries({
        queryKey: QUERY_KEYS.recognitionComments(recognitionId),
      });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.feed });
    },
  });
}

export function useDeleteRecognitionComment(recognitionId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (commentId: string) => {
      const { error } = await deleteRecognitionComment(commentId);
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: QUERY_KEYS.recognitionComments(recognitionId),
      });
    },
  });
}
