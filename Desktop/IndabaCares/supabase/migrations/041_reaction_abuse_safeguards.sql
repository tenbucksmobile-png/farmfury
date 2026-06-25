-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 041 — Reaction abuse safeguards
--
-- Hardens submit_recognition_reaction() with three additional guarantees:
--
--   1. Sender self-react blocked
--      Employee who WROTE the recognition cannot react to it.
--      Previously only the receiver was blocked; the sender was not.
--
--   2. Atomic allocation deduction (race-condition-safe)
--      Replaces the read → check → UPDATE pattern with a single
--      UPDATE … WHERE column > 0 RETURNING …
--      PostgreSQL serialises row-level updates, so two concurrent requests
--      for the last remaining allocation cannot both succeed.
--      The WHERE clause acts as the budget guard; if 0 rows are updated the
--      budget is exhausted and the function returns an error without ever
--      inserting a reaction.
--
--   3. Graceful duplicate-reaction catch
--      If two concurrent requests slip past the duplicate EXISTS check and
--      both reach the INSERT, the UNIQUE(recognition_id, employee_id) DB
--      constraint raises a unique_violation.  The EXCEPTION handler catches
--      it and returns {ok: false, error: '...'} instead of surfacing a raw
--      DB exception to the client.
--      PostgreSQL's savepoint semantics ensure the allocation deduction is
--      automatically rolled back if the exception is caught.
--
-- No schema changes — the table constraints added in migration 038 already
-- provide the underlying safety net (CHECK >= 0, UNIQUE index).
-- This migration only replaces the function body.
-- ─────────────────────────────────────────────────────────────────────────────


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
  v_month         integer := EXTRACT(MONTH FROM now())::integer;
  v_year          integer := EXTRACT(YEAR  FROM now())::integer;
  v_hotel         text;
  v_receiver_id   uuid;
  v_sender_id     uuid;
  v_points        integer;
  v_new_remaining integer;
  v_reaction_id   uuid;
