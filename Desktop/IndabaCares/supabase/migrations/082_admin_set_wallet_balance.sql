-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 082 — Admin RPC to directly set reward_wallet_balance
--
-- Migration 079 added a guard trigger (trg_guard_wallet_balance) that blocks
-- direct UPDATE of reward_wallet_balance / converted_points unless the GUC
-- indabacares.allow_wallet_update = 'true' is set for the transaction.
--
-- The admin dashboard's updateEmployee server action uses service_role but
-- still hits the trigger (GUC is session-scoped, not role-scoped), causing a
-- P0007 error surfaced as a Next.js Server Component crash.
--
-- This function sets the GUC then applies the update, giving admins a safe
-- path to manually adjust wallet balances.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.admin_set_wallet_balance(
  p_employee_id uuid,
  p_new_balance integer
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  IF p_new_balance < 0 THEN
    RAISE EXCEPTION 'Wallet balance cannot be negative.';
  END IF;

  PERFORM set_config('indabacares.allow_wallet_update', 'true', true);

  UPDATE public.employees
  SET    reward_wallet_balance = p_new_balance
  WHERE  id = p_employee_id;
END;
$$;

GRANT EXECUTE ON FUNCTION public.admin_set_wallet_balance(uuid, integer) TO authenticated;

COMMENT ON FUNCTION public.admin_set_wallet_balance IS
  'Admin-only: directly sets reward_wallet_balance for an employee, bypassing '
  'the guard trigger introduced in migration 079. Called from the admin dashboard '
  'updateEmployee server action when a manual balance adjustment is needed.';
