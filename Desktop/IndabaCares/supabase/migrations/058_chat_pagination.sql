-- 058_chat_pagination.sql
--
-- Adds cursor-based pagination to get_chat_messages().
-- p_before_timestamp: only return messages created before this timestamp.
-- Enables "Load earlier messages" in the mobile chat screen.
--
-- If p_before_timestamp is NULL, the function returns the most recent p_limit rows
-- (the original behaviour — fully backwards-compatible).

CREATE OR REPLACE FUNCTION get_chat_messages(
  p_hotel            text,
  p_limit            int  DEFAULT 40,
  p_before_timestamp timestamptz DEFAULT NULL
)
RETURNS TABLE (
  id               uuid,
  body             text,
  hotel            text,
  created_at       timestamptz,
  sender_id        uuid,
  sender_name      text,
  sender_code      text,
  sender_position  text
)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  -- Hotel isolation: caller's hotel must match p_hotel
  IF current_employee_hotel() <> p_hotel THEN
    RAISE EXCEPTION 'Forbidden' USING ERRCODE = '42501';
  END IF;

  RETURN QUERY
  SELECT
    m.id,
    m.body,
    m.hotel,
    m.created_at,
    e.id             AS sender_id,
    e.full_name      AS sender_name,
    e.employee_code  AS sender_code,
    e.position       AS sender_position
  FROM messages m
  JOIN employees e ON e.id = m.sender_id
  WHERE m.hotel = p_hotel
    AND (p_before_timestamp IS NULL OR m.created_at < p_before_timestamp)
  ORDER BY m.created_at DESC
  LIMIT p_limit;
END;
$$;
