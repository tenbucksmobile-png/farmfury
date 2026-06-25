-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 049 — Recognition full-text search RPC
--
-- search_recognitions(hotel, search_term, limit)
--
-- Searches recognitions within a hotel across:
--   sender name, receiver name, position, department, badge, message,
--   and formatted date / time strings.
--
-- Returns a JSONB array matching the RecognitionFeedItem shape used
-- by the mobile app so no client-side transformation is required.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.search_recognitions(
  p_hotel  text,
  p_search text,
  p_limit  int DEFAULT 50
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_term text := TRIM(p_search);
BEGIN
  RETURN COALESCE(
    (
      SELECT jsonb_agg(row_to_json(r))
      FROM (
        SELECT
          rec.id,
          rec.message,
          rec.badge,
          rec.hotel,
          rec.created_at,

          jsonb_build_object(
            'id',            s.id,
            'full_name',     s.full_name,
            'employee_code', s.employee_code,
            'position',      s.position,
            'department',    s.department
          ) AS sender,

          jsonb_build_object(
            'id',            rv.id,
            'full_name',     rv.full_name,
            'employee_code', rv.employee_code,
            'position',      rv.position,
            'department',    rv.department
          ) AS receiver,

          (
            SELECT COALESCE(jsonb_build_array(jsonb_build_object('count', COUNT(*))), '[{"count":0}]'::jsonb)
            FROM   recognition_likes l WHERE l.recognition_id = rec.id
          ) AS likes_count,

          (
            SELECT COALESCE(jsonb_build_array(jsonb_build_object('count', COUNT(*))), '[{"count":0}]'::jsonb)
            FROM   recognition_comments c WHERE c.recognition_id = rec.id
          ) AS comments_count

        FROM  recognitions rec
        JOIN  employees    s  ON s.id  = rec.sender_id
        JOIN  employees    rv ON rv.id = rec.receiver_id

        WHERE rec.hotel = TRIM(p_hotel)
          AND (
            v_term = '' OR v_term IS NULL
            OR s.full_name    ILIKE '%' || v_term || '%'
            OR rv.full_name   ILIKE '%' || v_term || '%'
            OR s.position     ILIKE '%' || v_term || '%'
            OR rv.position    ILIKE '%' || v_term || '%'
            OR s.department   ILIKE '%' || v_term || '%'
            OR rv.department  ILIKE '%' || v_term || '%'
            OR rec.badge      ILIKE '%' || v_term || '%'
            OR rec.message    ILIKE '%' || v_term || '%'
            -- date formats: 2026-03-14  |  14 March 2026  |  March  |  14/03
            OR to_char(rec.created_at, 'YYYY-MM-DD')      ILIKE '%' || v_term || '%'
            OR to_char(rec.created_at, 'DD Month YYYY')   ILIKE '%' || v_term || '%'
            OR to_char(rec.created_at, 'Month')           ILIKE '%' || v_term || '%'
            OR to_char(rec.created_at, 'DD/MM/YYYY')      ILIKE '%' || v_term || '%'
            -- time: 14:30
            OR to_char(rec.created_at, 'HH24:MI')         ILIKE '%' || v_term || '%'
          )

        ORDER BY rec.created_at DESC
        LIMIT p_limit
      ) r
    ),
    '[]'::jsonb
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.search_recognitions(text, text, int)
  TO anon, authenticated;

COMMENT ON FUNCTION public.search_recognitions IS
  'Full-text search across recognitions within a hotel.
   Matches sender/receiver name, position, department, badge, message, date, and time.
   Returns a JSONB array matching the RecognitionFeedItem shape.';
