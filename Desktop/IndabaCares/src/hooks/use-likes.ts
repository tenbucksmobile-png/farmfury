import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { likesQuery, addLike, removeLike } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export interface LikeRow {
  id: string;
  employee_id: string;
  created_at: string;
}

export function useLikes(recognitionId: string) {
  return useQuery({
    queryKey: QUERY_KEYS.likes(recognitionId),
    queryFn: async () => {
      const { data, error } = await likesQuery(recognitionId);
      if (error) throw error;
      return (data ?? []) as LikeRow[];
    },
    staleTime: 30 * 1000,
  });
}

/**
 * Returns { liked, likeId, toggle }.
 * Call toggle() to add or remove the current employee's like.
 */
export function useToggleLike(recognitionId: string) {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();

  const toggleMutation = useMutation({
    mutationFn: async ({ likeId }: { likeId: string | null }) => {
      if (!employee) throw new Error('Not authenticated');
      if (likeId) {
        const { error } = await removeLike(likeId);
        if (error) throw error;
      } else {
        const { error } = await addLike(recognitionId, employee.employee_id, employee.hotel);
        if (error) throw error;
      }
    },
    onMutate: async ({ likeId }) => {
      await queryClient.cancelQueries({ queryKey: QUERY_KEYS.likes(recognitionId) });
      const previous = queryClient.getQueryData<LikeRow[]>(QUERY_KEYS.likes(recognitionId));

      if (likeId) {
        queryClient.setQueryData<LikeRow[]>(QUERY_KEYS.likes(recognitionId), (old) =>
          (old ?? []).filter((l) => l.id !== likeId),
        );
      } else {
        queryClient.setQueryData<LikeRow[]>(QUERY_KEYS.likes(recognitionId), (old) => [
          ...(old ?? []),
          {
            id: `temp-${Date.now()}`,
            employee_id: employee?.employee_id ?? '',
            created_at: new Date().toISOString(),
          },
        ]);
      }

      return { previous };
    },
    onError: (_err, _vars, context) => {
      queryClient.setQueryData(QUERY_KEYS.likes(recognitionId), context?.previous);
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.likes(recognitionId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.feed });
    },
  });

  return toggleMutation;
}
