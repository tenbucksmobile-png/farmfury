import { useEffect, useState, useRef, useCallback } from 'react';
import { AppState } from 'react-native';
import { useQueryClient } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { recognitionDetailQuery } from '@/api/queries';
import { useEmployee } from '@/providers/EmployeeContext';
import { useUIStore } from '@/stores/ui-store';
import { QUERY_KEYS } from '@/lib/constants';
import { notificationHaptic } from '@/lib/haptics';
import type { RealtimeChannel } from '@supabase/supabase-js';

/**
 * Global realtime subscriptions — scoped to the employee's hotel.
 * Mounted once in RealtimeProvider.
 *
 * Channels:
 *   feed-realtime        — INSERT on recognitions (hotel)   → prepend to feed cache
 *   notifications-rt     — INSERT on notifications (employee_id) → toast + badge count
 *   leaderboard-rt       — INSERT on points_ledger (hotel)  → invalidate leaderboard
 *
 * Guard: requires a valid EmployeeContext session.
 * All channel filters use values sourced from the validated session —
 * never from untrusted client input.
 */
export function useGlobalRealtime() {
  const queryClient           = useQueryClient();
  const { employee }          = useEmployee();
  const incrementNewFeedItems = useUIStore((s) => s.incrementNewFeedItems);
  const showToast             = useUIStore((s) => s.showToast);
  const setRealtimeStatus     = useUIStore((s) => s.setRealtimeStatus);

  const disconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // PERF-05: prevent duplicate subscriptions (React 19 StrictMode double-invoke)
  const subscribedRef = useRef(false);

  useEffect(() => {
    // ── Guard ────────────────────────────────────────────────────────────
    if (!employee) return;
    if (subscribedRef.current) return;
    subscribedRef.current = true;

    const { hotel, employee_id } = employee;

    // ── Channel 1: Feed (new recognitions in this hotel) ─────────────────
    const feedChannel = supabase
      .channel('feed-realtime')
      .on(
        'postgres_changes',
        {
          event:  'INSERT',
          schema: 'public',
          table:  'recognitions',
          filter: `hotel=eq.${hotel}`,
        },
        async (payload) => {
          // Ignore own sends — already in cache from the optimistic update
          if (payload.new.sender_id === employee_id) return;

          incrementNewFeedItems();

          try {
            const { data } = await recognitionDetailQuery(payload.new.id);
            if (data) {
              queryClient.setQueryData(QUERY_KEYS.feed, (old: any) => {
                if (!old?.pages?.[0]) return old;
                return {
                  ...old,
                  pages: [[data, ...old.pages[0]], ...old.pages.slice(1)],
                };
              });
            }
          } catch {
            // Non-fatal: the "new items" banner is already shown;
            // the user can pull-to-refresh to load the post.
          }
        }
      )
      .subscribe((status) => {
        switch (status) {
          case 'SUBSCRIBED':
            setRealtimeStatus('connected');
            if (disconnectTimerRef.current) {
              clearTimeout(disconnectTimerRef.current);
              disconnectTimerRef.current = null;
            }
            // Refresh stale data that may have arrived while disconnected
            queryClient.invalidateQueries({ queryKey: QUERY_KEYS.feed });
            queryClient.invalidateQueries({ queryKey: QUERY_KEYS.notifications });
            queryClient.invalidateQueries({ queryKey: ['leaderboard'], exact: false });
            break;

          case 'CHANNEL_ERROR':
          case 'TIMED_OUT':
            setRealtimeStatus('reconnecting');
            // Mark as fully disconnected after 30 s if no reconnect
            disconnectTimerRef.current = setTimeout(
              () => setRealtimeStatus('disconnected'),
              30_000
            );
            break;

          case 'CLOSED':
            setRealtimeStatus('disconnected');
            break;
        }
      });

    // ── Channel 2: Notifications for this employee ────────────────────────
    // Filtered by employee_id so the client only receives its own rows.
    // The notifications_own_select RLS policy enforces this at the DB layer too.
    const notifChannel = supabase
      .channel('notifications-realtime')
      .on(
        'postgres_changes',
        {
          event:  'INSERT',
          schema: 'public',
          table:  'notifications',
          filter: `employee_id=eq.${employee_id}`,
        },
        (payload) => {
          queryClient.invalidateQueries({ queryKey: QUERY_KEYS.notifications });
          showToast({ type: 'info', message: payload.new.title, duration: 4000 });
          if (AppState.currentState === 'active') {
            notificationHaptic();
          }
        }
      )
      .subscribe();

    // ── Channel 3: Leaderboard (points changes in this hotel) ─────────────
    // Listens to points_ledger INSERTs rather than leaderboard_cache
    // (leaderboard_cache was dropped in migration 030).
    // Any point award in the hotel may shift rankings — invalidate the
    // leaderboard query family so the next focus re-fetches.
    const leaderboardChannel = supabase
      .channel('leaderboard-realtime')
      .on(
        'postgres_changes',
        {
          event:  'INSERT',
          schema: 'public',
          table:  'points_ledger',
          filter: `hotel=eq.${hotel}`,
        },
        () => {
          queryClient.invalidateQueries({ queryKey: ['leaderboard'], exact: false });
        }
      )
      .subscribe();

    return () => {
      subscribedRef.current = false;
      if (disconnectTimerRef.current) {
        clearTimeout(disconnectTimerRef.current);
        disconnectTimerRef.current = null;
      }
      supabase.removeChannel(feedChannel);
      supabase.removeChannel(notifChannel);
      supabase.removeChannel(leaderboardChannel);
    };
  }, [employee?.employee_id, employee?.hotel]);
}

