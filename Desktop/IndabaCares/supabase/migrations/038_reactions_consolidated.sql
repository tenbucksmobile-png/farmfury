-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 038 — Reactions system (consolidated)
--
-- Migrations 035, 036, 037 each failed partially:
--   035 — rolled back entirely; recognition_reactions was never created.
--   036 — employee_reaction_allocations may or may not exist.
--   037 — failed because recognition_reactions did not exist.
--
-- This migration tears down any partial state left by 035-037 and rebuilds
-- the entire reactions system cleanly in dependency order.
-- ─────────────────────────────────────────────────────────────────────────────


-- ══════════════════════════════════════════════════════════════════════════════
-- SECTION A — Teardown (idempotent; safe to run even if objects don't exist)
-- ══════════════════════════════════════════════════════════════════════════════

-- ── Triggers on recognition_reactions (may not exist if table absent) ─────────

DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM pg_class
    WHERE relname = 'recognition_reactions'
      AND relnamespace = 'public'::regnamespace
  ) THEN
    DROP TRIGGER IF EXISTS trg_reaction_points_insert        ON public.recognition_reactions;
    DROP TRIGGER IF EXISTS trg_reaction_points_delete        ON public.recognition_reactions;
    DROP TRIGGER IF EXISTS trg_check_reaction_allocation_insert   ON public.recognition_reactions;
    DROP TRIGGER IF EXISTS trg_restore_reaction_allocation_delete ON public.recognition_reactions;
  END IF;
END;
$$;

-- ── Tables ────────────────────────────────────────────────────────────────────

DROP TABLE IF EXISTS public.employee_reaction_allocations CASCADE;
DROP TABLE IF EXISTS public.recognition_reactions         CASCADE;

-- ── All overloads of the two functions ───────────────────────────────────────

DO $$
DECLARE r record;
BEGIN
  FOR r IN
    SELECT oid::regprocedure::text AS sig
    FROM   pg_proc
    WHERE  proname      IN ('award_reaction_points', 'enforce_reaction_allocation')
      AND  pronamespace =  'public'::regnamespace
  LOOP
    EXECUTE 'DROP FUNCTION IF EXISTS ' || r.sig || ' CASCADE';
  END LOOP;
END;
$$;

-- ── points_ledger source constraint — restore then re-add with new value ──────

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


-- ══════════════════════════════════════════════════════════════════════════════
-- SECTION B — recognition_reactions
-- ══════════════════════════════════════════════════════════════════════════════

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
   Inserting awards points to the receiver; deleting reverses them.';

-- One reaction per (recognition, employee)
CREATE UNIQUE INDEX uq_reaction_per_employee
  ON public.recognition_reactions (recognition_id, employee_id);

CREATE INDEX idx_reactions_recognition
  ON public.recognition_reactions (recognition_id);

CREATE INDEX idx_reactions_employee
  ON public.recognition_reactions (employee_id);

CREATE INDEX idx_reactions_hotel
  ON public.recognition_reactions (hotel, created_at DESC);

-- RLS
ALTER TABLE public.recognition_reactions ENABLE ROW LEVEL SECURITY;

CREATE POLICY "reactions_hotel_select"
  ON public.recognition_reactions
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

CREATE POLICY "reactions_hotel_insert"
  ON public.recognition_reactions
  FOR INSERT TO anon, authenticated
  WITH CHECK (
    hotel       = public.current_employee_hotel()
    AND employee_id = public.current_employee_id()
  );

CREATE POLICY "reactions_hotel_delete"
  ON public.recognition_reactions
  FOR DELETE TO anon, authenticated
  USING (
    hotel       = public.current_employee_hotel()
    AND employee_id = public.current_employee_id()
  );


-- ══════════════════════════════════════════════════════════════════════════════
-- SECTION C — employee_reaction_allocations
-- ══════════════════════════════════════════════════════════════════════════════

CREATE TABLE public.employee_reaction_allocations (
  employee_id       uuid    NOT NULL
                            REFERENCES public.employees(id) ON DELETE CASCADE,
  month             integer NOT NULL CHECK (month BETWEEN 1 AND 12),
  year              integer NOT NULL CHECK (year  >= 2024),

  hearts_remaining  integer NOT NULL DEFAULT 10 CHECK (hearts_remaining  >= 0),
  smiles_remaining  integer NOT NULL DEFAULT 15 CHECK (smiles_remaining  >= 0),
  thumbs_remaining  integer NOT NULL DEFAULT 20 CHECK (thumbs_remaining  >= 0),

  created_at        timestamptz NOT NULL DEFAULT now(),

  CONSTRAINT pk_reaction_allocations
    PRIMARY KEY (employee_id, month, year)
);

CREATE INDEX idx_allocations_employee
  ON public.employee_reaction_allocations (employee_id, year DESC, month DESC);

COMMENT ON TABLE public.employee_reaction_allocations IS
  'Monthly per-employee reaction budget.
   Row is created on first reaction in a given month with default limits.
   hearts = 10 / month, smiles = 15 / month, thumbs_up = 20 / month.';

-- RLS
ALTER TABLE public.employee_reaction_allocations ENABLE ROW LEVEL SECURITY;

CREATE POLICY "allocations_select_own"
  ON public.employee_reaction_allocations
  FOR SELECT TO anon, authenticated
  USING (employee_id = public.current_employee_id());


-- ══════════════════════════════════════════════════════════════════════════════
-- SECTION D — enforce_reaction_allocation() trigger function
-- ══════════════════════════════════════════════════════════════════════════════
--
-- BEFORE INSERT: upsert allocation row → check budget → decrement → allow.
-- BEFORE DELETE: restore counter (capped at default max).