BEGIN

  -- ── 1. Validate reaction type ─────────────────────────────────────────────

  IF p_reaction_type NOT IN ('heart', 'smile', 'thumbs_up') THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Invalid reaction type. Must be heart, smile, or thumbs_up.'
    );
  END IF;


  -- ── 2. Session guard ──────────────────────────────────────────────────────
  --
  -- The caller must be the authenticated employee.
  -- Prevents one employee from submitting reactions on behalf of another.

  IF p_employee_id IS DISTINCT FROM public.current_employee_id() THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Unauthorized.');
  END IF;


  -- ── 3. Resolve recognition ────────────────────────────────────────────────

  SELECT hotel, receiver_id, sender_id
  INTO   v_hotel, v_receiver_id, v_sender_id
  FROM   public.recognitions
  WHERE  id = p_recognition_id;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Recognition not found.');
  END IF;


  -- ── 4. Active-employee + hotel guard ─────────────────────────────────────

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


  -- ── 5. Self-react guard — receiver ───────────────────────────────────────
  --
  -- The person being recognised cannot react to their own recognition.
  -- Blocking self-congratulation.

  IF p_employee_id = v_receiver_id THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'You cannot react to a recognition you received.'
    );
  END IF;


  -- ── 6. Self-react guard — sender ─────────────────────────────────────────
  --
  -- The person who wrote the recognition cannot react to it either.
  -- Prevents the sender from amplifying their own post to award extra points.

  IF p_employee_id = v_sender_id THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'You cannot react to a recognition you sent.'
    );
  END IF;


  -- ── 7. Duplicate reaction guard ───────────────────────────────────────────
  --
  -- One reaction per employee per recognition (enforced at DB level too via
  -- UNIQUE index).  This early check returns a friendly message before we
  -- touch the allocation row.

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


  -- ── 8. Resolve points value ───────────────────────────────────────────────

  v_points := CASE p_reaction_type
    WHEN 'heart'     THEN 50
    WHEN 'smile'     THEN 20
    WHEN 'thumbs_up' THEN 10
  END;


  -- ── 9. Get or create monthly allocation row ───────────────────────────────
  --
  -- If this is the employee's first reaction of the calendar month, a fresh
  -- allocation row is created with default limits (hearts=10, smiles=15,
  -- thumbs=20) — this is the monthly reset; no cron job required.

  PERFORM public.get_or_reset_reaction_allocation(
    p_employee_id,
    v_month,
    v_year
  );


  -- ── 10. Atomic allocation deduction ──────────────────────────────────────
  --
  -- The WHERE clause includes AND column > 0, so this UPDATE succeeds only
  -- if budget remains.  PostgreSQL serialises concurrent updates to the same
  -- row: the second concurrent request will re-evaluate the WHERE clause
  -- against the already-decremented value and find 0, returning NOT FOUND.
  --
  -- RETURNING gives us the new remaining count without a second round-trip.
  --
  -- The CHECK (column >= 0) constraints on the table are a final backstop;
  -- they can never be violated by this logic but guard against any direct
  -- DML that bypasses this function.

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
    AND year        = v_year
    AND (
      (p_reaction_type = 'heart'     AND hearts_remaining > 0) OR
      (p_reaction_type = 'smile'     AND smiles_remaining > 0) OR
      (p_reaction_type = 'thumbs_up' AND thumbs_remaining > 0)
    )
  RETURNING
    CASE p_reaction_type
      WHEN 'heart'     THEN hearts_remaining
      WHEN 'smile'     THEN smiles_remaining
      WHEN 'thumbs_up' THEN thumbs_remaining
    END
  INTO v_new_remaining;

  -- 0 rows updated → budget exhausted
  IF NOT FOUND THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', format(
        'No %s reactions remaining this month.',
        CASE p_reaction_type
          WHEN 'heart'     THEN 'heart ❤️'
          WHEN 'smile'     THEN 'smile 😊'
          WHEN 'thumbs_up' THEN 'thumbs-up 👍'
        END
      )
    );
  END IF;


  -- ── 11. Insert reaction ───────────────────────────────────────────────────
  --
  -- If a concurrent request slipped past the duplicate check in step 7 and
  -- also reached this point, the UNIQUE(recognition_id, employee_id) index
  -- raises unique_violation.  The EXCEPTION block below catches it and
  -- returns a friendly response.  PostgreSQL's internal savepoint mechanism
  -- rolls back the allocation deduction in step 10 automatically.

  INSERT INTO public.recognition_reactions
    (recognition_id, employee_id, reaction_type, hotel)
  VALUES
    (p_recognition_id, p_employee_id, p_reaction_type, v_hotel)
  RETURNING id INTO v_reaction_id;


  -- ── 12. Award points to the recognition receiver ──────────────────────────

  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = points_balance + v_points
  WHERE  id = v_receiver_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (v_receiver_id, v_points, 'reaction_received', v_hotel);


  -- ── Return success ────────────────────────────────────────────────────────

  RETURN jsonb_build_object(
    'ok',             true,
    'reaction_id',    v_reaction_id,
    'points_awarded', v_points,
    'remaining',      v_new_remaining
  );

EXCEPTION
  WHEN unique_violation THEN
    -- A concurrent request won the race: the UNIQUE constraint fired.
    -- PostgreSQL rolled back the step 10 deduction via the internal savepoint.
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'You have already reacted to this recognition.'
    );
END;
$$;


COMMENT ON FUNCTION public.submit_recognition_reaction(uuid, uuid, text) IS
  'Atomic reaction creation with full abuse prevention.
   Guards (in order):
     1. Valid reaction type
     2. Session matches caller (no impersonation)
     3. Recognition exists
     4. Employee active at same hotel
     5. Receiver cannot self-react
     6. Sender cannot self-react
     7. No duplicate reaction (UNIQUE index also enforces at DB level)
     8. Monthly budget check — atomic UPDATE WHERE column > 0 (race-safe)
     9. Duplicate race caught by EXCEPTION WHEN unique_violation
   On success: inserts reaction, deducts allocation, awards points to receiver.
   Returns {ok, reaction_id, points_awarded, remaining} or {ok: false, error}.';
