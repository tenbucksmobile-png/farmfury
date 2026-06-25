/**
 * useChat
 *
 * Provides message history, pagination, and a send() function for hotel chat.
 *
 * - Initial page fetched on mount via get_chat_messages() RPC.
 * - loadMore() fetches the next page using the oldest visible message as cursor.
 * - New messages arrive via Supabase Realtime (postgres_changes INSERT).
 * - Sending uses an optimistic update: the message appears immediately
 *   with a temp ID, then is replaced by the server-confirmed row.
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import type { RealtimeChannel } from '@supabase/supabase-js';
import {
  getMessages,
  sendMessage,
  subscribeToMessages,
  CHAT_PAGE_SIZE,
  type ChatMessage,
} from '@/api/chat-service';
import { useEmployee } from '@/providers/EmployeeContext';

export function useChat() {
  const { employee } = useEmployee();
  const hotel = employee?.hotel ?? '';

  const [messages,   setMessages]   = useState<ChatMessage[]>([]);
  const [isLoading,  setIsLoading]  = useState(true);
  const [isSending,  setIsSending]  = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [hasMore,    setHasMore]    = useState(true);
  const [error,      setError]      = useState<string | null>(null);

  const channelRef = useRef<RealtimeChannel | null>(null);

  // ── Initial load ────────────────────────────────────────────────────────────

  useEffect(() => {
    if (!hotel) return;

    setIsLoading(true);
    setError(null);
    setHasMore(true);

    getMessages(hotel, CHAT_PAGE_SIZE)
      .then((msgs) => {
        setMessages(msgs);
        setHasMore(msgs.length >= CHAT_PAGE_SIZE);
      })
      .catch((e: Error) => setError(e.message))
      .finally(() => setIsLoading(false));
  }, [hotel]);

  // ── Realtime subscription ───────────────────────────────────────────────────

  useEffect(() => {
    if (!hotel) return;

    channelRef.current = subscribeToMessages(hotel, (newMsg) => {
      setMessages((prev) => {
        if (prev.some((m) => m.id === newMsg.id)) return prev;
        return [newMsg, ...prev];
      });
    });

    return () => {
      channelRef.current?.unsubscribe();
      channelRef.current = null;
    };
  }, [hotel]);

  // ── loadMore — cursor-based older-message pagination ───────────────────────

  const loadMore = useCallback(async () => {
    if (!hotel || isLoadingMore || !hasMore) return;

    // Oldest message currently shown is at the end of the array (newest-first list)
    const oldest = messages[messages.length - 1];
    if (!oldest) return;

    setIsLoadingMore(true);

    try {
      const older = await getMessages(hotel, CHAT_PAGE_SIZE, oldest.created_at);

      if (older.length === 0) {
        setHasMore(false);
        return;
      }

      setMessages((prev) => {
        const existingIds = new Set(prev.map((m) => m.id));
        const deduped = older.filter((m) => !existingIds.has(m.id));
        return [...prev, ...deduped];
      });

      setHasMore(older.length >= CHAT_PAGE_SIZE);
    } catch {
      // Non-fatal: user can try again
    } finally {
      setIsLoadingMore(false);
    }
  }, [hotel, messages, isLoadingMore, hasMore]);

  // ── send ────────────────────────────────────────────────────────────────────

  const send = useCallback(
    async (body: string): Promise<void> => {
      if (!employee || !body.trim() || isSending) return;

      const tempId = `optimistic-${Date.now()}`;
      const optimistic: ChatMessage = {
        id:         tempId,
        body:       body.trim(),
        hotel,
        created_at: new Date().toISOString(),
        sender: {
          id:            employee.employee_id,
          full_name:     employee.full_name,
          employee_code: employee.employee_code,
          position:      null,
        },
      };

      setMessages((prev) => [optimistic, ...prev]);
      setIsSending(true);

      try {
        const saved = await sendMessage(employee.employee_id, hotel, body);
        setMessages((prev) =>
          prev.map((m) => (m.id === tempId ? saved : m)),
        );
      } catch (e: any) {
        setMessages((prev) => prev.filter((m) => m.id !== tempId));
        throw e;
      } finally {
        setIsSending(false);
      }
    },
    [employee, hotel, isSending],
  );

  return {
    messages,
    isLoading,
    isSending,
    isLoadingMore,
    hasMore,
    error,
    send,
    loadMore,
    myId: employee?.employee_id ?? '',
  };
}
