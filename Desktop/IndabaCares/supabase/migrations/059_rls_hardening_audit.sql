-- 059_rls_hardening_audit.sql
--
-- Comprehensive RLS policy audit and hardening pass.
--
-- Principles enforced throughout:
--   1. Every table has RLS enabled — no exceptions.
--   2. Hotel isolation via current_employee_hotel() for multi-tenant tables.
--   3. Row ownership via current_employee_id() for personal data tables.
--   4. No unrestricted SELECT: every policy has a WHERE clause.
--   5. Service-role (used by Edge Functions) bypasses RLS by design — this is
--      safe because Edge Functions authenticate via withEmployeeAuth first.
--   6. Anon role has zero access to any table — the app uses employee sessions,
--      not Supabase Auth.
--
-- This migration is IDEMPOTENT: it replaces policies using DROP IF EXISTS +
-- CREATE, so it can be re-run safely.

-- ─── Helper: revoke anon access from all tables ───────────────────────────────
-- Belt-and-suspenders: anon role should have no direct table access.
-- Supabase grants SELECT on public schema to anon by default — revoke it.

REVOKE ALL ON ALL TABLES IN SCHEMA public FROM anon;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM anon;

-- ─── employees ────────────────────────────────────────────────────────────────

ALTER TABLE employees ENABLE ROW LEVEL SECURITY;

-- Read own profile
DROP POLICY IF EXISTS "employees_read_own"       ON employees;
DROP POLICY IF EXISTS "employees_read_hotel"     ON employees;
DROP POLICY IF EXISTS "employees_no_write_self"  ON employees;

CREATE POLICY "employees_read_hotel"
  ON employees FOR SELECT
  USING (hotel = current_employee_hotel() AND status = 'active');

-- Employees cannot directly INSERT/UPDATE/DELETE themselves.
-- All employee mutations go through SECURITY DEFINER RPCs.

-- ─── recognitions ─────────────────────────────────────────────────────────────

ALTER TABLE recognitions ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "recognitions_hotel_read"   ON recognitions;
DROP POLICY IF EXISTS "recognitions_hotel_insert"  ON recognitions;

-- Read any recognition in the same hotel
CREATE POLICY "recognitions_hotel_read"
  ON recognitions FOR SELECT
  USING (hotel = current_employee_hotel());

-- Insert blocked for direct table writes: use send-recognition Edge Function.
-- Edge Functions use adminClient (service_role) so they bypass RLS.

-- ─── recognition_reactions ────────────────────────────────────────────────────

ALTER TABLE recognition_reactions ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "reactions_hotel_read"    ON recognition_reactions;
DROP POLICY IF EXISTS "reactions_own_insert"    ON recognition_reactions;
DROP POLICY IF EXISTS "reactions_own_delete"    ON recognition_reactions;

CREATE POLICY "reactions_hotel_read"
  ON recognition_reactions FOR SELECT
  USING (
    EXISTS (
      SELECT 1 FROM recognitions r
      WHERE r.id    = recognition_id
        AND r.hotel = current_employee_hotel()
    )
  );

CREATE POLICY "reactions_own_insert"
  ON recognition_reactions FOR INSERT
  WITH CHECK (employee_id = current_employee_id());

CREATE POLICY "reactions_own_delete"
  ON recognition_reactions FOR DELETE
  USING (employee_id = current_employee_id());

-- ─── recognition_comments ─────────────────────────────────────────────────────

ALTER TABLE recognition_comments ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "comments_hotel_read"   ON recognition_comments;
DROP POLICY IF EXISTS "comments_own_insert"   ON recognition_comments;
DROP POLICY IF EXISTS "comments_own_delete"   ON recognition_comments;

CREATE POLICY "comments_hotel_read"
  ON recognition_comments FOR SELECT
  USING (hotel = current_employee_hotel());

CREATE POLICY "comments_own_insert"
  ON recognition_comments FOR INSERT
  WITH CHECK (
    hotel = current_employee_hotel()
    AND employee_id = current_employee_id()
  );

CREATE POLICY "comments_own_delete"
  ON recognition_comments FOR DELETE
  USING (employee_id = current_employee_id());

-- ─── notifications ────────────────────────────────────────────────────────────

ALTER TABLE notifications ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "notifications_own_select"    ON notifications;
DROP POLICY IF EXISTS "notifications_own_update"    ON notifications;

-- Employees can only read their OWN notifications — never other employees'.
CREATE POLICY "notifications_own_select"
  ON notifications FOR SELECT
  USING (
    employee_id = current_employee_id()
    AND hotel   = current_employee_hotel()
  );

-- Employees can mark their own notifications read (via RPC).
-- Direct UPDATE is blocked — use mark_notification_read RPC instead.

-- ─── mood_entries ─────────────────────────────────────────────────────────────
--
-- mood_entries was created in migration 004 with a `user_id` column (profiles FK).
-- profiles was dropped in migration 030. Add employee_id so the hotel-based
-- submit_mood RPC and RLS can reference a valid employees FK.

ALTER TABLE mood_entries
  ADD COLUMN IF NOT EXISTS employee_id uuid REFERENCES public.employees(id) ON DELETE CASCADE;

ALTER TABLE mood_entries ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "mood_own_select"  ON mood_entries;
DROP POLICY IF EXISTS "mood_own_insert"  ON mood_entries;

-- Mood data is private: employees can ONLY see their own entries.
CREATE POLICY "mood_own_select"
  ON mood_entries FOR SELECT
  USING (employee_id = current_employee_id());

