-- ─── Monthly Legends ─────────────────────────────────────────────────────────
--
-- Stores the Employee of the Month winner for each hotel, month, and year.
-- Populated automatically by the award-monthly-legend Edge Function on the
-- last day of each month via pg_cron.
--
-- One row per hotel per month — UNIQUE(hotel, month, year) enforces this.

CREATE TABLE IF NOT EXISTS monthly_legends (
  id              uuid        DEFAULT gen_random_uuid() PRIMARY KEY,
  hotel           text        NOT NULL,
  employee_id     uuid        NOT NULL REFERENCES employees(id) ON DELETE CASCADE,
  full_name       text        NOT NULL,
  job_title       text,
  avatar_url      text,
  month           int         NOT NULL CHECK (month BETWEEN 1 AND 12),
  year            int         NOT NULL CHECK (year >= 2024),
  total_points    int         NOT NULL DEFAULT 0,
  points_awarded  int         NOT NULL DEFAULT 500,
  recognition_id  uuid        REFERENCES recognitions(id) ON DELETE SET NULL,
  created_at      timestamptz DEFAULT now(),
  UNIQUE(hotel, month, year)
);

-- ─── RLS ─────────────────────────────────────────────────────────────────────

ALTER TABLE monthly_legends ENABLE ROW LEVEL SECURITY;

CREATE POLICY "monthly_legends_select"
  ON monthly_legends FOR SELECT
  USING (hotel = current_employee_hotel());

-- ─── Index ───────────────────────────────────────────────────────────────────

CREATE INDEX idx_monthly_legends_hotel_year
  ON monthly_legends(hotel, year DESC, month DESC);

-- ─── pg_cron schedule ────────────────────────────────────────────────────────
--
-- Runs at 23:45 on the last day of every month.
-- The edge function itself is idempotent (ON CONFLICT DO NOTHING).
--
-- Enable pg_cron extension first if not already enabled:
--   CREATE EXTENSION IF NOT EXISTS pg_cron;
--
-- Then schedule (replace <project-ref> with your Supabase project ref):
--
-- SELECT cron.schedule(
--   'award-monthly-legend',
--   '45 23 28-31 * *',
--   $$
--     SELECT CASE
--       WHEN date_trunc('day', now()) = date_trunc('month', now()) + interval '1 month' - interval '1 day'
--       THEN net.http_post(
--         url := 'https://<project-ref>.supabase.co/functions/v1/award-monthly-legend',
--         headers := jsonb_build_object(
--           'Content-Type', 'application/json',
--           'x-cron-secret', '<your-cron-secret>'
--         ),
--         body := '{}'::jsonb
--       )
--     END;
--   $$
-- );
