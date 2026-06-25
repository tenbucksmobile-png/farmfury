-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 083 — Admin RPC to directly set points_balance
--
-- points_balance is guarded by trg_guard_points_balance (migration 025/026).
-- The existing admin_grant_points() adds a *delta* — it cannot set an absolute
-- value, which is what the admin dashboard edit form needs.
--
-- This function sets the GUC then writes the exact target balance, logging the
-- delta to points_ledger for audit purposes.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.admin_set_points_balance(
  p_employee_id uuid,
  p_new_balance integer
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_current integer;
  v_delta   integer;
  v_hotel   text;
BEGIN
  IF p_new_balance < 0 THEN
    RAISE EXCEPTION 'Points balance cannot be negative.';
  END IF;

  SELECT points_balance, hotel
  INTO   v_current, v_hotel
  FROM   public.employees
  WHERE  id = p_employee_id;

  IF NOT FOUND THEN
    RAISE EXCEPTION 'Employee not found.';
  END IF;

  v_delta := p_new_balance - v_current;

  IF v_delta = 0 THEN
    RETURN;
  END IF;

  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = p_new_balance
  WHERE  id = p_employee_id;

  -- Audit trail
  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (p_employee_id, v_delta, 'admin_bonus', v_hotel);
END;
$$;

GRANT EXECUTE ON FUNCTION public.admin_set_points_balance(uuid, integer) TO authenticated;

COMMENT ON FUNCTION public.admin_set_points_balance IS
  'Admin-only: sets points_balance to an absolute value, bypassing the guard '
  'trigger from migration 025. Logs the delta to points_ledger with '
  'source = admin_adjustment for audit purposes.';