-- INSERT via submit_mood RPC (SECURITY DEFINER), not direct table write.

-- ─── points_ledger ────────────────────────────────────────────────────────────

ALTER TABLE points_ledger ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "ledger_own_select"    ON points_ledger;

-- Employees can read their own ledger history only.
CREATE POLICY "ledger_own_select"
  ON points_ledger FOR SELECT
  USING (employee_id = current_employee_id());

-- Immutable: no direct INSERT/UPDATE/DELETE from client side.

-- ─── redemptions ──────────────────────────────────────────────────────────────

ALTER TABLE redemptions ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "redemptions_own_select"    ON redemptions;

CREATE POLICY "redemptions_own_select"
  ON redemptions FOR SELECT
  USING (
    employee_id = current_employee_id()
    AND hotel   = current_employee_hotel()
  );

-- Mutations go through Edge Functions (redeem-reward, cancel-redemption).

-- ─── rewards ──────────────────────────────────────────────────────────────────

ALTER TABLE rewards ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "rewards_hotel_read"  ON rewards;

-- Employees can browse their hotel's catalogue.
CREATE POLICY "rewards_hotel_read"
  ON rewards FOR SELECT
  USING (hotel = current_employee_hotel());

-- ─── messages (hotel chat) ────────────────────────────────────────────────────

ALTER TABLE messages ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "messages_hotel_read"    ON messages;
DROP POLICY IF EXISTS "messages_hotel_insert"  ON messages;

CREATE POLICY "messages_hotel_read"
  ON messages FOR SELECT
  USING (hotel = current_employee_hotel());

CREATE POLICY "messages_hotel_insert"
  ON messages FOR INSERT
  WITH CHECK (
    hotel     = current_employee_hotel()
    AND sender_id = current_employee_id()
  );

-- No UPDATE/DELETE: chat messages are immutable.

-- ─── push_tokens ──────────────────────────────────────────────────────────────

ALTER TABLE push_tokens ENABLE ROW LEVEL SECURITY;

-- No SELECT policy for employees — tokens are read by service_role only.
-- All writes go through upsert_push_token() SECURITY DEFINER RPC.

-- ─── hotel_settings ───────────────────────────────────────────────────────────

ALTER TABLE hotel_settings ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "hotel_settings_employee_read"  ON hotel_settings;

CREATE POLICY "hotel_settings_employee_read"
  ON hotel_settings FOR SELECT
  USING (hotel = current_employee_hotel());

-- No employee writes: only service_role / admin can change hotel settings.

-- ─── employee_active_sessions ─────────────────────────────────────────────────

ALTER TABLE employee_active_sessions ENABLE ROW LEVEL SECURITY;

-- No direct access from client: session management goes through RPCs only.
-- validate_session, revoke_employee_session, authenticate_employee are all
-- SECURITY DEFINER and run as the DB owner.

-- ─── employee_password_auth ───────────────────────────────────────────────────

DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_name = 'employee_password_auth' AND table_schema = 'public'
  ) THEN
    EXECUTE 'ALTER TABLE employee_password_auth ENABLE ROW LEVEL SECURITY';
    -- Zero direct access: all auth goes through SECURITY DEFINER RPCs.
    -- No policy needed — default deny applies.
  END IF;
END;
$$;

-- ─── employee_reaction_allocations ───────────────────────────────────────────

ALTER TABLE employee_reaction_allocations ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "allocations_own_select"  ON employee_reaction_allocations;

CREATE POLICY "allocations_own_select"
  ON employee_reaction_allocations FOR SELECT
  USING (employee_id = current_employee_id());

-- ─── initiatives ──────────────────────────────────────────────────────────────

ALTER TABLE initiatives ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "initiatives_hotel_read"  ON initiatives;

CREATE POLICY "initiatives_hotel_read"
  ON initiatives FOR SELECT
  USING (hotel = current_employee_hotel());

-- ─── badges + user_badges ────────────────────────────────────────────────────

-- badges was created in migration 004 with company_id (old schema).
-- Add hotel column so hotel-based RLS can apply; existing rows default to NULL
-- which means "global/system badge" visible to all employees.
ALTER TABLE badges
  ADD COLUMN IF NOT EXISTS hotel text;

ALTER TABLE badges ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "badges_hotel_read"  ON badges;

CREATE POLICY "badges_hotel_read"
  ON badges FOR SELECT
  USING (hotel = current_employee_hotel() OR hotel IS NULL);

ALTER TABLE user_badges ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "user_badges_own_select"  ON user_badges;
DROP POLICY IF EXISTS "user_badges_hotel_select" ON user_badges;

-- Employees can see their own badges.
-- user_badges uses `user_id` (populated with employee UUIDs by evaluate-badges
-- Edge Function — the column predates the employees table rename).
CREATE POLICY "user_badges_own_select"
  ON user_badges FOR SELECT
  USING (user_id = current_employee_id());

-- ─── rate_limits ──────────────────────────────────────────────────────────────

DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_name = 'rate_limits' AND table_schema = 'public'
  ) THEN
    EXECUTE 'ALTER TABLE rate_limits ENABLE ROW LEVEL SECURITY';
    -- No client access: rate_limits is read/written by SECURITY DEFINER functions only.
  END IF;
END;
$$;

-- ─── audit_logs ───────────────────────────────────────────────────────────────

DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_name = 'audit_logs' AND table_schema = 'public'
  ) THEN
    EXECUTE 'ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY';
    -- No client SELECT: audit logs are admin-only (service_role).
  END IF;
END;
$$;