/**
 * Per-recognition realtime subscriptions (likes + comments + typing indicator).
 * Mounted on the recognition detail screen.
 *
 * Table references updated for migration 018:
 *   reactions  (dropped) → recognition_likes
 *   comments   (dropped) → recognition_comments
 */
export function useRecognitionRealtime(recognitionId: string) {
  const queryClient  = useQueryClient();
  const { employee } = useEmployee();

  const [typingUsers, setTypingUsers] = useState<
    Array<{ userId: string; fullName: string }>
  >([]);

  const typingTimers = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map());
  const channelRef   = useRef<RealtimeChannel | null>(null);

  useEffect(() => {
    if (!recognitionId) return;

    const channel = supabase
      .channel(`recognition-${recognitionId}`)
      // Likes (was: reactions table — dropped in migration 018)
      .on(
        'postgres_changes',
        {
          event:  '*',
          schema: 'public',
          table:  'recognition_likes',
          filter: `recognition_id=eq.${recognitionId}`,
        },
        () => {
          queryClient.invalidateQueries({
            queryKey: QUERY_KEYS.likes(recognitionId),
          });
        }
      )
      // Comments (was: comments table — dropped in migration 018)
      .on(
        'postgres_changes',
        {
          event:  '*',
          schema: 'public',
          table:  'recognition_comments',
          filter: `recognition_id=eq.${recognitionId}`,
        },
        () => {
          queryClient.invalidateQueries({
            queryKey: QUERY_KEYS.recognitionComments(recognitionId),
          });
        }
      )
      // Typing indicator broadcast (ephemeral — not persisted to DB)
      .on('broadcast', { event: 'typing' }, (payload) => {
        const { userId, fullName } = payload.payload as {
          userId:   string;
          fullName: string;
        };

        // Don't show the local employee's own typing indicator
        if (userId === employee?.employee_id) return;

        setTypingUsers((prev) => {
          if (prev.some((t) => t.userId === userId)) return prev;
          return [...prev, { userId, fullName }];
        });

        // Auto-clear typing indicator after 3 s of silence
        const existingTimer = typingTimers.current.get(userId);
        if (existingTimer) clearTimeout(existingTimer);

        const timer = setTimeout(() => {
          setTypingUsers((prev) => prev.filter((t) => t.userId !== userId));
          typingTimers.current.delete(userId);
        }, 3000);

        typingTimers.current.set(userId, timer);
      })
      .subscribe();

    channelRef.current = channel;

    return () => {
      typingTimers.current.forEach((timer) => clearTimeout(timer));
      typingTimers.current.clear();
      supabase.removeChannel(channel);
      channelRef.current = null;
    };
  }, [recognitionId, employee?.employee_id]);

  const sendTyping = useCallback(() => {
    if (!channelRef.current || !employee) return;
    channelRef.current.send({
      type:    'broadcast',
      event:   'typing',
      payload: {
        userId:   employee.employee_id,
        fullName: employee.full_name,
      },
    });
  }, [employee?.employee_id, employee?.full_name]);

  return { typingUsers, sendTyping };
}
