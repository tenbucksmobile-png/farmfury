-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 020 — Reward Redemption Engine
--
-- Aligns the redemptions table to spec and adds the full admin approval flow.
--
-- Schema changes (delta on migration 019):
--   • Rename  points_spent → points_used
--   • Add     status value  'rejected'
--   • Add     audit columns approved_at, rejected_at, fulfilled_at, rejection_reason
--
-- Redemption lifecycle:
--
--   Employee               Admin
--   ─────────────────      ────────────────────────────────
--   redeem_reward()   →    pending
--                          approve_redemption()  → approved
--                          fulfill_redemption()  → fulfilled
--                          reject_redemption()   → rejected  (points refunded)
--
-- RPCs (all SECURITY DEFINER):
--   redeem_reward(p_employee_id, p_reward_id)
--   approve_redemption(p_redemption_id)
--   reject_redemption(p_redemption_id, p_reason?)
--   fulfill_redemption(p_redemption_id)
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. Rename column ──────────────────────────────────────────────────────────

DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE  table_name = 'redemptions' AND column_name = 'points_spent'
  ) THEN
    ALTER TABLE public.redemptions RENAME COLUMN points_spent TO points_used;
  END IF;
END;
$$;


-- ── 2. Status constraint — add 'rejected' ─────────────────────────────────────

ALTER TABLE public.redemptions
  DROP CONSTRAINT IF EXISTS redemptions_status_check;

ALTER TABLE public.redemptions
  ADD CONSTRAINT redemptions_status_check
  CHECK (status IN ('pending', 'approved', 'rejected', 'fulfilled', 'cancelled'));


-- ── 3. Audit columns ──────────────────────────────────────────────────────────

ALTER TABLE public.redemptions
  ADD COLUMN IF NOT EXISTS approved_at       timestamptz,
  ADD COLUMN IF NOT EXISTS rejected_at       timestamptz,
  ADD COLUMN IF NOT EXISTS fulfilled_at      timestamptz,
  ADD COLUMN IF NOT EXISTS rejection_reason  text;

COMMENT ON COLUMN public.redemptions.rejection_reason IS
  'Free-text reason supplied by admin when rejecting a redemption.';


-- ── 4. Admin index ────────────────────────────────────────────────────────────

-- Admin dashboard queries pending/open redemptions across a hotel
CREATE INDEX IF NOT EXISTS idx_redemptions_hotel_status
  ON public.redemptions (hotel, status, created_at DESC);


-- ── 5. redeem_reward RPC (updated for points_used column) ────────────────────
-- Atomic: check balance + stock → deduct points → decrement stock → insert row.
-- Uses SELECT … FOR UPDATE to prevent concurrent over-redemption.

CREATE OR REPLACE FUNCTION public.redeem_reward(
  p_employee_id uuid,
  p_reward_id   uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_points_required integer;
  v_stock           integer;
  v_hotel           text;
  v_current_balance integer;
  v_redemption_id   uuid;
BEGIN
  -- Lock reward row (prevents race conditions on stock)
  SELECT points_required, stock, hotel
  INTO   v_points_required, v_stock, v_hotel
  FROM   public.rewards
  WHERE  id = p_reward_id
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Reward not found.');
  END IF;

  IF v_stock <= 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'This reward is out of stock.');
  END IF;

  -- Lock employee row
  SELECT points_balance
  INTO   v_current_balance
  FROM   public.employees
  WHERE  id = p_employee_id
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Employee not found.');
  END IF;

  IF v_current_balance < v_points_required THEN
    RETURN jsonb_build_object(
      'ok',       false,
      'error',    'Not enough points to redeem this reward.',
      'balance',  v_current_balance,
      'required', v_points_required
    );
  END IF;

  -- ── Atomic mutations ───────────────────────────────────────────────────────

  -- 1. Deduct points
  UPDATE public.employees
  SET    points_balance = points_balance - v_points_required
  WHERE  id = p_employee_id;

  -- 2. Reserve one unit of stock
  UPDATE public.rewards
  SET    stock = stock - 1
  WHERE  id = p_reward_id;

  -- 3. Create redemption record (status = 'pending')
  INSERT INTO public.redemptions (employee_id, reward_id, points_used, hotel)
  VALUES (p_employee_id, p_reward_id, v_points_required, v_hotel)
  RETURNING id INTO v_redemption_id;

  RETURN jsonb_build_object(
    'ok',            true,
    'redemption_id', v_redemption_id,
    'points_used',   v_points_required,
    'new_balance',   v_current_balance - v_points_required
  );
END;
$func$;

GRANT EXECUTE ON FUNCTION public.redeem_reward(uuid, uuid) TO anon;
GRANT EXECUTE ON FUNCTION public.redeem_reward(uuid, uuid) TO authenticated;

COMMENT ON FUNCTION public.redeem_reward IS
  'Step 1 of the redemption flow. Atomically deducts points and creates a pending '
  'redemption record. Admin then approves, fulfills, or rejects.';


