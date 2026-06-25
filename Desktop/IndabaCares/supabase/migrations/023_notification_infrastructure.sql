-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 023 — Notification Infrastructure
--
-- Reconciles the legacy notifications table (migration 005) to the new schema:
--
--   id          uuid        PRIMARY KEY
--   employee_id uuid        → employees(id)   (was: user_id → profiles)
--   title       text
--   message     text                          (was: body)
--   type        text        CHECK 4 types     (was: notification_type enum)
--   read        boolean     DEFAULT false     (was: is_read)
--   hotel       text                          (added in 017)
--   created_at  timestamptz DEFAULT now()
--
-- Notification types
--   recognition_received  — auto via trigger on recognitions INSERT
--   reward_approved       — auto via trigger on redemptions UPDATE (→ approved)
--   reward_rejected       — auto via trigger on redemptions UPDATE (→ rejected)
--   admin_announcement    — manual via notify_all(hotel, title, message) RPC
-- ─────────────────────────────────────────────────────────────────────────────


-- ─── 1. Schema reconciliation (all idempotent) ────────────────────────────────

-- Add employee_id column (new FK pointing to employees, not profiles)
ALTER TABLE public.notifications
  ADD COLUMN IF NOT EXISTS employee_id uuid REFERENCES public.employees(id) ON DELETE CASCADE;

-- Add message column (replacement for body)
ALTER TABLE public.notifications
  ADD COLUMN IF NOT EXISTS message text;

-- Add read column (replacement for is_read)
ALTER TABLE public.notifications
  ADD COLUMN IF NOT EXISTS read boolean NOT NULL DEFAULT false;

-- Back-fill read from is_read where is_read column exists
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE  table_schema = 'public'
      AND  table_name   = 'notifications'
      AND  column_name  = 'is_read'
  ) THEN
    UPDATE public.notifications SET read = is_read WHERE read = false;
  END IF;
END
$$;

-- Back-fill message from body where body column exists
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE  table_schema = 'public'
      AND  table_name   = 'notifications'
      AND  column_name  = 'body'
  ) THEN
    UPDATE public.notifications SET message = body WHERE message IS NULL AND body IS NOT NULL;
  END IF;
END
$$;

-- Drop the old partial index that references is_read (prevents column rename)
DROP INDEX IF EXISTS idx_notifications_user_unread;

-- Drop old check/constraint on type if any
ALTER TABLE public.notifications
  DROP CONSTRAINT IF EXISTS notifications_type_check;
ALTER TABLE public.notifications
  DROP CONSTRAINT IF EXISTS chk_notification_type;

-- Cast type column to plain text if it was an enum
-- (safe no-op if already text)
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE  table_schema = 'public'
      AND  table_name   = 'notifications'
      AND  column_name  = 'type'
      AND  udt_name    != 'text'
  ) THEN
    ALTER TABLE public.notifications
      ALTER COLUMN type TYPE text USING type::text;
  END IF;
END
$$;

-- Add the 4-type check constraint
ALTER TABLE public.notifications
  DROP CONSTRAINT IF EXISTS notifications_type_check;

ALTER TABLE public.notifications
  ADD CONSTRAINT notifications_type_check
  CHECK (type IN (
    'recognition_received',
    'reward_approved',
    'reward_rejected',
    'admin_announcement'
  ));


-- ─── 2. Indexes ───────────────────────────────────────────────────────────────

-- Primary inbox query: all notifications for an employee, newest-first
CREATE INDEX IF NOT EXISTS idx_notifications_employee_time
  ON public.notifications (employee_id, created_at DESC);

-- Fast unread count
CREATE INDEX IF NOT EXISTS idx_notifications_employee_unread
  ON public.notifications (employee_id)
  WHERE read = false;

-- Hotel-scoped (used by admin_announcement)
CREATE INDEX IF NOT EXISTS idx_notifications_hotel
  ON public.notifications (hotel, created_at DESC);


-- ─── 3. Trigger — recognition received ───────────────────────────────────────
--
-- Fires after every INSERT on recognitions.
-- Creates a 'recognition_received' notification for the receiver.

CREATE OR REPLACE FUNCTION public.notify_recognition_received()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_sender_name text;
BEGIN
  -- Resolve sender full name for the notification title
  SELECT full_name INTO v_sender_name
  FROM   public.employees
  WHERE  id = NEW.sender_id;

  INSERT INTO public.notifications (
    employee_id,
    title,
    message,
    type,
    hotel,
    read
  ) VALUES (
    NEW.receiver_id,
    '🌟 You received a recognition!',
    COALESCE(v_sender_name, 'A colleague') || ' recognised you' ||
      CASE WHEN NEW.message IS NOT NULL AND NEW.message <> ''
           THEN ': "' || LEFT(NEW.message, 120) || '"'
           ELSE '.'
      END,
    'recognition_received',
    NEW.hotel,
    false
  );

  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_notify_recognition ON public.recognitions;
CREATE TRIGGER trg_notify_recognition
  AFTER INSERT ON public.recognitions
  FOR EACH ROW
  EXECUTE FUNCTION public.notify_recognition_received();


-- ─── 4. Trigger — redemption status changes ───────────────────────────────────
--
-- Fires after every UPDATE on redemptions.
-- Creates approved/rejected notifications when status transitions.

