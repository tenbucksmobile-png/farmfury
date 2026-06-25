-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 071 — Fix redemptions table schema
--
-- Production DB skipped migration 020. This migration safely applies all
-- missing changes in one pass:
--   1. Rename points_spent → points_used
--   2. Add audit columns (approved_at, rejected_at, fulfilled_at, rejection_reason)
--   3. Widen status constraint to include 'rejected'
--   4. Replace RPCs to match the corrected schema
-- ─────────────────────────────────────────────────────────────────────────────

-- ── 1. Rename points_spent → points_used ─────────────────────────────────────

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

-- ── 2. Add missing audit columns ─────────────────────────────────────────────

ALTER TABLE public.redemptions
  ADD COLUMN IF NOT EXISTS approved_at       timestamptz,
  ADD COLUMN IF NOT EXISTS rejected_at       timestamptz,
  ADD COLUMN IF NOT EXISTS fulfilled_at      timestamptz,
  ADD COLUMN IF NOT EXISTS rejection_reason  text;

-- ── 3. Widen status constraint ───────────────────────────────────────────────

ALTER TABLE public.redemptions
  DROP CONSTRAINT IF EXISTS redemptions_status_check;

ALTER TABLE public.redemptions
  ADD CONSTRAINT redemptions_status_check
  CHECK (status IN ('pending', 'approved', 'rejected', 'fulfilled', 'cancelled'));

-- ── 4. Replace RPCs ───────────────────────────────────────────────────────────

-- redeem_reward
CREATE OR REPLACE FUNCTION public.redeem_reward(
  p_employee_id uuid,
  p_reward_id   uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_points_required integer;
  v_stock           integer;
  v_balance         integer;
  v_hotel           text;
  v_redemption_id   uuid;
BEGIN
  SELECT points_required, stock, hotel
  INTO   v_points_required, v_stock, v_hotel
  FROM   public.rewards
  WHERE  id = p_reward_id;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Reward not found');
  END IF;

  IF v_stock IS NOT NULL AND v_stock <= 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Out of stock');
  END IF;

  SELECT points_balance INTO v_balance
  FROM   public.employees
  WHERE  id = p_employee_id;

  IF v_balance < v_points_required THEN
    RETURN jsonb_build_object(
      'ok', false, 'error', 'Insufficient points',
      'balance', v_balance, 'required', v_points_required
    );
  END IF;

  UPDATE public.employees
  SET    points_balance = points_balance - v_points_required
  WHERE  id = p_employee_id;

  IF v_stock IS NOT NULL THEN
    UPDATE public.rewards SET stock = stock - 1 WHERE id = p_reward_id;
  END IF;

  INSERT INTO public.redemptions (employee_id, reward_id, points_used, hotel)
  VALUES (p_employee_id, p_reward_id, v_points_required, v_hotel)
  RETURNING id INTO v_redemption_id;

  RETURN jsonb_build_object(
    'ok',             true,
    'redemption_id',  v_redemption_id,
    'points_used',    v_points_required,
    'new_balance',    v_balance - v_points_required
  );
END;
$$;

-- approve_redemption
CREATE OR REPLACE FUNCTION public.approve_redemption(
  p_redemption_id uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  UPDATE public.redemptions
  SET    status      = 'approved',
         approved_at = now()
  WHERE  id = p_redemption_id
    AND  status = 'pending';

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Redemption not found or not pending');
  END IF;

  RETURN jsonb_build_object('ok', true, 'status', 'approved');
END;
$$;

-- reject_redemption
CREATE OR REPLACE FUNCTION public.reject_redemption(
  p_redemption_id uuid,
  p_reason        text DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_employee_id uuid;
  v_reward_id   uuid;
  v_points_used integer;
  v_status      text;
BEGIN
  SELECT employee_id, reward_id, points_used, status
  INTO   v_employee_id, v_reward_id, v_points_used, v_status
  FROM   public.redemptions
  WHERE  id = p_redemption_id;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Redemption not found');
  END IF;

  IF v_status NOT IN ('pending', 'approved') THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Cannot reject a ' || v_status || ' redemption');
  END IF;

  UPDATE public.redemptions
  SET    status           = 'rejected',
         rejected_at      = now(),
         rejection_reason = p_reason
  WHERE  id = p_redemption_id;

  UPDATE public.employees
  SET    points_balance = points_balance + v_points_used
  WHERE  id = v_employee_id;

  UPDATE public.rewards
  SET    stock = stock + 1
  WHERE  id = v_reward_id AND stock IS NOT NULL;

  RETURN jsonb_build_object(
    'ok',              true,
    'status',          'rejected',
    'points_refunded', v_points_used
  );
END;
$$;

-- fulfill_redemption
CREATE OR REPLACE FUNCTION public.fulfill_redemption(
  p_redemption_id uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  UPDATE public.redemptions
  SET    status       = 'fulfilled',
         fulfilled_at = now()
  WHERE  id = p_redemption_id
    AND  status = 'approved';

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Redemption not found or not approved');
  END IF;

  RETURN jsonb_build_object('ok', true, 'status', 'fulfilled');
END;
$$;
