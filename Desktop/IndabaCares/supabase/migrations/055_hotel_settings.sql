-- 055_hotel_settings.sql
--
-- Per-hotel feature flag configuration.
-- Each hotel has one row. Missing rows fall back to all-enabled defaults in the app.
-- Flags can be toggled by admins via the admin dashboard without an app release.

CREATE TABLE IF NOT EXISTS hotel_settings (
  hotel        text        PRIMARY KEY,
  feature_flags jsonb      NOT NULL DEFAULT '{}'::jsonb,
  created_at   timestamptz DEFAULT now(),
  updated_at   timestamptz DEFAULT now()
);

-- Seed with all-enabled defaults for each existing hotel
INSERT INTO hotel_settings (hotel, feature_flags)
SELECT DISTINCT hotel, '{
  "moods_enabled":           true,
  "rewards_enabled":         true,
  "skills_enabled":          true,
  "leaderboards_enabled":    true,
  "custom_hashtags_enabled": true,
  "boost_enabled":           true
}'::jsonb
FROM employees
ON CONFLICT (hotel) DO NOTHING;

-- RLS: employees can SELECT their own hotel's settings (read-only).
-- Only service_role can UPDATE.
ALTER TABLE hotel_settings ENABLE ROW LEVEL SECURITY;

CREATE POLICY "hotel_settings_employee_read"
  ON hotel_settings FOR SELECT
  USING (hotel = current_employee_hotel());

-- RPC: get_hotel_settings
-- Returns the feature_flags JSONB for the current session's hotel.
-- SECURITY DEFINER so it works even before RLS is resolved.
CREATE OR REPLACE FUNCTION get_hotel_settings(p_hotel text)
RETURNS jsonb
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_flags jsonb;
BEGIN
  SELECT feature_flags INTO v_flags
  FROM hotel_settings
  WHERE hotel = p_hotel
  LIMIT 1;

  RETURN COALESCE(v_flags, '{
    "moods_enabled":           true,
    "rewards_enabled":         true,
    "skills_enabled":          true,
    "leaderboards_enabled":    true,
    "custom_hashtags_enabled": true,
    "boost_enabled":           true
  }'::jsonb);
END;
$$;
