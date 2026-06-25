-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 069: Replace submit_mood with employee-schema version
--
-- The old submit_mood used profiles / company_id (legacy schema).
-- This replaces it with the current employee-based schema:
--   • mood_entries.employee_id  (added in migration 059)
--   • employees.points_balance  (running total)
--   • points_ledger             (audit trail, source = 'mood_checkin')
--
-- Once-per-day is enforced by a UNIQUE constraint on (employee_id, entry_date).
-- ─────────────────────────────────────────────────────────────────────────────

-- Ensure UNIQUE constraint exists on mood_entries (guard against re-running)
ALTER TABLE public.mood_entries
  DROP CONSTRAINT IF EXISTS mood_entries_employee_id_entry_date_key;

ALTER TABLE public.mood_entries
  ADD CONSTRAINT mood_entries_employee_id_entry_date_key
  UNIQUE (employee_id, entry_date);

-- ── Drop the old legacy signature (profiles / company_id schema) ─────────────
DROP FUNCTION IF EXISTS public.submit_mood(uuid, uuid, public.mood_value, text);

-- ── Create the new employee-schema version ───────────────────────────────────
CREATE OR REPLACE FUNCTION public.submit_mood(
  p_employee_id uuid,
  p_hotel       text,
  p_mood        text,           -- 'awful' | 'bad' | 'okay' | 'good' | 'amazing'
  p_note        text DEFAULT NULL
)
RETURNS uuid   -- returns mood_entry id
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_entry_id uuid;
  v_points   integer := 5;
BEGIN
  -- ── Validate employee belongs to stated hotel ────────────────────────────
  IF NOT EXISTS (
    SELECT 1 FROM public.employees
    WHERE id = p_employee_id
      AND hotel = p_hotel
      AND status = 'active'
  ) THEN
    RAISE EXCEPTION 'Employee not found'
      USING ERRCODE = 'P0002';
  END IF;

  -- ── Once per day (UNIQUE constraint is belt-and-suspenders) ─────────────
  IF EXISTS (
    SELECT 1 FROM public.mood_entries
    WHERE employee_id = p_employee_id
      AND entry_date  = current_date
  ) THEN
    RAISE EXCEPTION 'Mood already submitted today'
      USING ERRCODE = 'P3001';
  END IF;

  -- ── Insert mood entry ────────────────────────────────────────────────────
  INSERT INTO public.mood_entries (employee_id, mood, note, entry_date)
  VALUES (p_employee_id, p_mood::public.mood_value, p_note, current_date)
  RETURNING id INTO v_entry_id;

  -- ── Award 5 points ───────────────────────────────────────────────────────
  UPDATE public.employees
  SET    points_balance = points_balance + v_points
  WHERE  id = p_employee_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (p_employee_id, v_points, 'mood_checkin', p_hotel);

  RETURN v_entry_id;
END;
$$;

COMMENT ON FUNCTION public.submit_mood(uuid, text, text, text) IS
  'Employee daily mood check-in. Once per day; awards 5 points to employees.points_balance '
  'and appends an audit row to points_ledger (source = mood_checkin).';

-- Grant execute to authenticated and anon (called from mobile via RLS session)
GRANT EXECUTE ON FUNCTION public.submit_mood(uuid, text, text, text) TO authenticated, anon;
