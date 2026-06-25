-- =============================================================================
-- Migration 078 — Points System Full Overhaul
--
-- Covers:
--   1. recognitions.badge CHECK — add all skill badges + 'You Legend' + 'Legend of the Month'
--   2. recognitions.card_type column — differentiates 'recognition' vs 'skills'
--   3. points_ledger.source CHECK — add all 9 new event sources
--   4. award_recognition_points() — card_type-aware sources
--   5. award_reaction_points() — all reaction types = 1 pt (was 50/20/10)
--   6. employee_reaction_allocations — add total_remaining (unified 100/month pool)
--   7. enforce_reaction_allocation() — enforce total_remaining, not per-type
--   8. submit_mood() — add allow_points_update GUC (was missing, causing guard failure)
--   9. submit_recognition_response() — fix balance update + card_type-aware source
--  10. award_celebration_points() trigger — birthday/anniversary = 100 pts each
--  11. employees.highest_tier_reached + tier_from_balance() + check_status_unlock()
-- =============================================================================


-- ─────────────────────────────────────────────────────────────────────────────
-- 1. recognitions.badge CHECK — expand to include skill badges and new badges
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.recognitions
  DROP CONSTRAINT IF EXISTS chk_recognition_badge;

ALTER TABLE public.recognitions
  ADD CONSTRAINT chk_recognition_badge CHECK (badge IN (
    -- Original recognition badges
    'Team Player',
    'Leadership',
    'Customer Excellence',
    'Innovation',              -- kept for existing data
    'Going the Extra Mile',
    'Hospitality Hero',
    'You Legend',              -- was in frontend, now in DB
    'Legend of the Month',     -- awarded by legend function
    -- Skill badges
    'Teamwork',
    'Communication',
    'Problem Solving',
    'Customer Service',
    'Creativity',
    'Reliability',
    'Positivity'
  ));


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. recognitions.card_type — 'recognition' | 'skills'
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.recognitions
  ADD COLUMN IF NOT EXISTS card_type text NOT NULL DEFAULT 'recognition'
  CHECK (card_type IN ('recognition', 'skills'));

-- Backfill: skill-exclusive badges → 'skills'; ambiguous 'Leadership' stays 'recognition'
UPDATE public.recognitions
SET card_type = 'skills'
WHERE badge IN (
  'Teamwork', 'Communication', 'Problem Solving',
  'Customer Service', 'Creativity', 'Reliability', 'Positivity'
);

CREATE INDEX IF NOT EXISTS idx_recognitions_card_type
  ON public.recognitions (card_type, created_at DESC);


-- ─────────────────────────────────────────────────────────────────────────────
-- 3. points_ledger.source CHECK — expand to include all event sources
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.points_ledger
  DROP CONSTRAINT IF EXISTS points_ledger_source_check;

ALTER TABLE public.points_ledger
  ADD CONSTRAINT points_ledger_source_check
  CHECK (source IN (
    'recognition_received',    -- receiving a recognition card (10 pts)
    'skills_received',         -- receiving a skills card (10 pts)
    'recognition_response',    -- responding to a recognition card (5 pts)
    'skills_response',         -- responding to a skills card (5 pts)
    'reaction_received',       -- receiving any emoji reaction (1 pt)
    'mood_checkin',            -- daily mood board update (5 pts)
    'birthday',                -- birthday celebration (100 pts)
    'anniversary',             -- service milestone / anniversary (100 pts)
    'status_unlock',           -- unlocking a new status tier (50 pts)
    'badge_achieved',          -- earning a badge (10 pts)
    'legend_of_month',         -- legend of the month award (250 pts)
    'admin_bonus',             -- one-off admin grant
    'campaign_reward'          -- campaign / challenge reward
  ));


-- ─────────────────────────────────────────────────────────────────────────────
-- 4. award_recognition_points() — card_type-aware sources
-- ─────────────────────────────────────────────────────────────────────────────
--
-- Source is now determined by card_type:
--   'recognition' → 'recognition_received'
--   'skills'      → 'skills_received'

