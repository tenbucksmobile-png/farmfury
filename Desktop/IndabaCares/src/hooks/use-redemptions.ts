import { useRef, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { RealtimeChannel } from '@supabase/supabase-js';
import {
  getRedemptions,
  approveRedemption,
  rejectRedemption,
  fulfillRedemption,
} from '@/api/reward-service';
import { REWARD_QUERY_KEYS } from '@/hooks/use-rewards';
import { useEmployee } from '@/providers/EmployeeContext';
import { supabase } from '@/lib/supabase';

// ─── Employee hooks ───────────────────────────────────────────────────────────

export function useRedemptions() {
  const { employee } = useEmployee();
  const queryClient  = useQueryClient();
  const channelRef   = useRef<RealtimeChannel | null>(null);

  const query = useQuery({
    queryKey:  ['redemptions', employee?.employee_id],
    queryFn:   () => getRedemptions(employee!.employee_id),
    enabled:   !!employee,
    staleTime: 2 * 60 * 1000,
  });

  // ── Realtime: invalidate on any status change for this employee's orders ───
  useEffect(() => {
    if (!employee) return;

    channelRef.current = supabase
      .channel(`redemptions:${employee.employee_id}`)
      .on(
        'postgres_changes',
        {
          event:  '*',
          schema: 'public',
          table:  'redemptions',
          filter: `employee_id=eq.${employee.employee_id}`,
        },
        () => {
          queryClient.invalidateQueries({
            queryKey: ['redemptions', employee.employee_id],
          });
          // Balance may have changed on approval / rejection
          queryClient.invalidateQueries({
            queryKey: REWARD_QUERY_KEYS.points(employee.employee_id),
          });
        },
      )
      .subscribe();

    return () => {
      channelRef.current?.unsubscribe();
      channelRef.current = null;
    };
  }, [employee?.employee_id, queryClient]);

  return query;
}

// ─── Admin hooks ──────────────────────────────────────────────────────────────

function useAdminMutation(
  fn: (id: string, extra?: string) => Promise<{ ok: boolean; error?: string }>,
) {
  const queryClient  = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: ({ redemptionId, reason }: { redemptionId: string; reason?: string }) =>
      fn(redemptionId, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['redemptions'] });
      if (employee) {
        queryClient.invalidateQueries({
          queryKey: REWARD_QUERY_KEYS.points(employee.employee_id),
        });
        queryClient.invalidateQueries({
          queryKey: REWARD_QUERY_KEYS.rewards(employee.hotel),
        });
      }
    },
  });
}

export function useApproveRedemption() {
  return useAdminMutation((id) => approveRedemption(id));
}

export function useRejectRedemption() {
  return useAdminMutation((id, reason) => rejectRedemption(id, reason));
}

export function useFulfillRedemption() {
  return useAdminMutation((id) => fulfillRedemption(id));
}
