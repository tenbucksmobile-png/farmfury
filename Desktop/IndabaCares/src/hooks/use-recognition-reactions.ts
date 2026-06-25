import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { recognitionReactionsQuery, deleteReaction } from '@/api/queries';
import { supabase } from '@/lib/supabase';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

// ─── Types ────────────────────────────────────────────────────────────────────

export type ReactionType = 'heart' | 'smile' | 'thumbs_up';

export interface ReactionRow {
  id:            string;
  employee_id:   string;
  reaction_type: ReactionType;
  created_at:    string;
}

export interface ReactionCounts {
  heart:     number;
  smile:     number;
  thumbs_up: number;
}

// ─── useRecognitionReactions ──────────────────────────────────────────────────

export function useRecognitionReactions(recognitionId: string) {
  return useQuery({
    queryKey: QUERY_KEYS.recognitionReactions(recognitionId),
    queryFn:  async () => {
      const { data, error } = await recognitionReactionsQuery(recognitionId);
      if (error) throw error;
      return (data ?? []) as ReactionRow[];
    },
    staleTime: 30 * 1000,
  });
}

// ─── useSubmitReaction ────────────────────────────────────────────────────────
//
// Handles both add (via submit_recognition_reaction RPC) and remove (DELETE).
// Pressing the same reaction type the employee already has → toggles it off.
//
// Optimistic update:
//   Add  → append temp row immediately, revert on error
//   Remove → filter out the row immediately, revert on error

export function useSubmitReaction(recognitionId: string) {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();
  const key = QUERY_KEYS.recognitionReactions(recognitionId);

  return useMutation({
    mutationFn: async ({
      reactionType,
      existingId,
    }: {
      reactionType: ReactionType;
      existingId:   string | null;   // null = add, string = remove
    }) => {
      if (!employee) throw new Error('Not authenticated');

      if (existingId) {
        // Toggle off — DELETE triggers restore allocation + reverse points
        const { error } = await deleteReaction(existingId);
        if (error) throw error;
        return { action: 'removed' as const };
      }

      // Add via RPC (validates allocation, awards points)
      const { data, error } = await supabase.rpc('submit_recognition_reaction', {
        p_recognition_id: recognitionId,
        p_employee_id:    employee.employee_id,
        p_reaction_type:  reactionType,
      });
      if (error) throw error;
      if (!data?.ok) throw new Error(data?.error ?? 'Failed to submit reaction');
      return { action: 'added' as const, reactionId: data.reaction_id as string };
    },

    onMutate: async ({ reactionType, existingId }) => {
      await queryClient.cancelQueries({ queryKey: key });
      const previous = queryClient.getQueryData<ReactionRow[]>(key);

      if (existingId) {
        queryClient.setQueryData<ReactionRow[]>(key, (old = []) =>
          old.filter((r) => r.id !== existingId),
        );
      } else {
        queryClient.setQueryData<ReactionRow[]>(key, (old = []) => [
          ...old,
          {
            id:            `temp-${Date.now()}`,
            employee_id:   employee?.employee_id ?? '',
            reaction_type: reactionType,
            created_at:    new Date().toISOString(),
          },
        ]);
      }

      return { previous };
    },

    onError: (_err, _vars, context) => {
      queryClient.setQueryData(key, context?.previous);
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: key });
    },
  });
}