CREATE OR REPLACE FUNCTION public.award_recognition_points()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_base_points  CONSTANT integer := 10;
  v_source       text;
  v_multiplier   integer;
  v_bonus_points integer;
  v_campaign_id  uuid;
BEGIN
  v_source := CASE COALESCE(NEW.card_type, 'recognition')
    WHEN 'skills' THEN 'skills_received'
    ELSE 'recognition_received'
  END;

  v_multiplier := public.active_campaign_multiplier(NEW.hotel);

  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = points_balance + v_base_points
  WHERE  id = NEW.receiver_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (NEW.receiver_id, v_base_points, v_source, NEW.hotel);

  IF v_multiplier > 1 THEN
    v_bonus_points := v_base_points * (v_multiplier - 1);
    v_campaign_id  := public.active_campaign_id(NEW.hotel);

    UPDATE public.employees
    SET    points_balance = points_balance + v_bonus_points
    WHERE  id = NEW.receiver_id;

    INSERT INTO public.points_ledger
      (employee_id, points, source, hotel, campaign_id)
    VALUES
      (NEW.receiver_id, v_bonus_points, 'campaign_reward', NEW.hotel, v_campaign_id);
  END IF;

  RETURN NEW;
END;
$$;

COMMENT ON FUNCTION public.award_recognition_points IS
  'Awards 10 points per recognition/skills card received. '
  'Source is recognition_received or skills_received based on card_type. '
  'Includes active campaign multiplier if applicable.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 5. award_reaction_points() — all reaction types = 1 pt
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.award_reaction_points()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_receiver_id uuid;
  v_delta       integer;
BEGIN
  SELECT receiver_id INTO v_receiver_id
  FROM   public.recognitions
  WHERE  id = (COALESCE(NEW, OLD)).recognition_id;

  IF v_receiver_id IS NULL THEN
    RETURN COALESCE(NEW, OLD);
  END IF;

  -- All reaction types award 1 point
  v_delta := CASE TG_OP
    WHEN 'INSERT' THEN  1
    WHEN 'DELETE' THEN -1
  END;

  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = GREATEST(0, points_balance + v_delta)
  WHERE  id = v_receiver_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (
    v_receiver_id,
    v_delta,
    'reaction_received',
    (COALESCE(NEW, OLD)).hotel
  );

  RETURN COALESCE(NEW, OLD);
END;
$$;

COMMENT ON FUNCTION public.award_reaction_points() IS
  'Awards or reverses 1 point on the recognition receiver for any reaction type. '
  'Was: heart=50, smile=20, thumbs_up=10. Now: all types = 1 pt.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 6. employee_reaction_allocations — unified 100/month pool
-- ─────────────────────────────────────────────────────────────────────────────

-- Add total_remaining column
ALTER TABLE public.employee_reaction_allocations
  ADD COLUMN IF NOT EXISTS total_remaining integer NOT NULL DEFAULT 100
  CHECK (total_remaining >= 0);

-- Update column defaults to proportional split of 100
ALTER TABLE public.employee_reaction_allocations
  ALTER COLUMN hearts_remaining SET DEFAULT 34,
  ALTER COLUMN smiles_remaining SET DEFAULT 33,
  ALTER COLUMN thumbs_remaining SET DEFAULT 33;

-- Reset existing rows for the current month to new values
UPDATE public.employee_reaction_allocations
SET
  hearts_remaining = 34,
  smiles_remaining = 33,
  thumbs_remaining = 33,
  total_remaining  = 100
WHERE year  = EXTRACT(YEAR  FROM now())::integer
  AND month = EXTRACT(MONTH FROM now())::integer;

COMMENT ON COLUMN public.employee_reaction_allocations.total_remaining IS
  'Unified monthly emoji budget: 100 per employee per month (any mix of heart/smile/thumbs_up). '
  'This is the enforced limit. Per-type columns track usage for display purposes.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 7. enforce_reaction_allocation() — unified pool enforcement
-- ─────────────────────────────────────────────────────────────────────────────
--
-- BEFORE INSERT: upsert allocation row → check total_remaining → decrement total + type.
-- BEFORE DELETE: restore total_remaining (capped 100) + specific type column.

