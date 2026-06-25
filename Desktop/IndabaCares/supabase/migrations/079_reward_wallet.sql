-- =============================================================================
-- Migration 079 — Reward Wallet + RP Conversion
--
-- Separates Recognition Points (engagement score) from the Reward Wallet
-- (spendable currency). Introduces a 5:1 conversion mechanism.
--
-- New employee columns:
--   converted_points      — total RP locked into the wallet (monotone ↑)
--   reward_wallet_balance — spendable credits (↑ on conversion, ↓ on redemption)
--
-- New table:
--   reward_conversions — immutable audit log of every conversion event
--
-- New RPCs:
--   convert_points_to_wallet(p_employee_id, p_amount)
--   get_wallet_stats(p_employee_id)
--
-- Updated RPCs:
--   redeem_reward()    — deducts reward_wallet_balance (not points_balance)
--   reject_redemption() — refunds reward_wallet_balance (not points_balance)
--
-- Guard:
--   trg_guard_wallet_balance — blocks direct UPDATE of wallet columns;
--   RPCs set indabacares.allow_wallet_update GUC to bypass.
-- =============================================================================


-- ─────────────────────────────────────────────────────────────────────────────
-- 1. New columns on employees
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS converted_points      integer NOT NULL DEFAULT 0
    CHECK (converted_points >= 0),
  ADD COLUMN IF NOT EXISTS reward_wallet_balance integer NOT NULL DEFAULT 0
    CHECK (reward_wallet_balance >= 0);

COMMENT ON COLUMN public.employees.converted_points IS
  'Running total of Recognition Points that have been converted to wallet credits. '
  'Monotonically increasing. availableToConvert = points_balance - converted_points.';

COMMENT ON COLUMN public.employees.reward_wallet_balance IS
  'Spendable Reward Wallet credits. Incremented by convert_points_to_wallet(); '
  'decremented by redeem_reward(); restored by reject_redemption().';


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. Guard trigger — blocks direct UPDATE of wallet columns
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.guard_wallet_balance()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  IF current_setting('indabacares.allow_wallet_update', true) IS DISTINCT FROM 'true' THEN
    RAISE EXCEPTION
      'Direct modification of reward_wallet_balance / converted_points is forbidden. '
      'Use convert_points_to_wallet() or redeem_reward().'
      USING ERRCODE = 'P0007';
  END IF;
  RETURN NEW;
END;
$$;

COMMENT ON FUNCTION public.guard_wallet_balance() IS
  'Blocks direct UPDATE of reward_wallet_balance or converted_points unless '
  'indabacares.allow_wallet_update = ''true'' is set for the transaction.';

CREATE TRIGGER trg_guard_wallet_balance
  BEFORE UPDATE OF reward_wallet_balance, converted_points ON public.employees
  FOR EACH ROW EXECUTE FUNCTION public.guard_wallet_balance();


