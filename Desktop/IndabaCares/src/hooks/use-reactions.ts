import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { reactionsQuery, addReaction, removeReaction } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export function useReactions(recognitionId: string) {
  return useQuery({
    queryKey: QUERY_KEYS.reactions(recognitionId),
    queryFn: async () => {
      const { data, error } = await reactionsQuery(recognitionId);
      if (error) throw error;
      return data ?? [];
    },
    staleTime: 30 * 1000,
  });
}

export function useAddReaction(recognitionId: string) {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: async (emoji: string) => {
      if (!employee) throw new Error('Not authenticated');
      const { error } = await addReaction(recognitionId, employee.hotel, emoji);
      if (error) throw error;
    },
    onMutate: async (emoji) => {
      await queryClient.cancelQueries({ queryKey: QUERY_KEYS.reactions(recognitionId) });
      const previous = queryClient.getQueryData(QUERY_KEYS.reactions(recognitionId));

      queryClient.setQueryData(QUERY_KEYS.reactions(recognitionId), (old: any[]) => [
        ...(old || []),
        {
          id: `temp-${Date.now()}`,
          emoji,
          user_id: employee?.employee_id,
          created_at: new Date().toISOString(),
          user: { id: employee?.employee_id, full_name: employee?.full_name, avatar_url: null },
        },
      ]);

      return { previous };
    },
    onError: (_err, _emoji, context) => {
      queryClient.setQueryData(QUERY_KEYS.reactions(recognitionId), context?.previous);
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.reactions(recognitionId) });
    },
  });
}

export function useRemoveReaction(recognitionId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (reactionId: string) => {
      const { error } = await removeReaction(reactionId);
      if (error) throw error;
    },
    onMutate: async (reactionId) => {
      await queryClient.cancelQueries({ queryKey: QUERY_KEYS.reactions(recognitionId) });
      const previous = queryClient.getQueryData(QUERY_KEYS.reactions(recognitionId));

      queryClient.setQueryData(QUERY_KEYS.reactions(recognitionId), (old: any[]) =>
        (old || []).filter((r: any) => r.id !== reactionId)
      );

      return { previous };
    },
    onError: (_err, _id, context) => {
      queryClient.setQueryData(QUERY_KEYS.reactions(recognitionId), context?.previous);
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.reactions(recognitionId) });
    },
  });
}
