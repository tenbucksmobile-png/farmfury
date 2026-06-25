-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 035 — Recognition Reactions + Reaction-based Points
--
-- Adds an emoji-reaction system on top of the recognition feed.
--
-- Rules
--   · An employee may place exactly one reaction per recognition
--     (enforced by UNIQUE index on recognition_id, employee_id).
--   · Removing a reaction reverses the points awarded to the receiver.
--
-- Points awarded to the recognition RECEIVER on INSERT:
--   heart      → 50 pts
--   smile      → 20 pts
--   thumbs_up  → 10 pts
--
-- Points source written to points_ledger: 'reaction_received'
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. Extend points_ledger.source to allow 'reaction_received' ───────────────
--
-- The inline CHECK constraint from migration 021 is auto-named by Postgres.
-- Drop it and replace with a named version that includes the new source.

ALTER TABLE public.points_ledger
  DROP CONSTRAINT IF EXISTS points_ledger_source_check;

ALTER TABLE public.points_ledger
  ADD CONSTRAINT points_ledger_source_check
  CHECK (source IN (
    'recognition_received',
    'admin_bonus',
    'campaign_reward',
    'reaction_received'
  ));


-- ── 2. recognition_reactions table ───────────────────────────────────────────

CREATE TABLE public.recognition_reactions (
  id               uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  recognition_id   uuid        NOT NULL
                               REFERENCES public.recognitions(id) ON DELETE CASCADE,
  employee_id      uuid        NOT NULL
                               REFERENCES public.employees(id)    ON DELETE CASCADE,
  reaction_type    text        NOT NULL
                               CHECK (reaction_type IN ('heart', 'smile', 'thumbs_up')),
  hotel            text        NOT NULL
                               CHECK (public.is_valid_hotel(hotel)),
  created_at       timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE public.recognition_reactions IS
  'Emoji reactions on recognition posts. One reaction per employee per recognition.
   Inserting a reaction awards points to the recognition receiver; deleting reverses them.';


-- ── 3. Unique constraint: one reaction per (recognition, employee) ────────────

CREATE UNIQUE INDEX uq_reaction_per_employee
  ON public.recognition_reactions (recognition_id, employee_id);


-- ── 4. Performance indexes ────────────────────────────────────────────────────

CREATE INDEX idx_reactions_recognition
  ON public.recognition_reactions (recognition_id);

CREATE INDEX idx_reactions_employee
  ON public.recognition_reactions (employee_id);

CREATE INDEX idx_reactions_hotel
  ON public.recognition_reactions (hotel, created_at DESC);


-- ── 5. RLS ────────────────────────────────────────────────────────────────────

ALTER TABLE public.recognition_reactions ENABLE ROW LEVEL SECURITY;

-- Any employee in the same hotel can read reactions
CREATE POLICY "reactions_hotel_select"
  ON public.recognition_reactions
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

-- An employee may only insert their own reactions within their hotel
CREATE POLICY "reactions_hotel_insert"
  ON public.recognition_reactions
  FOR INSERT TO anon, authenticated
  WITH CHECK (
    hotel       = public.current_employee_hotel()
    AND employee_id = public.current_employee_id()
  );

-- An employee may only delete their own reactions
CREATE POLICY "reactions_hotel_delete"
  ON public.recognition_reactions
  FOR DELETE TO anon, authenticated
  USING (
    hotel       = public.current_employee_hotel()
    AND employee_id = public.current_employee_id()
  );


-- ── 6. award_reaction_points() trigger function ───────────────────────────────
--
-- Fired AFTER INSERT or AFTER DELETE on recognition_reactions.
--
-- On INSERT:  awards points to the recognition receiver
-- On DELETE:  reverses (deducts) those same points
--
-- Uses the same GUC pattern as admin_grant_points() so the
-- trg_guard_points_balance guard allows the UPDATE.

CREATE OR REPLACE FUNCTION public.award_reaction_points()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_receiver_id uuid;
  v_points      integer;
  v_delta       integer;   -- positive on INSERT, negative on DELETE
BEGIN
  -- ── Resolve points for the reaction type ───────────────────────────
  v_points := CASE
    WHEN (COALESCE(NEW, OLD)).reaction_type = 'heart'     THEN 50
    WHEN (COALESCE(NEW, OLD)).reaction_type = 'smile'     THEN 20
    WHEN (COALESCE(NEW, OLD)).reaction_type = 'thumbs_up' THEN 10
    ELSE 0
  END;

  IF v_points = 0 THEN
    RETURN COALESCE(NEW, OLD);
  END IF;

  -- ── Resolve the recognition receiver ───────────────────────────────
  SELECT receiver_id
  INTO   v_receiver_id
  FROM   public.recognitions
  WHERE  id = (COALESCE(NEW, OLD)).recognition_id;

  IF v_receiver_id IS NULL THEN
    RETURN COALESCE(NEW, OLD);
  END IF;

  -- ── Direction: award on INSERT, reverse on DELETE ───────────────────
  v_delta := CASE TG_OP
    WHEN 'INSERT' THEN  v_points
    WHEN 'DELETE' THEN -v_points
  END;

  -- ── Allow points_balance write (guard GUC pattern from mig 025) ────
  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  -- ── Update running balance (floor at 0 on deductions) ──────────────
  UPDATE public.employees
  SET    points_balance = GREATEST(0, points_balance + v_delta)
  WHERE  id = v_receiver_id;

  -- ── Append immutable ledger entry ──────────────────────────────────
  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (
    v_receiver_id,
    v_delta,
    'reaction_received',
    (COALESCE(NEW, OLD)).hotel
  );

  RETURN COALESCE(NEW, OLD);
END;
$$;

COMMENT ON FUNCTION public.award_reaction_points IS
  'Awards or reverses points on the recognition receiver when a reaction is
   added (INSERT) or removed (DELETE).
   heart = 50 pts, smile = 20 pts, thumbs_up = 10 pts.';


-- ── 7. Attach triggers ────────────────────────────────────────────────────────

CREATE TRIGGER trg_reaction_points_insert
  AFTER INSERT ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.award_reaction_points();

CREATE TRIGGER trg_reaction_points_delete
  AFTER DELETE ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.award_reaction_points();