CREATE OR REPLACE FUNCTION public.notify_redemption_status()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_reward_title text;
  v_title        text;
  v_message      text;
  v_type         text;
BEGIN
  -- Only act on status changes
  IF OLD.status = NEW.status THEN
    RETURN NEW;
  END IF;

  IF NEW.status NOT IN ('approved', 'rejected') THEN
    RETURN NEW;
  END IF;

  -- Resolve reward title
  SELECT title INTO v_reward_title
  FROM   public.rewards
  WHERE  id = NEW.reward_id;

  IF NEW.status = 'approved' THEN
    v_type    := 'reward_approved';
    v_title   := '✅ Reward approved!';
    v_message := 'Your redemption for "' || COALESCE(v_reward_title, 'a reward') ||
                 '" has been approved. It will be fulfilled shortly.';
  ELSE
    v_type    := 'reward_rejected';
    v_title   := '❌ Reward redemption declined';
    v_message := 'Your redemption for "' || COALESCE(v_reward_title, 'a reward') ||
                 '" was declined.' ||
                 CASE WHEN NEW.rejection_reason IS NOT NULL AND NEW.rejection_reason <> ''
                      THEN ' Reason: ' || NEW.rejection_reason
                      ELSE ''
                 END;
  END IF;

  INSERT INTO public.notifications (
    employee_id,
    title,
    message,
    type,
    hotel,
    read
  ) VALUES (
    NEW.employee_id,
    v_title,
    v_message,
    v_type,
    NEW.hotel,
    false
  );

  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_notify_redemption ON public.redemptions;
CREATE TRIGGER trg_notify_redemption
  AFTER UPDATE ON public.redemptions
  FOR EACH ROW
  EXECUTE FUNCTION public.notify_redemption_status();


-- ─── 5. Admin announcement RPC ────────────────────────────────────────────────
--
-- Inserts one notification row per active employee in the hotel.
-- Caller must use the service_role key (no anon/authenticated grant).

CREATE OR REPLACE FUNCTION public.notify_all(
  p_hotel   text,
  p_title   text,
  p_message text
)
RETURNS integer          -- number of notifications inserted
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_count integer;
BEGIN
  INSERT INTO public.notifications (employee_id, title, message, type, hotel, read)
  SELECT
    e.id,
    p_title,
    p_message,
    'admin_announcement',
    p_hotel,
    false
  FROM public.employees e
  WHERE e.hotel  = p_hotel
    AND e.status = 'active';

  GET DIAGNOSTICS v_count = ROW_COUNT;
  RETURN v_count;
END;
$$;

-- Do NOT grant to anon/authenticated — admin only via service_role key.
COMMENT ON FUNCTION public.notify_all IS
  'Broadcasts an admin announcement to every active employee in a hotel.
   Call with the service_role key only — not exposed to the app client.';


-- ─── 6. Mark-read helpers (RPC, avoids exposing UPDATE policy to all columns) ─

CREATE OR REPLACE FUNCTION public.mark_notification_read(p_id uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  UPDATE public.notifications
  SET    read = true
  WHERE  id   = p_id
    AND  employee_id = (
      SELECT employee_id
      FROM   public.employee_active_sessions
      WHERE  token      = (current_setting('request.headers', true)::json->>'x-session-token')::uuid
        AND  expires_at > now()
      LIMIT  1
    );
END;
$$;

GRANT EXECUTE ON FUNCTION public.mark_notification_read(uuid) TO anon, authenticated;

CREATE OR REPLACE FUNCTION public.mark_all_notifications_read(p_employee_id uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
  UPDATE public.notifications
  SET    read = true
  WHERE  employee_id = p_employee_id
    AND  read        = false;
END;
$$;

GRANT EXECUTE ON FUNCTION public.mark_all_notifications_read(uuid) TO anon, authenticated;


-- ─── 7. RLS — ensure hotel + employee isolation ───────────────────────────────

-- Drop any stale policies first
DROP POLICY IF EXISTS "notifications_hotel_select" ON public.notifications;
DROP POLICY IF EXISTS "notifications_hotel_update" ON public.notifications;
DROP POLICY IF EXISTS "notifications_select_own"   ON public.notifications;
DROP POLICY IF EXISTS "notifications_update_own"   ON public.notifications;

-- Employees may only read their own notifications within their hotel
CREATE POLICY "notifications_own_select"
  ON public.notifications
  FOR SELECT
  TO anon, authenticated
  USING (
    hotel       = public.current_employee_hotel()
    AND employee_id = (
      SELECT employee_id
      FROM   public.employee_active_sessions
      WHERE  token      = (current_setting('request.headers', true)::json->>'x-session-token')::uuid
        AND  expires_at > now()
      LIMIT  1
    )
  );

-- No direct UPDATE policy — use mark_notification_read() RPC instead.


-- ─── 8. Comments ─────────────────────────────────────────────────────────────

COMMENT ON TABLE public.notifications IS
  'Employee notification inbox. Auto-populated by triggers on recognitions and
   redemptions. Admin announcements created via notify_all() RPC.
   Columns: employee_id, title, message, type, read, hotel, created_at.';
