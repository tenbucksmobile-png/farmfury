-- =============================================================================
-- Migration 068 — Birthday & Work Anniversary Celebrations
--
-- 1. Add date_of_birth + start_date columns to employees
-- 2. Create celebrations table (one row per employee per event per day)
-- 3. RLS: employees see their hotel; APA sees all
-- 4. generate_today_celebrations() — called daily by the edge function
-- =============================================================================


-- ─── 1. Employee date columns ─────────────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS date_of_birth date,
  ADD COLUMN IF NOT EXISTS start_date    date;

COMMENT ON COLUMN public.employees.date_of_birth IS
  'Employee date of birth. Used to generate birthday celebration cards.';
COMMENT ON COLUMN public.employees.start_date IS
  'Employment start date. Used to generate work anniversary celebration cards.';


-- ─── 2. Celebrations table ───────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.celebrations (
  id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id   uuid        NOT NULL REFERENCES public.employees(id) ON DELETE CASCADE,
  hotel         text        NOT NULL,
  type          text        NOT NULL CHECK (type IN ('birthday', 'anniversary')),
  milestone     integer,    -- years of service (anniversary only; null for birthday)
  celebrated_on date        NOT NULL DEFAULT CURRENT_DATE,
  created_at    timestamptz NOT NULL DEFAULT now(),

  -- Prevent duplicates if the function runs more than once on the same day
  UNIQUE (employee_id, type, celebrated_on)
);

CREATE INDEX IF NOT EXISTS idx_celebrations_employee
  ON public.celebrations (employee_id);

CREATE INDEX IF NOT EXISTS idx_celebrations_hotel
  ON public.celebrations (hotel);

CREATE INDEX IF NOT EXISTS idx_celebrations_date
  ON public.celebrations (celebrated_on DESC);

COMMENT ON TABLE public.celebrations IS
  'Auto-generated birthday and work anniversary records.
   Populated daily by the daily-celebrations edge function.
   Mobile feed queries this table to render celebration cards.';


-- ─── 3. RLS ──────────────────────────────────────────────────────────────────

ALTER TABLE public.celebrations ENABLE ROW LEVEL SECURITY;

CREATE POLICY "celebrations_hotel_select"
  ON public.celebrations
  FOR SELECT
  TO anon, authenticated
  USING (
    hotel = public.current_employee_hotel()
    OR public.current_employee_is_apa()
  );


-- ─── 4. generate_today_celebrations() ────────────────────────────────────────
--
-- Inserts celebration rows for today's birthdays and work anniversaries.
-- ON CONFLICT DO NOTHING makes it safe to call multiple times.
-- Returns the number of new rows inserted.

CREATE OR REPLACE FUNCTION public.generate_today_celebrations()
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_today    date    := CURRENT_DATE;
  v_inserted integer := 0;
  v_count    integer;
BEGIN
  -- ── Birthdays ────────────────────────────────────────────────────────────────
  INSERT INTO public.celebrations (employee_id, hotel, type, celebrated_on)
  SELECT id, hotel, 'birthday', v_today
  FROM   public.employees
  WHERE  date_of_birth IS NOT NULL
    AND  status = 'active'
    AND  EXTRACT(MONTH FROM date_of_birth) = EXTRACT(MONTH FROM v_today)
    AND  EXTRACT(DAY   FROM date_of_birth) = EXTRACT(DAY   FROM v_today)
  ON CONFLICT (employee_id, type, celebrated_on) DO NOTHING;

  GET DIAGNOSTICS v_count = ROW_COUNT;
  v_inserted := v_inserted + v_count;

  -- ── Work anniversaries (every year of service) ───────────────────────────────
  INSERT INTO public.celebrations (employee_id, hotel, type, milestone, celebrated_on)
  SELECT
    id,
    hotel,
    'anniversary',
    EXTRACT(YEAR FROM age(v_today, start_date))::integer AS years,
    v_today
  FROM   public.employees
  WHERE  start_date IS NOT NULL
    AND  status = 'active'
    AND  start_date < v_today
    AND  EXTRACT(MONTH FROM start_date) = EXTRACT(MONTH FROM v_today)
    AND  EXTRACT(DAY   FROM start_date) = EXTRACT(DAY   FROM v_today)
  ON CONFLICT (employee_id, type, celebrated_on) DO NOTHING;

  GET DIAGNOSTICS v_count = ROW_COUNT;
  v_inserted := v_inserted + v_count;

  RETURN v_inserted;
END;
$$;

GRANT EXECUTE ON FUNCTION public.generate_today_celebrations()
  TO service_role;

COMMENT ON FUNCTION public.generate_today_celebrations IS
  'Inserts celebration rows for today''s birthdays and work anniversaries.
   Called daily by the daily-celebrations edge function at 06:00 local time.
   Safe to re-run (ON CONFLICT DO NOTHING).';
