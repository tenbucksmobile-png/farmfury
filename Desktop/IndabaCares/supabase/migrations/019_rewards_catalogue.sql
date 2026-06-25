-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 019 — Rewards Catalogue System
--
-- Points model:
--   • employees.points_balance  — denormalized running total
--   • Each recognition received  → +10 points (trigger on recognitions)
--   • Each redemption            → -N points (via redeem_reward RPC, atomic)
--   • Cancellation               → +N points refunded (via cancel_redemption RPC)
--
-- Tables:
--   rewards     — reward catalogue items per hotel
--   redemptions — employee redemption records
--
-- RPCs (SECURITY DEFINER, run under postgres):
--   redeem_reward(p_employee_id, p_reward_id)      → {ok, redemption_id, points_spent, new_balance}
--   cancel_redemption(p_redemption_id, p_employee_id) → {ok, points_refunded}
-- ─────────────────────────────────────────────────────────────────────────────


-- ── 1. Points balance on employees ───────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS points_balance integer NOT NULL DEFAULT 0
    CONSTRAINT chk_points_balance CHECK (points_balance >= 0);

COMMENT ON COLUMN public.employees.points_balance IS
  'Running total of reward-eligible points. '
  'Incremented +10 per recognition received; decremented on redemption.';


-- ── 2. Drop legacy reward tables ─────────────────────────────────────────────

DROP TABLE IF EXISTS public.redemptions     CASCADE;
DROP TABLE IF EXISTS public.rewards         CASCADE;
DROP TABLE IF EXISTS public.reward_categories CASCADE;


-- ── 3. rewards ────────────────────────────────────────────────────────────────

CREATE TABLE public.rewards (
  id              uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  title           text        NOT NULL,
  description     text,
  points_required integer     NOT NULL CHECK (points_required > 0),
  image_url       text,
  hotel           text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  stock           integer     NOT NULL DEFAULT 0 CHECK (stock >= 0),
  created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_rewards_hotel ON public.rewards (hotel);

COMMENT ON TABLE public.rewards IS
  'Reward catalogue items per hotel.  Employees redeem using points earned from recognitions.';


-- ── 4. redemptions ────────────────────────────────────────────────────────────

CREATE TABLE public.redemptions (
  id           uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id  uuid        NOT NULL REFERENCES public.employees(id) ON DELETE CASCADE,
  reward_id    uuid        NOT NULL REFERENCES public.rewards(id),
  points_spent integer     NOT NULL CHECK (points_spent > 0),
  hotel        text        NOT NULL CHECK (public.is_valid_hotel(hotel)),
  status       text        NOT NULL DEFAULT 'pending'
                           CHECK (status IN ('pending', 'approved', 'fulfilled', 'cancelled')),
  created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_redemptions_employee
  ON public.redemptions (employee_id, created_at DESC);

COMMENT ON TABLE public.redemptions IS
  'Records of reward redemptions.  Points are deducted atomically by redeem_reward().';


-- ── 5. Enable RLS ─────────────────────────────────────────────────────────────

ALTER TABLE public.rewards     ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.redemptions ENABLE ROW LEVEL SECURITY;

-- Employees can browse rewards in their own hotel
CREATE POLICY "rewards_hotel_select"
  ON public.rewards
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());

-- Employees can read their own redemptions (hotel-scoped)
CREATE POLICY "redemptions_hotel_select"
  ON public.redemptions
  FOR SELECT TO anon, authenticated
  USING (hotel = public.current_employee_hotel());


-- ── 6. Trigger: +10 points on recognition received ───────────────────────────

CREATE OR REPLACE FUNCTION public.award_recognition_points()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  UPDATE public.employees
  SET    points_balance = points_balance + 10
  WHERE  id = NEW.receiver_id;
  RETURN NEW;
END;
$$;

CREATE TRIGGER trg_recognition_points
  AFTER INSERT ON public.recognitions
  FOR EACH ROW EXECUTE FUNCTION public.award_recognition_points();

COMMENT ON FUNCTION public.award_recognition_points IS
  'Awards 10 points to the recognition receiver.  Fires after every INSERT on recognitions.';


-- ── 7. redeem_reward RPC ──────────────────────────────────────────────────────
-- Atomic: checks stock + balance, deducts points, decrements stock, inserts redemption.
-- Uses SELECT ... FOR UPDATE to prevent race conditions.

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
  -- Lock reward row (prevents concurrent stock depletion)
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
      'error',    'Not enough points.',
      'balance',  v_current_balance,
      'required', v_points_required
    );
  END IF;

  -- Deduct points
  UPDATE public.employees
  SET    points_balance = points_balance - v_points_required
  WHERE  id = p_employee_id;

  -- Decrement stock
  UPDATE public.rewards
  SET    stock = stock - 1
  WHERE  id = p_reward_id;

  -- Record redemption
  INSERT INTO public.redemptions (employee_id, reward_id, points_spent, hotel)
  VALUES (p_employee_id, p_reward_id, v_points_required, v_hotel)
  RETURNING id INTO v_redemption_id;

  RETURN jsonb_build_object(
    'ok',            true,
    'redemption_id', v_redemption_id,
    'points_spent',  v_points_required,
    'new_balance',   v_current_balance - v_points_required
  );
END;
$func$;

GRANT EXECUTE ON FUNCTION public.redeem_reward(uuid, uuid) TO anon;
GRANT EXECUTE ON FUNCTION public.redeem_reward(uuid, uuid) TO authenticated;

COMMENT ON FUNCTION public.redeem_reward IS
  'Atomic reward redemption.  Validates stock and balance, deducts points, '
  'decrements stock, and inserts a redemption record.  Safe under concurrent load.';


-- ── 8. cancel_redemption RPC ──────────────────────────────────────────────────
-- Only pending redemptions can be cancelled.
-- Refunds points and restores stock.

CREATE OR REPLACE FUNCTION public.cancel_redemption(
  p_redemption_id uuid,
  p_employee_id   uuid
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $func$
DECLARE
  v_points_spent integer;
  v_reward_id    uuid;
  v_status       text;
BEGIN
  SELECT points_spent, reward_id, status
  INTO   v_points_spent, v_reward_id, v_status
  FROM   public.redemptions
  WHERE  id           = p_redemption_id
    AND  employee_id  = p_employee_id
  FOR UPDATE;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Redemption not found.');
  END IF;

  IF v_status <> 'pending' THEN
    RETURN jsonb_build_object(
      'ok',    false,
      'error', 'Only pending redemptions can be cancelled.'
    );
  END IF;

  -- Refund points
  UPDATE public.employees
  SET    points_balance = points_balance + v_points_spent
  WHERE  id = p_employee_id;

  -- Restore stock
  UPDATE public.rewards
  SET    stock = stock + 1
  WHERE  id = v_reward_id;

  -- Mark cancelled
  UPDATE public.redemptions
  SET    status = 'cancelled'
  WHERE  id = p_redemption_id;

  RETURN jsonb_build_object('ok', true, 'points_refunded', v_points_spent);
END;
$func$;

GRANT EXECUTE ON FUNCTION public.cancel_redemption(uuid, uuid) TO anon;
GRANT EXECUTE ON FUNCTION public.cancel_redemption(uuid, uuid) TO authenticated;

COMMENT ON FUNCTION public.cancel_redemption IS
  'Cancels a pending redemption, refunds points, and restores reward stock.';
