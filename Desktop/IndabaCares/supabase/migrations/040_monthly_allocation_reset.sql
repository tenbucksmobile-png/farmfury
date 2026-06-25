-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 040 — Explicit monthly allocation reset
--
-- The reset strategy is purely dynamic: no cron, no scheduled job.
-- When an employee submits their first reaction of a new calendar month,
-- get_or_reset_reaction_allocation() detects that no row exists for
-- (employee_id, current_month, current_year) and creates one with fresh
-- default limits.  Subsequent reactions in the same month reuse that row.
--
-- Each calendar month gets its own row.  Old rows are never mutated across
-- month boundaries — January's budget cannot bleed into February's.
--
-- Changes in this migration
--   1. New helper: get_or_reset_reaction_allocation()
--      Encapsulates "create fresh row if new month, else return existing".
--   2. submit_recognition_reaction() rebuilt to call the helper explicitly,
--      replacing the bare INSERT … ON CONFLICT DO NOTHING that was in mig 039.
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. get_or_reset_reaction_allocation() ────────────────────────────────────
--
-- Called at the start of every reaction submission.
-- Returns the allocation row for (employee_id, current month, current year).
--
-- If no row exists for the current month (first reaction of the month):
--   → INSERT a fresh row with default limits (hearts=10, smiles=15, thumbs=20).
--   → This IS the monthly reset — no cron needed.
--
-- If a row already exists for the current month:
--   → Return it unchanged (subsequent reactions this month).

CREATE OR REPLACE FUNCTION public.get_or_reset_reaction_allocation(
  p_employee_id uuid,
  p_month       integer,
  p_year        integer
)
RETURNS public.employee_reaction_allocations
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_row public.employee_reaction_allocations;
BEGIN
  -- Attempt to fetch an existing row for this month
  SELECT * INTO v_row
  FROM   public.employee_reaction_allocations
  WHERE  employee_id = p_employee_id
    AND  month       = p_month
    AND  year        = p_year;

  IF FOUND THEN
    -- Row exists — employee has already reacted this month; return as-is
    RETURN v_row;
  END IF;

  -- No row for this month → first reaction of a new month.
  -- Create a fresh allocation row with default limits (monthly reset).
  INSERT INTO public.employee_reaction_allocations
    (employee_id, month, year, hearts_remaining, smiles_remaining, thumbs_remaining)
  VALUES
    (p_employee_id, p_month, p_year, 10, 15, 20)
  RETURNING * INTO v_row;

  RETURN v_row;
END;
$$;

COMMENT ON FUNCTION public.get_or_reset_reaction_allocation(uuid, integer, integer) IS
  'Returns the reaction allocation row for the given employee and month/year.
   If none exists (first reaction of the month) a fresh row is created with
   default limits (hearts=10, smiles=15, thumbs=20).
   This is the monthly reset mechanism — no cron job required.';


-- ── 2. Rebuild submit_recognition_reaction to use the helper ─────────────────
--
-- All logic is identical to migration 039 except steps 2–3, which now call
-- get_or_reset_reaction_allocation() instead of the bare upsert.

