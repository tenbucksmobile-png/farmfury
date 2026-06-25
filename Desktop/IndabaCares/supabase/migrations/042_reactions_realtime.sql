-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 042 — Enable Realtime for recognition_reactions
--
-- Two changes required for the client-side subscription to work correctly:
--
--   1. REPLICA IDENTITY FULL on recognition_reactions
--      By default PostgreSQL only writes the PRIMARY KEY columns to the WAL
--      for DELETE events.  Supabase Realtime surfaces this as the `old`
--      record in the DELETE payload.  Without REPLICA IDENTITY FULL, a DELETE
--      payload only contains { id } — the client cannot resolve which
--      recognition the row belonged to.
--      Setting REPLICA IDENTITY FULL writes all columns to WAL on DELETE so
--      the payload includes recognition_id, employee_id, reaction_type, hotel.
--
--   2. Add table to Supabase Realtime publication
--      The supabase_realtime publication must include recognition_reactions
--      before the Realtime service will broadcast changes for it.
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. Full row identity for DELETE events ────────────────────────────────────

ALTER TABLE public.recognition_reactions REPLICA IDENTITY FULL;


-- ── 2. Register with Supabase Realtime publication ────────────────────────────
--
-- The ALTER PUBLICATION … ADD TABLE statement is idempotent in the sense that
-- it fails gracefully when the table is already a member.  Wrap in a DO block
-- so a re-run does not abort the migration.

DO $$
BEGIN
  ALTER PUBLICATION supabase_realtime ADD TABLE public.recognition_reactions;
EXCEPTION
  WHEN duplicate_object THEN
    -- Table already in publication; nothing to do.
    NULL;
END;
$$;
