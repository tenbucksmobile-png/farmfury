import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { commentsQuery, addComment, deleteComment } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export function useComments(recognitionId: string) {
  return useQuery({
    queryKey: QUERY_KEYS.comments(recognitionId),
    queryFn: async () => {
      const { data, error } = await commentsQuery(recognitionId);
      if (error) throw error;
      return data ?? [];
    },
    staleTime: 60 * 1000,
  });
}

export function useAddComment(recognitionId: string) {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: async (body: string) => {
      if (!employee) throw new Error('Not authenticated');
      const { error } = await addComment(recognitionId, employee.hotel, body);
      if (error) throw error;
    },
    onMutate: async (body) => {
      await queryClient.cancelQueries({ queryKey: QUERY_KEYS.comments(recognitionId) });
      const previous = queryClient.getQueryData(QUERY_KEYS.comments(recognitionId));

      queryClient.setQueryData(QUERY_KEYS.comments(recognitionId), (old: any[]) => [
        ...(old || []),
        {
          id: `temp-${Date.now()}`,
          body,
          created_at: new Date().toISOString(),
          updated_at: new Date().toISOString(),
          user: {
            id: employee?.employee_id,
            full_name: employee?.full_name,
            display_name: null,
            avatar_url: null,
          },
        },
      ]);

      return { previous };
    },
    onError: (_err, _body, context) => {
      queryClient.setQueryData(QUERY_KEYS.comments(recognitionId), context?.previous);
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.comments(recognitionId) });
    },
  });
}

export function useDeleteComment(recognitionId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (commentId: string) => {
      const { error } = await deleteComment(commentId);
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.comments(recognitionId) });
    },
  });
}
