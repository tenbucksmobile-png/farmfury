-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 036 — Monthly Reaction Allocation Tracking
--
-- Each employee receives a fixed monthly budget of reactions:
--   hearts_remaining  = 10
--   smiles_remaining  = 15
--   thumbs_remaining  = 20
--
-- The allocation row is created on-demand the first time an employee reacts
-- in a given month.  A BEFORE INSERT trigger on recognition_reactions checks
-- and decrements the budget, blocking the INSERT when the budget is exhausted.
-- A BEFORE DELETE trigger restores the allocation when a reaction is removed.
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. employee_reaction_allocations table ────────────────────────────────────

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


-- ── 2. RLS ────────────────────────────────────────────────────────────────────

ALTER TABLE public.employee_reaction_allocations ENABLE ROW LEVEL SECURITY;

-- An employee may only read their own allocation
CREATE POLICY "allocations_select_own"
  ON public.employee_reaction_allocations
  FOR SELECT TO anon, authenticated
  USING (employee_id = public.current_employee_id());

-- Rows are written exclusively by SECURITY DEFINER trigger functions.
-- No INSERT / UPDATE policy for anon / authenticated — the trigger bypasses RLS.


-- ── 3. enforce_reaction_allocation() — BEFORE INSERT / DELETE trigger ─────────
--
-- BEFORE INSERT
--   · Upserts the allocation row for the current (employee, month, year).
--   · Checks that the relevant counter > 0; raises if exhausted.
--   · Decrements the counter and allows the INSERT to proceed.
--
-- BEFORE DELETE
--   · Restores (increments) the counter, capped at the default maximum.
--   · Never raises — removal is always allowed.

CREATE OR REPLACE FUNCTION public.enforce_reaction_allocation()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_month  integer := EXTRACT(MONTH FROM now())::integer;
  v_year   integer := EXTRACT(YEAR  FROM now())::integer;

  -- Default ceilings (used when capping restored counts)
  DEFAULT_HEARTS CONSTANT integer := 10;
  DEFAULT_SMILES CONSTANT integer := 15;
  DEFAULT_THUMBS CONSTANT integer := 20;

  v_remaining integer;
BEGIN

  -- ── INSERT path: check budget then decrement ──────────────────────

  IF TG_OP = 'INSERT' THEN

    -- Create the allocation row if this is the employee's first reaction
    -- in this calendar month (INSERT … ON CONFLICT DO NOTHING).
    INSERT INTO public.employee_reaction_allocations
      (employee_id, month, year)
    VALUES
      (NEW.employee_id, v_month, v_year)
    ON CONFLICT (employee_id, month, year) DO NOTHING;

    -- Read the current remaining count for this reaction type
    SELECT
      CASE NEW.reaction_type
        WHEN 'heart'     THEN hearts_remaining
        WHEN 'smile'     THEN smiles_remaining
        WHEN 'thumbs_up' THEN thumbs_remaining
      END
    INTO v_remaining
    FROM  public.employee_reaction_allocations
    WHERE employee_id = NEW.employee_id
      AND month       = v_month
      AND year        = v_year;

    IF v_remaining IS NULL OR v_remaining <= 0 THEN
      RAISE EXCEPTION
        'Monthly % allocation exhausted for this employee.',
        NEW.reaction_type
        USING ERRCODE = 'P0006';
    END IF;

    -- Decrement the appropriate counter
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


  -- ── DELETE path: restore the counter (capped at default maximum) ──

  IF TG_OP = 'DELETE' THEN

    -- Only restore if the allocation row still exists for this month/year.
    -- (It may have been deleted in a data-cleanup scenario; ignore if so.)
    UPDATE public.employee_reaction_allocations
    SET
      hearts_remaining = CASE
        WHEN OLD.reaction_type = 'heart'
        THEN LEAST(hearts_remaining + 1, DEFAULT_HEARTS)
        ELSE hearts_remaining
      END,
      smiles_remaining = CASE
        WHEN OLD.reaction_type = 'smile'
        THEN LEAST(smiles_remaining + 1, DEFAULT_SMILES)
        ELSE smiles_remaining
      END,
      thumbs_remaining = CASE
        WHEN OLD.reaction_type = 'thumbs_up'
        THEN LEAST(thumbs_remaining + 1, DEFAULT_THUMBS)
        ELSE thumbs_remaining
      END
    WHERE employee_id = OLD.employee_id
      AND month       = v_month
      AND year        = v_year;

    RETURN OLD;
  END IF;

  RETURN COALESCE(NEW, OLD);
END;
$$;

COMMENT ON FUNCTION public.enforce_reaction_allocation IS
  'BEFORE INSERT: creates the monthly allocation row if absent, checks budget,
   decrements the relevant counter, and raises P0006 if exhausted.
   BEFORE DELETE: restores the counter (capped at the default maximum).';


-- ── 4. Attach triggers ────────────────────────────────────────────────────────
--
-- BEFORE INSERT — must run before award_reaction_points (AFTER INSERT, mig 035)
-- so a rejected INSERT never reaches the points trigger.

CREATE TRIGGER trg_check_reaction_allocation_insert
  BEFORE INSERT ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.enforce_reaction_allocation();

CREATE TRIGGER trg_restore_reaction_allocation_delete
  BEFORE DELETE ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.enforce_reaction_allocation();