CREATE OR REPLACE FUNCTION public.enforce_reaction_allocation()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_month          integer := EXTRACT(MONTH FROM now())::integer;
  v_year           integer := EXTRACT(YEAR  FROM now())::integer;
  DEFAULT_TOTAL    CONSTANT integer := 100;
  DEFAULT_HEARTS   CONSTANT integer := 34;
  DEFAULT_SMILES   CONSTANT integer := 33;
  DEFAULT_THUMBS   CONSTANT integer := 33;
  v_total_remaining integer;
BEGIN

  IF TG_OP = 'INSERT' THEN

    -- Ensure allocation row exists for this employee/month
    INSERT INTO public.employee_reaction_allocations (employee_id, month, year)
    VALUES (NEW.employee_id, v_month, v_year)
    ON CONFLICT (employee_id, month, year) DO NOTHING;

    -- Check unified total
    SELECT total_remaining
    INTO   v_total_remaining
    FROM   public.employee_reaction_allocations
    WHERE  employee_id = NEW.employee_id
      AND  month       = v_month
      AND  year        = v_year;

    IF v_total_remaining IS NULL OR v_total_remaining <= 0 THEN
      RAISE EXCEPTION 'Monthly emoji allocation exhausted (100 per month).'
        USING ERRCODE = 'P0006';
    END IF;

    -- Decrement total_remaining and the specific type column (floored at 0)
    UPDATE public.employee_reaction_allocations
    SET
      total_remaining  = total_remaining - 1,
      hearts_remaining = CASE WHEN NEW.reaction_type = 'heart'     THEN GREATEST(0, hearts_remaining - 1) ELSE hearts_remaining END,
      smiles_remaining = CASE WHEN NEW.reaction_type = 'smile'     THEN GREATEST(0, smiles_remaining - 1) ELSE smiles_remaining END,
      thumbs_remaining = CASE WHEN NEW.reaction_type = 'thumbs_up' THEN GREATEST(0, thumbs_remaining - 1) ELSE thumbs_remaining END
    WHERE employee_id = NEW.employee_id
      AND month       = v_month
      AND year        = v_year;

    RETURN NEW;
  END IF;

  IF TG_OP = 'DELETE' THEN

    UPDATE public.employee_reaction_allocations
    SET
      total_remaining  = LEAST(total_remaining  + 1, DEFAULT_TOTAL),
      hearts_remaining = CASE WHEN OLD.reaction_type = 'heart'     THEN LEAST(hearts_remaining + 1, DEFAULT_HEARTS) ELSE hearts_remaining END,
      smiles_remaining = CASE WHEN OLD.reaction_type = 'smile'     THEN LEAST(smiles_remaining + 1, DEFAULT_SMILES) ELSE smiles_remaining END,
      thumbs_remaining = CASE WHEN OLD.reaction_type = 'thumbs_up' THEN LEAST(thumbs_remaining + 1, DEFAULT_THUMBS) ELSE thumbs_remaining END
    WHERE employee_id = OLD.employee_id
      AND month       = v_month
      AND year        = v_year;

    RETURN OLD;
  END IF;

  RETURN COALESCE(NEW, OLD);
END;
$$;

COMMENT ON FUNCTION public.enforce_reaction_allocation() IS
  'BEFORE INSERT: upserts allocation row, checks unified total_remaining (100/month), decrements.
   BEFORE DELETE: restores total_remaining (cap 100) and specific type counter.
   Enforcement is on total_remaining only — type columns are display tracking.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 8. submit_mood() — add allow_points_update GUC (was missing, blocked by guard)
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.submit_mood(
  p_employee_id uuid,
  p_hotel       text,
  p_mood        text,
  p_note        text DEFAULT NULL
)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_entry_id uuid;
  v_points   CONSTANT integer := 5;