CREATE FUNCTION public.enforce_reaction_allocation()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_month  integer := EXTRACT(MONTH FROM now())::integer;
  v_year   integer := EXTRACT(YEAR  FROM now())::integer;
  DEFAULT_HEARTS CONSTANT integer := 10;
  DEFAULT_SMILES CONSTANT integer := 15;
  DEFAULT_THUMBS CONSTANT integer := 20;
  v_remaining integer;
BEGIN

  IF TG_OP = 'INSERT' THEN

    INSERT INTO public.employee_reaction_allocations (employee_id, month, year)
    VALUES (NEW.employee_id, v_month, v_year)
    ON CONFLICT (employee_id, month, year) DO NOTHING;

    SELECT CASE NEW.reaction_type
             WHEN 'heart'     THEN hearts_remaining
             WHEN 'smile'     THEN smiles_remaining
             WHEN 'thumbs_up' THEN thumbs_remaining
           END
    INTO  v_remaining
    FROM  public.employee_reaction_allocations
    WHERE employee_id = NEW.employee_id
      AND month       = v_month
      AND year        = v_year;

    IF v_remaining IS NULL OR v_remaining <= 0 THEN
      RAISE EXCEPTION 'Monthly % allocation exhausted.', NEW.reaction_type
        USING ERRCODE = 'P0006';
    END IF;

    UPDATE public.employee_reaction_allocations
    SET
      hearts_remaining = CASE WHEN NEW.reaction_type = 'heart'     THEN hearts_remaining - 1 ELSE hearts_remaining END,
      smiles_remaining = CASE WHEN NEW.reaction_type = 'smile'     THEN smiles_remaining - 1 ELSE smiles_remaining END,
      thumbs_remaining = CASE WHEN NEW.reaction_type = 'thumbs_up' THEN thumbs_remaining - 1 ELSE thumbs_remaining END
    WHERE employee_id = NEW.employee_id
      AND month       = v_month
      AND year        = v_year;

    RETURN NEW;
  END IF;

  IF TG_OP = 'DELETE' THEN

    UPDATE public.employee_reaction_allocations
    SET
      hearts_remaining = CASE WHEN OLD.reaction_type = 'heart'     THEN LEAST(hearts_remaining + 1, DEFAULT_HEARTS) ELSE hearts_remaining END,
      smiles_remaining = CASE WHEN OLD.reaction_type = 'smile'     THEN LEAST(smiles_remaining + 1, DEFAULT_SMILES) ELSE smiles_remaining END,
      thumbs_remaining = CASE WHEN OLD.reaction_type = 'thumbs_up' THEN LEAST(thumbs_remaining + 1, DEFAULT_THUMBS) ELSE thumbs_remaining END
    WHERE employee_id = OLD.employee_id
      AND month       = v_month
      AND year        = v_year;

    RETURN OLD;
  END IF;

  RETURN COALESCE(NEW, OLD);
END;
$$;

COMMENT ON FUNCTION public.enforce_reaction_allocation() IS
  'BEFORE INSERT: creates allocation row if absent, checks budget, decrements.
   BEFORE DELETE: restores counter capped at default maximum.';


-- ══════════════════════════════════════════════════════════════════════════════
-- SECTION E — award_reaction_points() trigger function
-- ══════════════════════════════════════════════════════════════════════════════
--
-- AFTER INSERT: awards points to the recognition receiver.
-- AFTER DELETE: reverses those points (floored at 0).

CREATE FUNCTION public.award_reaction_points()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_receiver_id uuid;
  v_points      integer;
  v_delta       integer;
BEGIN
  v_points := CASE (COALESCE(NEW, OLD)).reaction_type
    WHEN 'heart'     THEN 50
    WHEN 'smile'     THEN 20
    WHEN 'thumbs_up' THEN 10
    ELSE 0
  END;

  IF v_points = 0 THEN
    RETURN COALESCE(NEW, OLD);
  END IF;

  SELECT receiver_id INTO v_receiver_id
  FROM   public.recognitions
  WHERE  id = (COALESCE(NEW, OLD)).recognition_id;

  IF v_receiver_id IS NULL THEN
    RETURN COALESCE(NEW, OLD);
  END IF;

  v_delta := CASE TG_OP
    WHEN 'INSERT' THEN  v_points
    WHEN 'DELETE' THEN -v_points
  END;

  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = GREATEST(0, points_balance + v_delta)
  WHERE  id = v_receiver_id;

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

COMMENT ON FUNCTION public.award_reaction_points() IS
  'Awards or reverses points on the recognition receiver when a reaction is
   added (INSERT) or removed (DELETE).
   heart = 50 pts, smile = 20 pts, thumbs_up = 10 pts.';


-- ══════════════════════════════════════════════════════════════════════════════
-- SECTION F — Attach all triggers
-- ══════════════════════════════════════════════════════════════════════════════
--
-- Trigger order on INSERT:
--   1. trg_check_reaction_allocation_insert  (BEFORE) — blocks if budget exhausted
--   2. row written to recognition_reactions
--   3. trg_reaction_points_insert            (AFTER)  — awards points
--
-- A rejected INSERT never reaches the AFTER trigger; no phantom points awarded.

CREATE TRIGGER trg_check_reaction_allocation_insert
  BEFORE INSERT ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.enforce_reaction_allocation();

CREATE TRIGGER trg_restore_reaction_allocation_delete
  BEFORE DELETE ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.enforce_reaction_allocation();

CREATE TRIGGER trg_reaction_points_insert
  AFTER INSERT ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.award_reaction_points();

CREATE TRIGGER trg_reaction_points_delete
  AFTER DELETE ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.award_reaction_points();
