-- 054_push_tokens.sql
--
-- Push notification token registry.
-- Stores Expo push tokens per employee/device. One token per employee+token pair
-- (UNIQUE constraint prevents duplicate rows on re-registration).
-- Tokens are read exclusively by service_role (Edge Functions / admin) for delivery.

-- ─── Table ───────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS push_tokens (
  id          uuid        DEFAULT gen_random_uuid() PRIMARY KEY,
  employee_id uuid        NOT NULL REFERENCES employees(id) ON DELETE CASCADE,
  hotel       text        NOT NULL,
  token       text        NOT NULL,
  platform    text        NOT NULL CHECK (platform IN ('ios', 'android', 'web')),
  created_at  timestamptz DEFAULT now(),
  updated_at  timestamptz DEFAULT now(),
  UNIQUE (employee_id, token)
);

CREATE INDEX IF NOT EXISTS idx_push_tokens_employee ON push_tokens(employee_id);
CREATE INDEX IF NOT EXISTS idx_push_tokens_hotel    ON push_tokens(hotel);

ALTER TABLE push_tokens ENABLE ROW LEVEL SECURITY;

-- Employees cannot SELECT, INSERT, UPDATE, or DELETE directly.
-- All writes go through the upsert_push_token() SECURITY DEFINER RPC below.
-- Reads are service_role only (used when broadcasting notifications).

-- ─── RPC: upsert_push_token ──────────────────────────────────────────────────
--
-- Upserts a push token for the authenticated employee.
-- Parameters are passed from the validated client session — the function re-validates
-- that the employee exists and is active before writing.
-- On conflict (same employee + same token), updates the platform and timestamp only.

CREATE OR REPLACE FUNCTION upsert_push_token(
  p_employee_id uuid,
  p_hotel       text,
  p_token       text,
  p_platform    text
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  -- Validate platform value
  IF p_platform NOT IN ('ios', 'android', 'web') THEN
    RAISE EXCEPTION 'Invalid platform: %', p_platform USING ERRCODE = '22023';
  END IF;

  -- Validate token is not blank
  IF p_token IS NULL OR trim(p_token) = '' THEN
    RAISE EXCEPTION 'Token must not be empty' USING ERRCODE = '22023';
  END IF;

  -- Ensure employee is active and belongs to the declared hotel
  IF NOT EXISTS (
    SELECT 1 FROM employees
    WHERE id     = p_employee_id
      AND hotel  = p_hotel
      AND status = 'active'
  ) THEN
    RAISE EXCEPTION 'Employee not found or inactive' USING ERRCODE = '42501';
  END IF;

  INSERT INTO push_tokens (employee_id, hotel, token, platform, updated_at)
  VALUES (p_employee_id, p_hotel, p_token, p_platform, now())
  ON CONFLICT (employee_id, token) DO UPDATE
    SET platform   = EXCLUDED.platform,
        updated_at = now();
END;
$$;

-- ─── RPC: delete_push_token ──────────────────────────────────────────────────
--
-- Removes a specific push token on logout or permission revoke.

CREATE OR REPLACE FUNCTION delete_push_token(
  p_employee_id uuid,
  p_token       text
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  DELETE FROM push_tokens
  WHERE employee_id = p_employee_id
    AND token       = p_token;
END;
$$;