BEGIN
  -- Validate employee belongs to stated hotel
  IF NOT EXISTS (
    SELECT 1 FROM public.employees
    WHERE id = p_employee_id
      AND hotel = p_hotel
      AND status = 'active'
  ) THEN
    RAISE EXCEPTION 'Employee not found'
      USING ERRCODE = 'P0002';
  END IF;

  -- Once per day (UNIQUE constraint is belt-and-suspenders)
  IF EXISTS (
    SELECT 1 FROM public.mood_entries
    WHERE employee_id = p_employee_id
      AND entry_date  = current_date
  ) THEN
    RAISE EXCEPTION 'Mood already submitted today'
      USING ERRCODE = 'P3001';
  END IF;

  -- Insert mood entry
  INSERT INTO public.mood_entries (employee_id, mood, note, entry_date)
  VALUES (p_employee_id, p_mood::public.mood_value, p_note, current_date)
  RETURNING id INTO v_entry_id;

  -- Award 5 points (GUC required to bypass guard trigger)
  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = points_balance + v_points
  WHERE  id = p_employee_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (p_employee_id, v_points, 'mood_checkin', p_hotel);

  RETURN v_entry_id;
END;
$$;

GRANT EXECUTE ON FUNCTION public.submit_mood(uuid, text, text, text) TO authenticated, anon;

COMMENT ON FUNCTION public.submit_mood(uuid, text, text, text) IS
  'Employee daily mood check-in. Once per day; awards 5 points to employees.points_balance '
  'and appends an audit row to points_ledger (source = mood_checkin).';


-- ─────────────────────────────────────────────────────────────────────────────
-- 9. submit_recognition_response() — fix balance update + card_type-aware source
-- ─────────────────────────────────────────────────────────────────────────────

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
  v_rec    public.recognitions%ROWTYPE;
  v_source text;
  v_points CONSTANT integer := 5;
BEGIN
  SELECT * INTO v_rec
  FROM   public.recognitions
  WHERE  id = p_recognition_id
  LIMIT  1;

  IF NOT FOUND THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Recognition not found.');
  END IF;

  IF v_rec.receiver_id <> p_employee_id THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Only the recipient may respond.');
  END IF;

  IF v_rec.recipient_response IS NOT NULL THEN
    RETURN jsonb_build_object('ok', false, 'error', 'Already responded.');
  END IF;

  -- Determine source from card_type
  v_source := CASE COALESCE(v_rec.card_type, 'recognition')
    WHEN 'skills' THEN 'skills_response'
    ELSE 'recognition_response'
  END;

  UPDATE public.recognitions
  SET    recipient_response     = TRIM(p_response),
         recipient_responded_at = now()
  WHERE  id = p_recognition_id;

  -- Award 5 pts (update balance + ledger)
  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = points_balance + v_points
  WHERE  id = p_employee_id;

  INSERT INTO public.points_ledger (employee_id, hotel, points, source)
  VALUES (p_employee_id, v_rec.hotel, v_points, v_source);

  RETURN jsonb_build_object('ok', true);

EXCEPTION WHEN others THEN
  RETURN jsonb_build_object('ok', false, 'error', SQLERRM);
END;
$$;

GRANT EXECUTE ON FUNCTION public.submit_recognition_response(uuid, uuid, text)
  TO anon, authenticated;

COMMENT ON FUNCTION public.submit_recognition_response IS
  'Lets the recognition/skills recipient post a one-time response.
   Awards 5 pts (updates balance + ledger). Source is recognition_response or skills_response.';


-- ─────────────────────────────────────────────────────────────────────────────
-- 10. award_celebration_points() — birthday and anniversary = 100 pts each
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.award_celebration_points()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_points CONSTANT integer := 100;
  v_source text;
BEGIN
  v_source := CASE NEW.type
    WHEN 'birthday'    THEN 'birthday'
    WHEN 'anniversary' THEN 'anniversary'
    ELSE NULL
  END;

  IF v_source IS NULL THEN
    RETURN NEW;
  END IF;

  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  UPDATE public.employees
  SET    points_balance = points_balance + v_points
  WHERE  id = NEW.employee_id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (NEW.employee_id, v_points, v_source, NEW.hotel);

  RETURN NEW;
END;
$$;

COMMENT ON FUNCTION public.award_celebration_points() IS
  'AFTER INSERT on celebrations: awards 100 pts for birthday or anniversary. '
  'Fires once per celebration row (daily cron ensures one row per event per day).';

