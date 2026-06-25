/**
 * Notification hooks
 *
 * useNotifications      — full inbox list
 * useUnreadCount        — lightweight badge count (polls every 60 s)
 * useMarkRead           — mark single notification read
 * useMarkAllRead        — mark all read
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getNotifications,
  getUnreadCount,
  markRead,
  markAllRead,
} from '@/api/notification-service';
import { useEmployee } from '@/providers/EmployeeContext';

// ─── Query keys ───────────────────────────────────────────────────────────────

const NOTIF_KEY      = (id: string) => ['notifications', id] as const;
const UNREAD_KEY     = (id: string) => ['notifications', id, 'unread'] as const;

// ─── useNotifications ─────────────────────────────────────────────────────────

export function useNotifications() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: NOTIF_KEY(employee?.employee_id ?? ''),
    queryFn:  () => getNotifications(employee!.employee_id),
    enabled:  !!employee,
    staleTime: 30 * 1000,
  });
}

// ─── useUnreadCount ───────────────────────────────────────────────────────────

export function useUnreadCount(): number {
  const { employee } = useEmployee();

  const { data = 0 } = useQuery({
    queryKey:       UNREAD_KEY(employee?.employee_id ?? ''),
    queryFn:        () => getUnreadCount(employee!.employee_id),
    enabled:        !!employee,
    staleTime:      15 * 1000,
    refetchInterval: 60 * 1000,
  });

  return data;
}

// ─── useMarkRead ──────────────────────────────────────────────────────────────

export function useMarkRead() {
  const queryClient  = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: (id: string) => markRead(id),

    // Optimistic update: flip read flag in the list cache immediately
    onMutate: async (id: string) => {
      const key = NOTIF_KEY(employee?.employee_id ?? '');
      await queryClient.cancelQueries({ queryKey: key });
      const prev = queryClient.getQueryData(key);

      queryClient.setQueryData(key, (old: any[] = []) =>
        old.map((n) => (n.id === id ? { ...n, read: true } : n)),
      );

      return { prev, key };
    },

    onError: (_err, _id, ctx: any) => {
      if (ctx?.prev !== undefined) queryClient.setQueryData(ctx.key, ctx.prev);
    },

    onSettled: () => {
      if (!employee) return;
      queryClient.invalidateQueries({ queryKey: NOTIF_KEY(employee.employee_id) });
      queryClient.invalidateQueries({ queryKey: UNREAD_KEY(employee.employee_id) });
    },
  });
}

// ─── useMarkAllRead ───────────────────────────────────────────────────────────

export function useMarkAllRead() {
  const queryClient  = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: () => markAllRead(employee!.employee_id),

    onMutate: async () => {
      const key = NOTIF_KEY(employee?.employee_id ?? '');
      await queryClient.cancelQueries({ queryKey: key });
      const prev = queryClient.getQueryData(key);

      queryClient.setQueryData(key, (old: any[] = []) =>
        old.map((n) => ({ ...n, read: true })),
      );
      queryClient.setQueryData(UNREAD_KEY(employee?.employee_id ?? ''), 0);

      return { prev, key };
    },

    onError: (_err, _v, ctx: any) => {
      if (ctx?.prev !== undefined) queryClient.setQueryData(ctx.key, ctx.prev);
    },

    onSettled: () => {
      if (!employee) return;
      queryClient.invalidateQueries({ queryKey: NOTIF_KEY(employee.employee_id) });
      queryClient.invalidateQueries({ queryKey: UNREAD_KEY(employee.employee_id) });
    },
  });
}
