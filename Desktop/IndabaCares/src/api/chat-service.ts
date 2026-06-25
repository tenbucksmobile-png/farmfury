/**
 * Chat Service
 *
 * Hotel-scoped real-time messaging backed by Supabase Realtime.
 *
 * Initial history  → get_chat_messages() RPC (SECURITY DEFINER, hotel-verified)
 * Load older       → get_chat_messages() RPC with p_before_timestamp cursor
 * Send message     → direct INSERT into messages (hotel-isolation RLS applies)
 * Live updates     → supabase.channel postgres_changes filtered by hotel
 */

import { supabase } from '@/lib/supabase';
import type { RealtimeChannel } from '@supabase/supabase-js';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ChatMessage {
  id:         string;
  body:       string;
  hotel:      string;
  created_at: string;
  sender: {
    id:            string;
    full_name:      string;
    employee_code:  string;
    position:       string | null;
  };
}

export const CHAT_PAGE_SIZE = 40;

// ─── Queries ──────────────────────────────────────────────────────────────────

/**
 * Fetch message history for a hotel, newest-first.
 * Pass beforeTimestamp to fetch messages older than a given point (pagination cursor).
 */
export async function getMessages(
  hotel:            string,
  limit             = CHAT_PAGE_SIZE,
  beforeTimestamp?: string,
): Promise<ChatMessage[]> {
  const { data, error } = await supabase.rpc('get_chat_messages', {
    p_hotel:            hotel,
    p_limit:            limit,
    p_before_timestamp: beforeTimestamp ?? null,
  });

  if (error) throw new Error(error.message);

  return ((data ?? []) as any[]).map(rowToMessage);
}

/** Send a new message. RLS enforces hotel = current_employee_hotel(). */
export async function sendMessage(
  senderId: string,
  hotel:    string,
  body:     string,
): Promise<ChatMessage> {
  const { data, error } = await supabase
    .from('messages')
    .insert({ sender_id: senderId, hotel, body: body.trim() })
    .select(`
      id, body, hotel, created_at,
      sender:employees!sender_id ( id, full_name, employee_code, position )
    `)
    .single();

  if (error) throw new Error(error.message);
  return data as unknown as ChatMessage;
}

// ─── Realtime ─────────────────────────────────────────────────────────────────

export function subscribeToMessages(
  hotel:     string,
  onMessage: (msg: ChatMessage) => void,
): RealtimeChannel {
  return supabase
    .channel(`chat:${hotel}`)
    .on(
      'postgres_changes',
      {
        event:  'INSERT',
        schema: 'public',
        table:  'messages',
        filter: `hotel=eq.${hotel}`,
      },
      async (payload) => {
        const newId = payload.new?.id as string | undefined;
        if (!newId) return;

        // Fetch full row with sender join via direct select
        const { data } = await supabase
          .from('messages')
          .select(`
            id, body, hotel, created_at,
            sender:employees!sender_id ( id, full_name, employee_code, position )
          `)
          .eq('id', newId)
          .single();

        if (data) onMessage(data as unknown as ChatMessage);
      },
    )
    .subscribe();
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function rowToMessage(row: any): ChatMessage {
  return {
    id:         row.id,
    body:       row.body,
    hotel:      row.hotel,
    created_at: row.created_at,
    sender: {
      id:            row.sender_id,
      full_name:     row.sender_name,
      employee_code: row.sender_code,
      position:      row.sender_position ?? null,
    },
  };
}
