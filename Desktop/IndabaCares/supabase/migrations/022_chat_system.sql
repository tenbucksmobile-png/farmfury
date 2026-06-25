-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 022 — Hotel Chat System
--
-- The messages table shell was created in migration 017.
-- This migration:
--   1. Adds performance indices for chat timeline queries
--   2. Enables Supabase Realtime replication on messages
--   3. Creates a get_chat_messages() RPC for ordered, sender-joined results
--
-- Schema (from 017):
--   messages (id, hotel, sender_id → employees, body, created_at)
--   RLS policies already applied in 017 (hotel-session isolation)
-- ─────────────────────────────────────────────────────────────────────────────


-- ─── 1. Performance indices ───────────────────────────────────────────────────

-- Composite index for chat timeline: all messages in a hotel, newest-first.
CREATE INDEX IF NOT EXISTS idx_messages_hotel_time
  ON public.messages (hotel, created_at DESC);

-- Sender lookup (for joins)
CREATE INDEX IF NOT EXISTS idx_messages_sender
  ON public.messages (sender_id);


-- ─── 2. Supabase Realtime ─────────────────────────────────────────────────────
--
-- REPLICA IDENTITY FULL ensures the replication stream includes all column
-- values, which is required for the hotel filter to work reliably in
-- postgres_changes subscriptions.

ALTER TABLE public.messages REPLICA IDENTITY FULL;

-- Add messages to the supabase_realtime publication so clients can subscribe.
-- Wrapped in a DO block so it is idempotent and safe on any Supabase project.
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'supabase_realtime') THEN
    -- Only add if not already a member
    IF NOT EXISTS (
      SELECT 1
      FROM   pg_publication_tables
      WHERE  pubname   = 'supabase_realtime'
        AND  schemaname = 'public'
        AND  tablename  = 'messages'
    ) THEN
      ALTER PUBLICATION supabase_realtime ADD TABLE public.messages;
    END IF;
  END IF;
END
$$;


-- ─── 3. get_chat_messages() — paginated message history ──────────────────────
--
-- Returns messages for a hotel in ascending order (oldest first) for rendering
-- in a chat timeline.  Supports cursor-based pagination via p_before_id.
--
-- The function is SECURITY DEFINER so it can join employees regardless of
-- employee RLS policies.  It still enforces hotel isolation by checking that
-- the caller's hotel (from the session token) matches the requested hotel.

CREATE OR REPLACE FUNCTION public.get_chat_messages(
  p_hotel     text,
  p_limit     integer DEFAULT 50,
  p_before_id uuid    DEFAULT NULL
)
RETURNS TABLE (
  id            uuid,
  body          text,
  hotel         text,
  created_at    timestamptz,
  sender_id     uuid,
  sender_name   text,
  sender_code   text,
  sender_position text
)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  -- Ensure the caller's session hotel matches the requested hotel.
  IF public.current_employee_hotel() IS DISTINCT FROM p_hotel THEN
    RETURN;  -- return empty result set; client sees no rows
  END IF;

  RETURN QUERY
  SELECT
    m.id,
    m.body,
    m.hotel,
    m.created_at,
    e.id          AS sender_id,
    e.full_name   AS sender_name,
    e.employee_code AS sender_code,
    e.position    AS sender_position
  FROM public.messages m
  JOIN public.employees e ON e.id = m.sender_id
  WHERE m.hotel = p_hotel
    AND (p_before_id IS NULL OR m.id < p_before_id)
  ORDER BY m.created_at DESC
  LIMIT p_limit;
END;
$$;

GRANT EXECUTE ON FUNCTION public.get_chat_messages(text, integer, uuid)
  TO anon, authenticated;

COMMENT ON FUNCTION public.get_chat_messages IS
  'Returns paginated chat messages for a hotel, newest-first.
   Enforces hotel isolation via current_employee_hotel() even though it is
   SECURITY DEFINER (needed for the employees JOIN).';


-- ─── 4. Comments ─────────────────────────────────────────────────────────────

COMMENT ON TABLE public.messages IS
  'Hotel-scoped chat messages. RLS: hotel = current_employee_hotel().
   Supabase Realtime is enabled for live updates.';
