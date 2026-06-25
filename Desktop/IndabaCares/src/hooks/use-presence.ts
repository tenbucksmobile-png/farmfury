import { useEffect, useMemo } from 'react';
import { supabase } from '@/lib/supabase';
import { useEmployee } from '@/providers/EmployeeContext';
import { useUIStore } from '@/stores/ui-store';

/**
 * Tracks online presence for the current hotel.
 * Mounted once in RealtimeProvider.
 * Channel key: hotel name — so only employees at the same property see each other.
 */
export function usePresence() {
  const { employee }   = useEmployee();
  const setOnlineUsers = useUIStore((s) => s.setOnlineUsers);

  useEffect(() => {
    if (!employee) return;

    const { hotel, employee_id } = employee;

    const channel = supabase.channel(`presence:${hotel}`, {
      config: { presence: { key: employee_id } },
    });

    channel
      .on('presence', { event: 'sync' }, () => {
        const state   = channel.presenceState();
        const userIds = new Set<string>(Object.keys(state));
        setOnlineUsers(userIds);
      })
      .subscribe(async (status) => {
        if (status === 'SUBSCRIBED') {
          await channel.track({ employeeId: employee_id });
        }
      });

    return () => {
      supabase.removeChannel(channel);
    };
  }, [employee?.employee_id, employee?.hotel]);
}

/**
 * Check if a specific employee is currently online.
 */
export function useIsOnline(employeeId: string | undefined): boolean {
  const onlineUsers = useUIStore((s) => s.onlineUsers);
  return useMemo(() => {
    if (!employeeId) return false;
    return onlineUsers.has(employeeId);
  }, [onlineUsers, employeeId]);
}
