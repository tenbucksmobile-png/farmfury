/**
 * useReactionRealtime
 *
 * Opens a single Supabase Realtime channel for the authenticated employee's
 * hotel and keeps the TanStack Query cache in sync as reactions are inserted
 * or deleted by any employee in the same hotel.
 *
 * Architecture
 * ────────────
 * • One channel per hotel, not one per recognition card.
 *   The channel filter `hotel=eq.<hotel>` is applied server-side by the
 *   Realtime service so only relevant WAL events are delivered.
 *
 * • Hotel guard in the event handler provides a second layer of defence
 *   against any event that might have slipped through the server filter.
 *
 * • INSERT events: append the new ReactionRow directly to the cached array
 *   for the relevant recognition.  Skips rows already present (optimistic
 *   updates from the local user are already in the cache).
 *
 * • DELETE events: remove the row by id from the cached array.
 *   Requires REPLICA IDENTITY FULL on the table (migration 042) so the
 *   DELETE payload includes recognition_id alongside id.
 *
 * • Reaction balance invalidation: when the current employee's own reaction
 *   changes (cross-device sync), the balance cache is refreshed.
 *
 * Usage
 * ────────────
 * Call once in the feed screen (or any layout component that mounts while the
 * feed is visible).  Do NOT call inside individual recognition cards — that
 * would open N channels for N cards.
 */

import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { useEmployee } from '@/providers/EmployeeContext';
import { QUERY_KEYS } from '@/lib/constants';
import type { ReactionType, ReactionRow } from '@/hooks/use-recognition-reactions';

// ─── Realtime payload shapes ──────────────────────────────────────────────────

interface ReactionInsertPayload {
  id:             string;
  recognition_id: string;
  employee_id:    string;
  reaction_type:  string;
  hotel:          string;
  created_at:     string;
}

interface ReactionDeletePayload {
  id:             string;
  recognition_id: string; // available because of REPLICA IDENTITY FULL
  employee_id:    string;
  reaction_type:  string;
  hotel:          string;
  created_at:     string;
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

export function useReactionRealtime() {
  const { employee }  = useEmployee();
  const queryClient   = useQueryClient();

  useEffect(() => {
    if (!employee) return;

    const hotel      = employee.hotel;
    const employeeId = employee.employee_id;

    const channel = supabase
      .channel(`reaction-sync:${hotel}`)
      .on(
        'postgres_changes',
        {
          event:  'INSERT',
          schema: 'public',
          table:  'recognition_reactions',
          // Server-side filter — only events matching this hotel are delivered.
          filter: `hotel=eq.${hotel}`,
        },
        (payload) => {
          const row = payload.new as ReactionInsertPayload;

          // Client-side hotel guard (second layer of defence).
          if (row.hotel !== hotel) return;

          const key = QUERY_KEYS.recognitionReactions(row.recognition_id);

          queryClient.setQueryData<ReactionRow[]>(key, (prev = []) => {
            // The local user's own INSERT is already in the cache via the
            // optimistic update in useSubmitReaction; skip to avoid duplicates.
            if (prev.some((r) => r.id === row.id)) return prev;

            return [
              ...prev,
              {
                id:            row.id,
                employee_id:   row.employee_id,
                reaction_type: row.reaction_type as ReactionType,
                created_at:    row.created_at,
              },
            ];
          });

          // If this INSERT came from the current employee on another device,
          // refresh their reaction balance so it stays accurate.
          if (row.employee_id === employeeId) {
            queryClient.invalidateQueries({
              queryKey: QUERY_KEYS.reactionBalance(employeeId),
            });
          }
        },
      )
      .on(
        'postgres_changes',
        {
          event:  'DELETE',
          schema: 'public',
          table:  'recognition_reactions',
          // DELETE filter uses the old row's hotel value.
          // REPLICA IDENTITY FULL (migration 042) makes this available in WAL.
          filter: `hotel=eq.${hotel}`,
        },
        (payload) => {
          const row = payload.old as ReactionDeletePayload;

          // Client-side hotel guard.
          if (row.hotel !== hotel) return;

          const key = QUERY_KEYS.recognitionReactions(row.recognition_id);

          queryClient.setQueryData<ReactionRow[]>(key, (prev = []) =>
            prev.filter((r) => r.id !== row.id),
          );

          // Refresh balance if it was the current employee's reaction that
          // was removed from another device.
          if (row.employee_id === employeeId) {
            queryClient.invalidateQueries({
              queryKey: QUERY_KEYS.reactionBalance(employeeId),
            });
          }
        },
      )
      .subscribe();

    return () => {
      supabase.removeChannel(channel);
    };
  }, [employee?.hotel, employee?.employee_id, queryClient]);
}
