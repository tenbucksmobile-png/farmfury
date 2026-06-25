-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 018 — Recognition Feed System
--
-- Replaces the legacy profiles/company-based recognition tables with a clean
-- employee-centric schema:
--
--   recognitions        — core posts (sender → receiver, badge, message)
--   recognition_likes   — one like per employee per recognition
--   recognition_comments — threaded comments on recognitions
--
-- All three tables are hotel-isolated using current_employee_hotel() RLS.
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. Drop legacy tables ─────────────────────────────────────────────────────
-- CASCADE removes dependent FK constraints, policies, and triggers.

DROP TABLE IF EXISTS public.recognition_recipients CASCADE;
DROP TABLE IF EXISTS public.reactions              CASCADE;
DROP TABLE IF EXISTS public.comments               CASCADE;
DROP TABLE IF EXISTS public.thumbs_up_types        CASCADE;
DROP TABLE IF EXISTS public.company_values         CASCADE;
DROP TABLE IF EXISTS public.recognitions           CASCADE;


-- ── 2. recognitions ──────────────────────────────────────────────────────────

CREATE TABLE public.recognitions (
  id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  sender_id   uuid        NOT NULL REFERENCES public.employees(id) ON DELETE CASCADE,
  receiver_id uuid        NOT NULL REFERENCES public.employees(id) ON DELETE CASCADE,
  message     text        NOT NULL,
  badge       text        NOT NULL,
  hotel       text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  created_at  timestamptz NOT NULL DEFAULT now(),

  CONSTRAINT chk_recognition_badge CHECK (badge IN (
    'Team Player',
    'Leadership',
    'Customer Excellence',
    'Innovation',
    'Going the Extra Mile',
    'Hospitality Hero'
  )),
  CONSTRAINT chk_recognition_message CHECK (char_length(trim(message)) >= 3),
  CONSTRAINT chk_no_self_recognition  CHECK (sender_id <> receiver_id)
);

-- Primary feed query: newest first within a hotel
CREATE INDEX idx_recognitions_feed
  ON public.recognitions (hotel, created_at DESC);

-- "Sent by me" profile view
CREATE INDEX idx_recognitions_sender
  ON public.recognitions (sender_id, created_at DESC);

-- "Received by me" profile view
CREATE INDEX idx_recognitions_receiver
  ON public.recognitions (receiver_id, created_at DESC);

COMMENT ON TABLE public.recognitions IS
  'Employee recognition posts.  A sender celebrates a receiver with a named badge and a '
  'personal message.  Scoped to a single hotel for tenant isolation.';


-- ── 3. recognition_likes ─────────────────────────────────────────────────────
-- One like per employee per recognition.  Toggle model: insert to like,
-- delete to unlike.

CREATE TABLE public.recognition_likes (
  id              uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  recognition_id  uuid        NOT NULL REFERENCES public.recognitions(id) ON DELETE CASCADE,
  employee_id     uuid        NOT NULL REFERENCES public.employees(id)    ON DELETE CASCADE,
  hotel           text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  created_at      timestamptz NOT NULL DEFAULT now(),

  CONSTRAINT uq_recognition_like UNIQUE (recognition_id, employee_id)
);

CREATE INDEX idx_likes_recognition
  ON public.recognition_likes (recognition_id);

COMMENT ON TABLE public.recognition_likes IS
  'Likes on recognition posts.  Unique constraint prevents duplicate likes.';


-- ── 4. recognition_comments ──────────────────────────────────────────────────

CREATE TABLE public.recognition_comments (
  id              uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  recognition_id  uuid        NOT NULL REFERENCES public.recognitions(id) ON DELETE CASCADE,
  employee_id     uuid        NOT NULL REFERENCES public.employees(id)    ON DELETE CASCADE,
  body            text        NOT NULL CHECK (char_length(trim(body)) > 0),
  hotel           text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_recognition_comments_recognition
  ON public.recognition_comments (recognition_id, created_at ASC);

-- updated_at auto-touch
CREATE OR REPLACE FUNCTION public.touch_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$$;

CREATE TRIGGER recognition_comments_updated_at
  BEFORE UPDATE ON public.recognition_comments
  FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();

COMMENT ON TABLE public.recognition_comments IS
  'Comments on recognition posts.  Ordered oldest-first for threaded display.';


-- ── 5. Enable RLS ─────────────────────────────────────────────────────────────

ALTER TABLE public.recognitions         ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.recognition_likes    ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.recognition_comments ENABLE ROW LEVEL SECURITY;


-- ── 6. Hotel-isolation RLS policies ──────────────────────────────────────────
--
-- Pattern (same as migration 017):
--   USING     (hotel = public.current_employee_hotel())  — read gate
--   WITH CHECK (hotel = public.current_employee_hotel())  — write gate
--
-- current_employee_hotel() returns NULL for missing/expired tokens,
-- so NULL = NULL evaluates to NULL (not TRUE) → access is denied.

-- recognitions ────────────────────────────────────────────────────────────────

CREATE POLICY "recognitions_hotel_select"
  ON public.recognitions
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

CREATE POLICY "recognitions_hotel_insert"
  ON public.recognitions
  FOR INSERT TO anon, authenticated
  WITH CHECK (hotel = public.current_employee_hotel());

-- recognition_likes ───────────────────────────────────────────────────────────

CREATE POLICY "likes_hotel_select"
  ON public.recognition_likes
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

CREATE POLICY "likes_hotel_insert"
  ON public.recognition_likes
  FOR INSERT TO anon, authenticated
  WITH CHECK (hotel = public.current_employee_hotel());

CREATE POLICY "likes_hotel_delete"
  ON public.recognition_likes
  FOR DELETE TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

-- recognition_comments ────────────────────────────────────────────────────────

CREATE POLICY "rec_comments_hotel_select"
  ON public.recognition_comments
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

CREATE POLICY "rec_comments_hotel_insert"
  ON public.recognition_comments
  FOR INSERT TO anon, authenticated
  WITH CHECK (hotel = public.current_employee_hotel());

CREATE POLICY "rec_comments_hotel_delete"
  ON public.recognition_comments
  FOR DELETE TO anon, authenticated
  USING (hotel = public.current_employee_hotel());
