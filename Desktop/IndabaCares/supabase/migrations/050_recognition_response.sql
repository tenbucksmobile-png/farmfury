-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 050 — Recipient response on recognitions
--
-- Adds recipient_response + recipient_responded_at to recognitions.
-- submit_recognition_response() RPC:
--   • Only the receiver may respond (enforced in the function).
--   • Can only respond once.
--   • Awards 5 pts to the responder via points_ledger.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.recognitions
  ADD COLUMN IF NOT EXISTS recipient_response      text,
  ADD COLUMN IF NOT EXISTS recipient_responded_at  timestamptz;

-- ─── RPC ─────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.submit_recognition_response(
  p_recognition_id uuid,
  p_employee_id    uuid,
  p_response       text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_rec public.recognitions%ROWTYPE;
BEGIN
  SELECT * INTO v_rec
  FROM   public.recognitions
  WHERE  id = p_recognition_id
  LIMIT  1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Recognition not found.');
  END IF;

  -- Only the receiver may respond
  IF v_rec.receiver_id <> p_employee_id THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Only the recipient may respond.');
  END IF;

  -- Can only respond once
  IF v_rec.recipient_response IS NOT NULL THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Already responded.');
  END IF;

  -- Save response
  UPDATE public.recognitions
  SET    recipient_response     = TRIM(p_response),
         recipient_responded_at = now()
  WHERE  id = p_recognition_id;

  -- Award 5 pts to the recipient
  INSERT INTO public.points_ledger (employee_id, hotel, points, source)
  VALUES (p_employee_id, v_rec.hotel, 5, 'recognition_response');

  RETURN jsonb_build_object('ok', true);

EXCEPTION WHEN others THEN
  RETURN jsonb_build_object('ok', false, 'error', SQLERRM);
END;
$$;

GRANT EXECUTE ON FUNCTION public.submit_recognition_response(uuid, uuid, text)
  TO anon, authenticated;

COMMENT ON FUNCTION public.submit_recognition_response IS
  'Lets the recognition recipient post a one-time preset response.
   Awards 5 pts to the recipient on success.';