CREATE TRIGGER trg_award_celebration_points
  AFTER INSERT ON public.celebrations
  FOR EACH ROW EXECUTE FUNCTION public.award_celebration_points();


-- ─────────────────────────────────────────────────────────────────────────────
-- 11. Status unlock — tier tracking + 50 pts on new tier achievement
-- ─────────────────────────────────────────────────────────────────────────────

-- 11a. Add highest_tier_reached to employees
ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS highest_tier_reached text NOT NULL DEFAULT 'newcomer'
  CHECK (highest_tier_reached IN ('newcomer', 'bronze', 'silver', 'gold'));

-- 11b. Backfill from current points_balance
UPDATE public.employees
SET highest_tier_reached = CASE
  WHEN points_balance >= 2000 THEN 'gold'
  WHEN points_balance >= 500  THEN 'silver'
  WHEN points_balance >= 100  THEN 'bronze'
  ELSE 'newcomer'
END;

-- 11c. tier_from_balance() helper
CREATE OR REPLACE FUNCTION public.tier_from_balance(p_balance integer)
RETURNS text
LANGUAGE sql
IMMUTABLE
SET search_path = public
AS $$
  SELECT CASE
    WHEN p_balance >= 2000 THEN 'gold'
    WHEN p_balance >= 500  THEN 'silver'
    WHEN p_balance >= 100  THEN 'bronze'
    ELSE 'newcomer'
  END;
$$;

COMMENT ON FUNCTION public.tier_from_balance(integer) IS
  'Returns the status tier name for a given points balance. '
  'Thresholds: bronze=100, silver=500, gold=2000.';

-- 11d. check_status_unlock() trigger function
CREATE OR REPLACE FUNCTION public.check_status_unlock()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_new_tier    text;
  v_new_rank    integer;
  v_current_rank integer;
  v_unlock_pts  CONSTANT integer := 50;
BEGIN
  -- Prevent re-entry (avoid infinite loop when we UPDATE points_balance + 50)
  IF current_setting('indabacares.skip_status_unlock', true) = 'true' THEN
    RETURN NEW;
  END IF;

  v_new_tier := public.tier_from_balance(NEW.points_balance);

  -- No award for newcomer tier
  IF v_new_tier = 'newcomer' THEN
    RETURN NEW;
  END IF;

  -- Convert tiers to ordinal ranks for comparison
  v_new_rank := CASE v_new_tier
    WHEN 'gold'   THEN 3
    WHEN 'silver' THEN 2
    WHEN 'bronze' THEN 1
    ELSE 0
  END;

  v_current_rank := CASE NEW.highest_tier_reached
    WHEN 'gold'   THEN 3
    WHEN 'silver' THEN 2
    WHEN 'bronze' THEN 1
    ELSE 0
  END;

  -- Only award when crossing INTO a higher tier (not just staying in it)
  IF v_new_rank <= v_current_rank THEN
    RETURN NEW;
  END IF;

  -- Mark re-entry guard BEFORE the recursive update
  PERFORM set_config('indabacares.skip_status_unlock', 'true', true);
  PERFORM set_config('indabacares.allow_points_update', 'true', true);

  -- Update tier + award bonus in a single statement
  UPDATE public.employees
  SET    highest_tier_reached = v_new_tier,
         points_balance       = points_balance + v_unlock_pts
  WHERE  id = NEW.id;

  INSERT INTO public.points_ledger (employee_id, points, source, hotel)
  VALUES (NEW.id, v_unlock_pts, 'status_unlock', NEW.hotel);

  RETURN NEW;
END;
$$;

COMMENT ON FUNCTION public.check_status_unlock() IS
  'AFTER UPDATE OF points_balance on employees: awards 50 pts when an employee '
  'crosses into a new (higher) tier. Tiers: bronze=100pts, silver=500pts, gold=2000pts. '
  'Uses indabacares.skip_status_unlock GUC to prevent infinite recursion.';

CREATE TRIGGER trg_check_status_unlock
  AFTER UPDATE OF points_balance ON public.employees
  FOR EACH ROW EXECUTE FUNCTION public.check_status_unlock();