CREATE OR REPLACE FUNCTION public.submit_recognition_reaction(
  p_recognition_id  uuid,
  p_employee_id     uuid,
  p_reaction_type   text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_month       integer := EXTRACT(MONTH FROM now())::integer;
  v_year        integer := EXTRACT(YEAR  FROM now())::integer;
  v_hotel       text;
  v_receiver_id uuid;
  v_points      integer;
  v_alloc       public.employee_reaction_allocations;
  v_remaining   integer;
  v_reaction_id uuid;
BEGIN

  -- ── Step 1: Validate reaction type ────────────────────────────────

  IF p_reaction_type NOT IN ('heart', 'smile', 'thumbs_up') THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Invalid reaction type. Must be heart, smile, or thumbs_up.'
    );
  END IF;

  -- Security: employee_id must match the authenticated session
  IF p_employee_id IS DISTINCT FROM public.current_employee_id() THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Unauthorized.');
  END IF;

  -- Resolve recognition → hotel + receiver
  SELECT hotel, receiver_id
  INTO   v_hotel, v_receiver_id
  FROM   public.recognitions
  WHERE  id = p_recognition_id;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Recognition not found.');
  END IF;

  -- Confirm employee is active in the same hotel
  IF NOT EXISTS (
    SELECT 1 FROM public.employees
    WHERE  id     = p_employee_id
      AND  hotel  = v_hotel
      AND  status = 'active'
  ) THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Employee not found or inactive at this hotel.'
    );
  END IF;

  -- Guard: no self-reaction
  IF p_employee_id = v_receiver_id THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'You cannot react to a recognition you received.'
    );
  END IF;

  -- Guard: duplicate reaction
  IF EXISTS (
    SELECT 1 FROM public.recognition_reactions
    WHERE  recognition_id = p_recognition_id
      AND  employee_id    = p_employee_id
  ) THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'You have already reacted to this recognition.'
    );
  END IF;

  -- Resolve points value
  v_points := CASE p_reaction_type
    WHEN 'heart'     THEN 50
    WHEN 'smile'     THEN 20
    WHEN 'thumbs_up' THEN 10
  END;

  -- ── Step 2: Get or reset monthly allocation ────────────────────────
  --
  -- If no allocation row exists for this month, get_or_reset_reaction_allocation
  -- creates one with fresh defaults — this is the automatic monthly reset.
  -- If a row already exists, it is returned unchanged.

  v_alloc := public.get_or_reset_reaction_allocation(
    p_employee_id,
    v_month,
    v_year
  );

  v_remaining := CASE p_reaction_type
    WHEN 'heart'     THEN v_alloc.hearts_remaining
    WHEN 'smile'     THEN v_alloc.smiles_remaining
    WHEN 'thumbs_up' THEN v_alloc.thumbs_remaining
  END;

  -- ── Step 3: Return error if allocation exhausted ───────────────────

  IF v_remaining <= 0 THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'You have exceeded your monthly reaction allocation.'
    );
  END IF;

  -- ── Step 4: Insert reaction ────────────────────────────────────────

  INSERT INTO public.recognition_reactions
    (recognition_id, employee_id, reaction_type, hotel)
  VALUES
    (p_recognition_id, p_employee_id, p_reaction_type, v_hotel)
  RETURNING id INTO v_reaction_id;

  -- ── Step 5: Deduct allocation ──────────────────────────────────────

  UPDATE public.employee_reaction_allocations
  SET
    hearts_remaining = CASE WHEN p_reaction_type = 'heart'
                            THEN hearts_remaining - 1
                            ELSE hearts_remaining END,
    smiles_remaining = CASE WHEN p_reaction_type = 'smile'
                            THEN smiles_remaining - 1
                            ELSE smiles_remaining END,
    thumbs_remaining = CASE WHEN p_reaction_type = 'thumbs_up'
                            THEN thumbs_remaining - 1
                            ELSE thumbs_remaining END
  WHERE employee_id = p_employee_id
    AND month       = v_month
    AND year        = v_year;

  -- ── Step 6: Award points to the recognition receiver ──────────────

  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = points_balance + v_points
  WHERE  id = v_receiver_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (v_receiver_id, v_points, 'reaction_received', v_hotel);

  -- ── Return success ─────────────────────────────────────────────────

  RETURN jsonb_build_object(
    'ok',             true,
    'reaction_id',    v_reaction_id,
    'points_awarded', v_points,
    'remaining',      v_remaining - 1
  );

END;
$$;

COMMENT ON FUNCTION public.submit_recognition_reaction(uuid, uuid, text) IS
  'Atomic reaction creation RPC with automatic monthly reset.
   Calls get_or_reset_reaction_allocation() which creates a fresh allocation
   row on the first reaction of each new month (no cron required).
   Steps: validate → monthly reset check → budget check →
          insert reaction → deduct → award points to receiver.';
