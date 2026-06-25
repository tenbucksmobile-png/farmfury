-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 037 — Fix ambiguous award_reaction_points() overloads
--
-- A partial run of migration 035 left multiple overloaded signatures of
-- public.award_reaction_points().  PostgreSQL error 42725 fires whenever
-- a DROP FUNCTION or EXECUTE FUNCTION cannot resolve the name unambiguously.
--
-- Fix:
--   1. Drop the dependent triggers (they reference the ambiguous function).
--   2. Dynamically drop every overload of award_reaction_points() regardless
--      of argument list.
--   3. Recreate the single canonical trigger-function signature.
--   4. Reattach the triggers.
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. Drop dependent triggers first ─────────────────────────────────────────

DROP TRIGGER IF EXISTS trg_reaction_points_insert ON public.recognition_reactions;
DROP TRIGGER IF EXISTS trg_reaction_points_delete ON public.recognition_reactions;


-- ── 2. Drop all overloads of award_reaction_points() ─────────────────────────
--
-- Iterates pg_proc to find every registered signature and drops each one.
-- CASCADE removes any remaining trigger dependencies.

DO $$
DECLARE
  r record;
BEGIN
  FOR r IN
    SELECT oid::regprocedure::text AS sig
    FROM   pg_proc
    WHERE  proname        = 'award_reaction_points'
      AND  pronamespace   = 'public'::regnamespace
  LOOP
    EXECUTE 'DROP FUNCTION IF EXISTS ' || r.sig || ' CASCADE';
  END LOOP;
END;
$$;


-- ── 3. Recreate the canonical trigger function ────────────────────────────────
--
-- Takes no arguments, returns trigger (the only valid signature for a
-- PostgreSQL trigger function called via EXECUTE FUNCTION).

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

  SELECT receiver_id
  INTO   v_receiver_id
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


-- ── 4. Reattach triggers ──────────────────────────────────────────────────────

CREATE TRIGGER trg_reaction_points_insert
  AFTER INSERT ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.award_reaction_points();

CREATE TRIGGER trg_reaction_points_delete
  AFTER DELETE ON public.recognition_reactions
  FOR EACH ROW EXECUTE FUNCTION public.award_reaction_points();