-- ─────────────────────────────────────────────────────────────────────────────
-- 3. reward_conversions — immutable audit log
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE public.reward_conversions (
  id             uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id    uuid        NOT NULL REFERENCES public.employees(id) ON DELETE CASCADE,
  hotel          text        NOT NULL,
  rp_converted   integer     NOT NULL CHECK (rp_converted   > 0 AND rp_converted % 5 = 0),
  credits_earned integer     NOT NULL CHECK (credits_earned > 0),
  created_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_conversions_employee
  ON public.reward_conversions (employee_id, created_at DESC);

CREATE INDEX idx_conversions_hotel
  ON public.reward_conversions (hotel, created_at DESC);

ALTER TABLE public.reward_conversions ENABLE ROW LEVEL SECURITY;

CREATE POLICY "conversions_own_select"
  ON public.reward_conversions
  FOR SELECT TO anon, authenticated
  USING (employee_id = public.current_employee_id());

COMMENT ON TABLE public.reward_conversions IS
  'Immutable log of every RP → wallet-credit conversion. '
  'rp_converted is always a multiple of 5. credits_earned = rp_converted / 5.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 4. get_wallet_stats() — single-call summary for the Reward tab
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.get_wallet_stats(p_employee_id uuid)
RETURNS jsonb
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
  SELECT jsonb_build_object(
    'wallet_balance',       e.reward_wallet_balance,
    'converted_points',     e.converted_points,
    'points_balance',       e.points_balance,
    'available_to_convert', GREATEST(0, e.points_balance - e.converted_points),
    'max_credits',          GREATEST(0, (e.points_balance - e.converted_points) / 5),
    'redeemed_total',       COALESCE((
      SELECT SUM(r2.points_used)
      FROM   public.redemptions r2
      WHERE  r2.employee_id = p_employee_id
        AND  r2.status IN ('pending', 'approved', 'fulfilled')
    ), 0),
    'converted_this_month', COALESCE((
      SELECT SUM(rc.credits_earned)
      FROM   public.reward_conversions rc
      WHERE  rc.employee_id = p_employee_id
        AND  DATE_TRUNC('month', rc.created_at AT TIME ZONE 'UTC')
             = DATE_TRUNC('month', NOW() AT TIME ZONE 'UTC')
    ), 0)
  )
  FROM public.employees e
  WHERE e.id = p_employee_id;
$$;

GRANT EXECUTE ON FUNCTION public.get_wallet_stats(uuid) TO anon, authenticated;

COMMENT ON FUNCTION public.get_wallet_stats IS
  'Returns a JSON object with all wallet-related data for an employee: '
  'wallet_balance, converted_points, points_balance, available_to_convert, '
  'max_credits (max whole credits available now), redeemed_total (lifetime), '
  'converted_this_month (credits earned from conversions this calendar month).';


-- ─────────────────────────────────────────────────────────────────────────────
-- 5. convert_points_to_wallet() — the conversion RPC
-- ─────────────────────────────────────────────────────────────────────────────
--
-- Rules:
--   • p_amount must be a positive multiple of 5.
--   • p_amount must not exceed (points_balance − converted_points).
--   • credits_earned = p_amount / 5.
--   • converted_points += p_amount    (prevents double-dipping)
--   • reward_wallet_balance += credits (spendable currency)

CREATE OR REPLACE FUNCTION public.convert_points_to_wallet(
  p_employee_id uuid,
  p_amount      integer
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_points_balance    integer;
  v_converted_points  integer;
  v_wallet_balance    integer;
  v_hotel             text;
  v_available         integer;
  v_credits           integer;
BEGIN
  -- ── Input validation ──────────────────────────────────────────────────────
  IF p_amount <= 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Amount must be greater than 0.');
  END IF;

  IF p_amount % 5 <> 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Amount must be a multiple of 5.');
  END IF;

  -- ── Lock row (prevents concurrent double-conversion) ─────────────────────
  SELECT points_balance, converted_points, reward_wallet_balance, hotel
  INTO   v_points_balance, v_converted_points, v_wallet_balance, v_hotel
  FROM   public.employees
  WHERE  id     = p_employee_id
    AND  status = 'active'
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Active employee not found.');
  END IF;

  v_available := GREATEST(0, v_points_balance - v_converted_points);

  IF p_amount > v_available THEN
    RETURN jsonb_build_object(
      'ok',        false,
      'error',     'Not enough unconverted Recognition Points.',
      'available', v_available,
      'requested', p_amount
    );
  END IF;

  v_credits := p_amount / 5;

  -- ── Atomic update ─────────────────────────────────────────────────────────
  PERFORM set_config('indabacares.allow_wallet_update', 'true', true);

  UPDATE public.employees
  SET    converted_points      = converted_points      + p_amount,
         reward_wallet_balance = reward_wallet_balance + v_credits
  WHERE  id = p_employee_id;

  -- ── Audit log ─────────────────────────────────────────────────────────────
  INSERT INTO public.reward_conversions (employee_id, hotel, rp_converted, credits_earned)
  VALUES (p_employee_id, v_hotel, p_amount, v_credits);

  RETURN jsonb_build_object(
    'ok',                 true,
    'rp_converted',       p_amount,
    'credits_earned',     v_credits,
    'new_wallet_balance', v_wallet_balance + v_credits
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.convert_points_to_wallet(uuid, integer) TO anon, authenticated;

COMMENT ON FUNCTION public.convert_points_to_wallet IS
  'Converts Recognition Points to Reward Wallet credits at a 5:1 ratio. '
  'p_amount must be a positive multiple of 5 and <= (points_balance - converted_points). '
  'points_balance is never touched — it remains the engagement score. '
  'Writes an immutable row to reward_conversions for audit purposes.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 6. redeem_reward() — deduct reward_wallet_balance (not points_balance)
-- ─────────────────────────────────────────────────────────────────────────────

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
  v_hotel           text;
  v_wallet_balance  integer;
  v_redemption_id   uuid;
BEGIN
  -- Lock reward row (prevents race on stock)
  SELECT points_required, stock, hotel
  INTO   v_points_required, v_stock, v_hotel
  FROM   public.rewards
  WHERE  id = p_reward_id
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Reward not found.');
  END IF;

  IF v_stock IS NOT NULL AND v_stock <= 0 THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Out of stock.');
  END IF;

  -- Lock employee row
  SELECT reward_wallet_balance
  INTO   v_wallet_balance
  FROM   public.employees
  WHERE  id     = p_employee_id
    AND  status = 'active'
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Active employee not found.');
  END IF;

  IF v_wallet_balance < v_points_required THEN
    RETURN jsonb_build_object(
      'ok',       false,
      'error',    'Insufficient Reward Wallet balance.',
      'balance',  v_wallet_balance,
      'required', v_points_required
    );
  END IF;

  -- ── Atomic mutations ───────────────────────────────────────────────────────

  PERFORM set_config('indabacares.allow_wallet_update', 'true', true);

  -- 1. Deduct wallet credits
  UPDATE public.employees
  SET    reward_wallet_balance = reward_wallet_balance - v_points_required
  WHERE  id = p_employee_id;

  -- 2. Reserve one unit of stock
  IF v_stock IS NOT NULL THEN
    UPDATE public.rewards SET stock = stock - 1 WHERE id = p_reward_id;
  END IF;

  -- 3. Create redemption record (status = 'pending')
  INSERT INTO public.redemptions (employee_id, reward_id, points_used, hotel)
  VALUES (p_employee_id, p_reward_id, v_points_required, v_hotel)
  RETURNING id INTO v_redemption_id;

  RETURN jsonb_build_object(
    'ok',             true,
    'redemption_id',  v_redemption_id,
    'points_used',    v_points_required,
    'new_balance',    v_wallet_balance - v_points_required
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.redeem_reward(uuid, uuid) TO anon, authenticated;

COMMENT ON FUNCTION public.redeem_reward IS
  'Atomically deducts reward_wallet_balance and creates a pending redemption. '
  'Admin then approves, fulfils, or rejects. '
  'points_balance (engagement score) is never touched.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 7. reject_redemption() — refund reward_wallet_balance (not points_balance)
-- ─────────────────────────────────────────────────────────────────────────────

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
  WHERE  id = p_redemption_id
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Redemption not found.');
  END IF;

  IF v_status NOT IN ('pending', 'approved') THEN
    RETURN jsonb_build_object('ok', false, 'error',
      format('Cannot reject a %s redemption.', v_status));
  END IF;

  -- Mark rejected
  UPDATE public.redemptions
  SET    status           = 'rejected',
         rejected_at      = now(),
         rejection_reason = p_reason
  WHERE  id = p_redemption_id;

  -- Refund wallet credits to employee
  PERFORM set_config('indabacares.allow_wallet_update', 'true', true);

  UPDATE public.employees
  SET    reward_wallet_balance = reward_wallet_balance + v_points_used
  WHERE  id = v_employee_id;

  -- Restore stock
  UPDATE public.rewards
  SET    stock = stock + 1
  WHERE  id    = v_reward_id
    AND  stock IS NOT NULL;

  RETURN jsonb_build_object(
    'ok',              true,
    'status',          'rejected',
    'credits_refunded', v_points_used
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.reject_redemption(uuid, text) TO anon, authenticated;

COMMENT ON FUNCTION public.reject_redemption IS
  'Admin: rejects a pending or approved redemption. '
  'Refunds wallet credits (not points_balance). Restores reward stock.';
