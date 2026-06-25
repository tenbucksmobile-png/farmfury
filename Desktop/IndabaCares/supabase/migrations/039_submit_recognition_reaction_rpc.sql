-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 039 — submit_recognition_reaction RPC
--
-- Replaces the INSERT-side triggers from migration 038 with a single atomic
-- SECURITY DEFINER RPC that owns the full reaction creation flow.
--
-- The DELETE-side triggers (trg_restore_reaction_allocation_delete and
-- trg_reaction_points_delete) are kept — they handle point reversal and
-- allocation restoration when a reaction is removed.
--
-- Trigger ownership after this migration
--   INSERT path  → submit_recognition_reaction() RPC  (this migration)
--   DELETE path  → trg_restore_reaction_allocation_delete  (migration 038)
--                  trg_reaction_points_delete              (migration 038)
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. Drop INSERT-side triggers that the RPC supersedes ─────────────────────

DROP TRIGGER IF EXISTS trg_check_reaction_allocation_insert ON public.recognition_reactions;
DROP TRIGGER IF EXISTS trg_reaction_points_insert           ON public.recognition_reactions;


-- ── 2. submit_recognition_reaction ───────────────────────────────────────────

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
  v_remaining   integer;
  v_reaction_id uuid;

  DEFAULT_HEARTS CONSTANT integer := 10;
  DEFAULT_SMILES CONSTANT integer := 15;
  DEFAULT_THUMBS CONSTANT integer := 20;
BEGIN

  -- ── Step 1: Validate reaction type ────────────────────────────────

  IF p_reaction_type NOT IN ('heart', 'smile', 'thumbs_up') THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Invalid reaction type. Must be heart, smile, or thumbs_up.'
    );
  END IF;

  -- ── Security: employee_id must match the authenticated session ─────

  IF p_employee_id IS DISTINCT FROM public.current_employee_id() THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Unauthorized.');
  END IF;

  -- ── Resolve recognition → hotel + receiver ─────────────────────────

  SELECT hotel, receiver_id
  INTO   v_hotel, v_receiver_id
  FROM   public.recognitions
  WHERE  id = p_recognition_id;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Recognition not found.');
  END IF;

  -- ── Confirm employee is active in the same hotel ───────────────────

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

  -- ── Guard: no self-reaction on own recognitions ────────────────────

  IF p_employee_id = v_receiver_id THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'You cannot react to a recognition you received.'
    );
  END IF;

  -- ── Guard: duplicate reaction check ───────────────────────────────

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

  -- ── Resolve points for this reaction type ─────────────────────────

  v_points := CASE p_reaction_type
    WHEN 'heart'     THEN 50
    WHEN 'smile'     THEN 20
    WHEN 'thumbs_up' THEN 10
  END;

  -- ── Step 2: Check monthly allocation ──────────────────────────────
  -- Upsert the allocation row (created on first reaction of the month).

  INSERT INTO public.employee_reaction_allocations (employee_id, month, year)
  VALUES (p_employee_id, v_month, v_year)
  ON CONFLICT (employee_id, month, year) DO NOTHING;

  SELECT CASE p_reaction_type
           WHEN 'heart'     THEN hearts_remaining
           WHEN 'smile'     THEN smiles_remaining
           WHEN 'thumbs_up' THEN thumbs_remaining
         END
  INTO   v_remaining
  FROM   public.employee_reaction_allocations
  WHERE  employee_id = p_employee_id
    AND  month       = v_month
    AND  year        = v_year;

  -- ── Step 3: Return error if allocation exhausted ───────────────────

  IF v_remaining IS NULL OR v_remaining <= 0 THEN
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
    'ok',            true,
    'reaction_id',   v_reaction_id,
    'points_awarded', v_points,
    'remaining',     v_remaining - 1
  );

END;
$$;

GRANT EXECUTE ON FUNCTION public.submit_recognition_reaction(uuid, uuid, text)
  TO anon, authenticated;

COMMENT ON FUNCTION public.submit_recognition_reaction IS
  'Atomic reaction creation RPC.
   Steps: validate type → check monthly allocation → guard duplicates →
   insert reaction → deduct allocation → award points to receiver.
   Returns {ok, reaction_id, points_awarded, remaining} on success or
   {ok: false, error} on any validation failure.';