-- ── 6. approve_redemption RPC ─────────────────────────────────────────────────
-- Moves a pending redemption to approved.  No points change.

CREATE OR REPLACE FUNCTION public.approve_redemption(
  p_redemption_id uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_status text;
BEGIN
  SELECT status INTO v_status
  FROM   public.redemptions
  WHERE  id = p_redemption_id
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Redemption not found.');
  END IF;

  IF v_status <> 'pending' THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', format('Cannot approve a %s redemption.', v_status)
    );
  END IF;

  UPDATE public.redemptions
  SET    status      = 'approved',
         approved_at = now()
  WHERE  id = p_redemption_id;

  RETURN jsonb_build_object('ok', true, 'status', 'approved');
END;
$func$;

GRANT EXECUTE ON FUNCTION public.approve_redemption(uuid) TO anon;
GRANT EXECUTE ON FUNCTION public.approve_redemption(uuid) TO authenticated;

COMMENT ON FUNCTION public.approve_redemption IS
  'Admin: moves pending → approved.  Triggers fulfilment workflow.';


-- ── 7. reject_redemption RPC ──────────────────────────────────────────────────
-- Rejects a pending or approved redemption.
-- Refunds points to employee and restores reward stock.

CREATE OR REPLACE FUNCTION public.reject_redemption(
  p_redemption_id uuid,
  p_reason        text DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_employee_id uuid;
  v_reward_id   uuid;
  v_points_used integer;
  v_status      text;
BEGIN
  SELECT employee_id, reward_id, points_used, status
  INTO   v_employee_id, v_reward_id, v_points_used, v_status
  FROM   public.redemptions
  WHERE  id = p_redemption_id
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Redemption not found.');
  END IF;

  IF v_status NOT IN ('pending', 'approved') THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', format('Cannot reject a %s redemption.', v_status)
    );
  END IF;

  -- Refund points to employee
  UPDATE public.employees
  SET    points_balance = points_balance + v_points_used
  WHERE  id = v_employee_id;

  -- Restore one unit of stock
  UPDATE public.rewards
  SET    stock = stock + 1
  WHERE  id = v_reward_id;

  -- Mark rejected
  UPDATE public.redemptions
  SET    status           = 'rejected',
         rejected_at      = now(),
         rejection_reason = p_reason
  WHERE  id = p_redemption_id;

  RETURN jsonb_build_object(
    'ok',              true,
    'status',          'rejected',
    'points_refunded', v_points_used
  );
END;
$func$;

GRANT EXECUTE ON FUNCTION public.reject_redemption(uuid, text) TO anon;
GRANT EXECUTE ON FUNCTION public.reject_redemption(uuid, text) TO authenticated;

COMMENT ON FUNCTION public.reject_redemption IS
  'Admin: rejects a pending or approved redemption, refunds points, restores stock.';


-- ── 8. fulfill_redemption RPC ─────────────────────────────────────────────────
-- Marks an approved redemption as fulfilled.  No points change.

CREATE OR REPLACE FUNCTION public.fulfill_redemption(
  p_redemption_id uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_status text;
BEGIN
  SELECT status INTO v_status
  FROM   public.redemptions
  WHERE  id = p_redemption_id
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Redemption not found.');
  END IF;

  IF v_status <> 'approved' THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', format('Cannot fulfil a %s redemption — must be approved first.', v_status)
    );
  END IF;

  UPDATE public.redemptions
  SET    status       = 'fulfilled',
         fulfilled_at = now()
  WHERE  id = p_redemption_id;

  RETURN jsonb_build_object('ok', true, 'status', 'fulfilled');
END;
$func$;

GRANT EXECUTE ON FUNCTION public.fulfill_redemption(uuid) TO anon;
GRANT EXECUTE ON FUNCTION public.fulfill_redemption(uuid) TO authenticated;

COMMENT ON FUNCTION public.fulfill_redemption IS
  'Admin: moves approved → fulfilled once the physical/digital reward is delivered.';


-- ── 9. Admin view — all redemptions for a hotel ───────────────────────────────

CREATE OR REPLACE VIEW public.redemptions_admin_view AS
SELECT
  r.id,
  r.status,
  r.points_used,
  r.hotel,
  r.created_at,
  r.approved_at,
  r.rejected_at,
  r.fulfilled_at,
  r.rejection_reason,
  e.full_name      AS employee_name,
  e.employee_code,
  e.department,
  rw.title         AS reward_title,
  rw.points_required,
  rw.image_url     AS reward_image_url
FROM public.redemptions r
JOIN public.employees   e  ON e.id  = r.employee_id
JOIN public.rewards     rw ON rw.id = r.reward_id;

COMMENT ON VIEW public.redemptions_admin_view IS
  'Flattened view of redemptions with employee and reward details. '
  'Intended for admin dashboard queries via service_role (bypasses RLS).';
