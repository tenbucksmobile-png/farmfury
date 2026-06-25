-- =============================================================================
-- IndabaCares — Demo Account Seed
-- =============================================================================
--
-- Creates DEMO01 (the reviewer account) + 4 fictional colleagues at
-- Indaba Hotel, with realistic recognitions, reactions, rewards, and
-- mood history so every screen has visible content.
--
-- HOW TO RUN
--   Paste into the Supabase SQL editor and execute with service_role.
--   Re-running is safe — the first block wipes the demo rows cleanly.
--
-- CREDENTIALS
--   Employee Code : DEMO01
--   Hotel         : Indaba Hotel
--   Password      : DemoViewer1!
--
-- REMOVE AFTER APPROVAL
--   Run the cleanup block at the bottom of this file once Apple and
--   Google have approved the app, then delete this file.
-- =============================================================================


-- =============================================================================
-- SECTION 1 — SEED
-- =============================================================================

DO $$
DECLARE
  -- ── Fixed employee IDs ────────────────────────────────────────────────────
  v_demo01  uuid := 'a0000001-0000-0000-0000-000000000001';
  v_zanele  uuid := 'a0000001-0000-0000-0000-000000000002';
  v_sipho   uuid := 'a0000001-0000-0000-0000-000000000003';
  v_ayanda  uuid := 'a0000001-0000-0000-0000-000000000004';
  v_thandi  uuid := 'a0000001-0000-0000-0000-000000000005';

  -- ── Fixed recognition IDs ─────────────────────────────────────────────────
  v_rec1    uuid := 'b0000001-0000-0000-0000-000000000001'; -- Zanele  → Demo01
  v_rec2    uuid := 'b0000001-0000-0000-0000-000000000002'; -- Sipho   → Demo01
  v_rec3    uuid := 'b0000001-0000-0000-0000-000000000003'; -- Thandi  → Demo01
  v_rec4    uuid := 'b0000001-0000-0000-0000-000000000004'; -- Demo01  → Sipho
  v_rec5    uuid := 'b0000001-0000-0000-0000-000000000005'; -- Demo01  → Ayanda
  v_rec6    uuid := 'b0000001-0000-0000-0000-000000000006'; -- Zanele  → Ayanda

  -- ── Fixed reward IDs ──────────────────────────────────────────────────────
  v_rwd1    uuid := 'c0000001-0000-0000-0000-000000000001';
  v_rwd2    uuid := 'c0000001-0000-0000-0000-000000000002';
  v_rwd3    uuid := 'c0000001-0000-0000-0000-000000000003';
  v_rwd4    uuid := 'c0000001-0000-0000-0000-000000000004';

  v_hotel   text := 'Indaba Hotel';

BEGIN

  -- ────────────────────────────────────────────────────────────────────────────
  -- 1. WIPE any previous demo run (safe to re-run)
  -- ────────────────────────────────────────────────────────────────────────────

  DELETE FROM public.employee_reaction_allocations
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  DELETE FROM public.recognition_reactions
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  DELETE FROM public.recognition_comments
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  DELETE FROM public.recognition_likes
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  DELETE FROM public.recognitions
    WHERE id IN (v_rec1, v_rec2, v_rec3, v_rec4, v_rec5, v_rec6);

  DELETE FROM public.mood_entries
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  DELETE FROM public.redemptions
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  DELETE FROM public.points_ledger
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  DELETE FROM public.employee_active_sessions
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  DELETE FROM public.notifications
    WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);

  -- Also catch any redemptions referencing these reward rows from prior runs
  DELETE FROM public.redemptions
    WHERE reward_id IN (v_rwd1, v_rwd2, v_rwd3, v_rwd4);

  DELETE FROM public.rewards
    WHERE id IN (v_rwd1, v_rwd2, v_rwd3, v_rwd4);

  DELETE FROM public.employees
    WHERE id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);


  -- ────────────────────────────────────────────────────────────────────────────
  -- 2. EMPLOYEES
  -- ────────────────────────────────────────────────────────────────────────────
  --
  -- DEMO01   — the App Store / Play Store reviewer account
  -- IH002–05 — fictional colleagues who populate the feed and leaderboard

  INSERT INTO public.employees
    (id, full_name, employee_code, hotel, department, status)
  VALUES
    (v_demo01, 'Demo User',      'DEMO01', v_hotel, 'Front of House',  'active'),
    (v_zanele, 'Zanele Mokoena', 'IH002',  v_hotel, 'Front of House',  'active'),
    (v_sipho,  'Sipho Dlamini',  'IH003',  v_hotel, 'Food & Beverage', 'active'),
    (v_ayanda, 'Ayanda Nkosi',   'IH004',  v_hotel, 'Housekeeping',    'active'),
    (v_thandi, 'Thandi Sithole', 'IH005',  v_hotel, 'Concierge',       'active');

  -- Avatar photos for demo employees (stable randomuser.me portrait URLs)
  -- photo_url is the actual column (migration 044); used by both feed queries and get_leaderboard()
  UPDATE public.employees SET photo_url = 'https://randomuser.me/api/portraits/men/32.jpg'   WHERE id = v_demo01;
  UPDATE public.employees SET photo_url = 'https://randomuser.me/api/portraits/women/44.jpg' WHERE id = v_zanele;
  UPDATE public.employees SET photo_url = 'https://randomuser.me/api/portraits/men/52.jpg'   WHERE id = v_sipho;
  UPDATE public.employees SET photo_url = 'https://randomuser.me/api/portraits/women/68.jpg' WHERE id = v_ayanda;
  UPDATE public.employees SET photo_url = 'https://randomuser.me/api/portraits/women/26.jpg' WHERE id = v_thandi;


  -- ────────────────────────────────────────────────────────────────────────────
  -- 3. PASSWORD FOR DEMO01
  -- ────────────────────────────────────────────────────────────────────────────
  --
  -- Sets bcrypt hash for 'DemoViewer1!' (cost 12, same as production).
  -- The guard trigger (trg_guard_points_balance) only fires on UPDATE OF
  -- points_balance — this UPDATE touches password_hash only, so no GUC needed.

  UPDATE public.employees
  SET    password_hash = crypt('DemoViewer1!', gen_salt('bf', 12))
  WHERE  id = v_demo01;


  -- ────────────────────────────────────────────────────────────────────────────
  -- 4. RECOGNITIONS
  -- ────────────────────────────────────────────────────────────────────────────
  --
  -- Migration 034 removed the auto-points trigger on recognitions.
  -- Points are granted explicitly in section 5 via admin_grant_points().
  --
  -- trg_notify_recognition fires on INSERT and tries to write to notifications
  -- with a company_id NOT NULL column (orphaned from migration 005 — the FK was
  -- dropped in migration 030 but the NOT NULL remains). Disable it for the seed
  -- and re-enable immediately after so production behaviour is unchanged.

  ALTER TABLE public.recognitions DISABLE TRIGGER trg_notify_recognition;

  INSERT INTO public.recognitions
    (id, sender_id, receiver_id, message, badge, hotel, created_at)
  VALUES

    -- Three recognitions RECEIVED by Demo01 (drive visible feed + points)
    (v_rec1, v_zanele, v_demo01,
     'Thank you for staying late to assist our VIP guests last night! Your dedication made all the difference — they left absolutely delighted. We are lucky to have you on the team.',
     'Going the Extra Mile', v_hotel, now() - interval '3 days'),

    (v_rec2, v_sipho, v_demo01,
     'Demo always steps up when the restaurant gets slammed. Jumped in without being asked — that kind of teamwork keeps service running smoothly for everyone!',
     'Team Player', v_hotel, now() - interval '5 days'),

    (v_rec3, v_thandi, v_demo01,
     'Our guests keep asking for Demo by name. You have set the gold standard for what excellent service looks like at this hotel. Truly inspiring!',
     'Customer Excellence', v_hotel, now() - interval '8 days'),

    -- Two recognitions SENT by Demo01 (shows Give tab is functional)
    (v_rec4, v_demo01, v_sipho,
     'The Friday dinner service was absolutely exceptional Sipho! Every table was raving about the food and the attentiveness — you made us all proud.',
     'Hospitality Hero', v_hotel, now() - interval '2 days'),

    (v_rec5, v_demo01, v_ayanda,
     'Ayanda led the housekeeping team brilliantly during the conference weekend. Rooms turned over faster than ever and every guest comment was glowing. Above and beyond!',
     'Leadership', v_hotel, now() - interval '6 days'),

    -- One peer recognition (makes the feed feel like a real community)
    (v_rec6, v_zanele, v_ayanda,
     'Ayanda''s attention to detail has genuinely raised the bar in housekeeping. The rooms have never looked better and team morale is sky high. Keep it up!',
     'Team Player', v_hotel, now() - interval '10 days');


  ALTER TABLE public.recognitions ENABLE TRIGGER trg_notify_recognition;

  -- ────────────────────────────────────────────────────────────────────────────
  -- 5. POINTS FOR DEMO01's THREE RECEIVED RECOGNITIONS (3 × 10 = 30 pts)
  -- ────────────────────────────────────────────────────────────────────────────
  --
  -- admin_grant_points() sets the indabacares.allow_points_update GUC and
  -- bypasses the trg_guard_points_balance trigger, then writes to points_ledger.

  PERFORM public.admin_grant_points(v_demo01, 10, 'admin_bonus');
  PERFORM public.admin_grant_points(v_demo01, 10, 'admin_bonus');
  PERFORM public.admin_grant_points(v_demo01, 10, 'admin_bonus');


  -- ────────────────────────────────────────────────────────────────────────────
  -- 6. REACTIONS
  -- ────────────────────────────────────────────────────────────────────────────
  --
  -- The trg_reaction_points_insert trigger fires on INSERT and awards points
  -- to the recognition RECEIVER automatically (heart=50, smile=20, thumbs_up=10).
  --
  -- Points awarded to Demo01 from these reactions:
  --   rec1 ❤️  (Sipho)   → +50    rec1 👍 (Ayanda)   → +10
  --   rec2 😊 (Ayanda)  → +20    rec2 👍 (Thandi)   → +10
  --   rec3 ❤️  (Zanele)  → +50    rec3 😊 (Sipho)    → +20
  --   ─────────────────────────────────────────────────────
  --   Total from reactions = 160 pts
  --
  -- Grand total for Demo01: 30 (recognitions) + 160 (reactions) = 190 pts

  INSERT INTO public.recognition_reactions
    (recognition_id, employee_id, reaction_type, hotel, created_at)
  VALUES
    (v_rec1, v_sipho,  'heart',     v_hotel, now() - interval '3 days'  + interval '2 hours'),
    (v_rec1, v_ayanda, 'thumbs_up', v_hotel, now() - interval '3 days'  + interval '3 hours'),
    (v_rec2, v_ayanda, 'smile',     v_hotel, now() - interval '5 days'  + interval '1 hour'),
    (v_rec2, v_thandi, 'thumbs_up', v_hotel, now() - interval '5 days'  + interval '4 hours'),
    (v_rec3, v_zanele, 'heart',     v_hotel, now() - interval '8 days'  + interval '2 hours'),
    (v_rec3, v_sipho,  'smile',     v_hotel, now() - interval '8 days'  + interval '5 hours'),
    (v_rec4, v_zanele, 'heart',     v_hotel, now() - interval '2 days'  + interval '1 hour'),
    (v_rec5, v_thandi, 'thumbs_up', v_hotel, now() - interval '6 days'  + interval '3 hours'),
    (v_rec6, v_sipho,  'heart',     v_hotel, now() - interval '10 days' + interval '2 hours');


  -- ────────────────────────────────────────────────────────────────────────────
  -- 7. LIKES
  -- ────────────────────────────────────────────────────────────────────────────

  INSERT INTO public.recognition_likes
    (recognition_id, employee_id, hotel, created_at)
  VALUES
    (v_rec1, v_ayanda, v_hotel, now() - interval '3 days' + interval '4 hours'),
    (v_rec1, v_thandi, v_hotel, now() - interval '3 days' + interval '5 hours'),
    (v_rec2, v_zanele, v_hotel, now() - interval '5 days' + interval '2 hours'),
    (v_rec3, v_demo01, v_hotel, now() - interval '8 days' + interval '1 hour'),
    (v_rec3, v_sipho,  v_hotel, now() - interval '8 days' + interval '6 hours'),
    (v_rec4, v_sipho,  v_hotel, now() - interval '2 days' + interval '2 hours'),
    (v_rec5, v_ayanda, v_hotel, now() - interval '6 days' + interval '4 hours'),
    (v_rec6, v_demo01, v_hotel, now() - interval '10 days'+ interval '3 hours');


  -- ────────────────────────────────────────────────────────────────────────────
  -- 8. COMMENTS
  -- ────────────────────────────────────────────────────────────────────────────

  INSERT INTO public.recognition_comments
    (recognition_id, employee_id, body, hotel, created_at)
  VALUES
    (v_rec1, v_demo01,
     'Thank you so much Zanele — it was a genuine pleasure helping them! Moments like that are exactly why I love this job. 🙏',
     v_hotel, now() - interval '3 days' + interval '6 hours'),

    (v_rec1, v_thandi,
     'So well deserved! Demo always goes the extra mile — this is the third time I have seen it this month!',
     v_hotel, now() - interval '3 days' + interval '7 hours'),

    (v_rec4, v_sipho,
     'Thank you Demo! The whole team worked really hard that evening — so glad it showed. Means a lot coming from you!',
     v_hotel, now() - interval '2 days' + interval '3 hours'),

    (v_rec6, v_ayanda,
     'Thank you Zanele, it has been an amazing few weeks for the whole team. Everyone has been putting in such an effort!',
     v_hotel, now() - interval '10 days' + interval '5 hours');


  -- ────────────────────────────────────────────────────────────────────────────
  -- 9. REWARDS CATALOGUE
  -- ────────────────────────────────────────────────────────────────────────────
  --
  -- Four tiered rewards so the catalogue screen has clear content at
  -- different point thresholds. Demo01 (190 pts) can afford the first two.

  INSERT INTO public.rewards
    (id, title, description, points_required, hotel, stock)
  VALUES
    (v_rwd1,
     'Coffee & Muffin Voucher',
     'Enjoy a complimentary coffee and freshly baked muffin from our in-house café. A perfect mid-shift treat — collect from the staff canteen.',
     20, v_hotel, 50),

    (v_rwd2,
     'Spa Treatment — 30 Min',
     'Unwind with a 30-minute express massage or facial at the Indaba Spa. Book your slot through HR at least 48 hours in advance.',
     75, v_hotel, 10),

    (v_rwd3,
     'Team Lunch Voucher',
     'Treat yourself and one colleague to a two-course lunch in the hotel restaurant. Valid Monday to Friday, 12:00–14:30. Subject to availability.',
     150, v_hotel, 20),

    (v_rwd4,
     'Weekend Stay — Room Upgrade',
     'Enjoy a complimentary one-night upgrade to a Superior Room with breakfast for two. Subject to availability. Book through HR with 2 weeks'' notice.',
     500, v_hotel, 5);


  -- ────────────────────────────────────────────────────────────────────────────
  -- 10. MOOD HISTORY FOR DEMO01
  -- ────────────────────────────────────────────────────────────────────────────
  --
  -- mood_entries was created in migration 004 with company_id and user_id as
  -- NOT NULL columns that referenced profiles/companies (dropped in migration 030).
  -- The FK constraints were removed by CASCADE but the NOT NULL constraints
  -- remain on the columns. Dummy UUIDs satisfy NOT NULL. The employee_id column
  -- (added in migration 057/059) is what the app and RLS policies read.
  --
  -- Each row uses a unique dummy UUID for user_id so the original
  -- UNIQUE(user_id, entry_date) constraint is never triggered.

  INSERT INTO public.mood_entries
    (company_id,                                    user_id,                                        mood,      entry_date,        employee_id)
  VALUES
    ('00000099-0000-0000-0000-000000000001'::uuid,  '00000099-0000-0000-0000-000000000001'::uuid,  'amazing', current_date,      v_demo01),
    ('00000099-0000-0000-0000-000000000002'::uuid,  '00000099-0000-0000-0000-000000000002'::uuid,  'good',    current_date - 1,  v_demo01),
    ('00000099-0000-0000-0000-000000000003'::uuid,  '00000099-0000-0000-0000-000000000003'::uuid,  'amazing', current_date - 2,  v_demo01),
    ('00000099-0000-0000-0000-000000000004'::uuid,  '00000099-0000-0000-0000-000000000004'::uuid,  'okay',    current_date - 3,  v_demo01),
    ('00000099-0000-0000-0000-000000000005'::uuid,  '00000099-0000-0000-0000-000000000005'::uuid,  'good',    current_date - 4,  v_demo01);


  -- ────────────────────────────────────────────────────────────────────────────
  -- 11. NOTIFICATIONS FOR DEMO01
  -- ────────────────────────────────────────────────────────────────────────────
  --
  -- Populates the notification bell so the reviewer sees content.
  -- Columns: employee_id, hotel, type, title, message, read, created_at
  -- Types: recognition_received, admin_announcement
  -- company_id and user_id are nullable (migrations 084–085) — omitted here.

  INSERT INTO public.notifications
    (employee_id, hotel, type, title, message, read, created_at)
  VALUES
    (v_demo01, v_hotel, 'recognition_received',
     'Zanele Mokoena recognised you!',
     'Thank you for staying late to assist our VIP guests last night! Your dedication made all the difference.',
     false, now() - interval '3 days' + interval '30 minutes'),

    (v_demo01, v_hotel, 'recognition_received',
     'Sipho Dlamini recognised you!',
     'Demo always steps up when the restaurant gets slammed — that kind of teamwork keeps service running smoothly!',
     false, now() - interval '5 days' + interval '30 minutes'),

    (v_demo01, v_hotel, 'recognition_received',
     'Thandi Sithole recognised you!',
     'Our guests keep asking for Demo by name. You have set the gold standard for excellent service at this hotel.',
     true, now() - interval '8 days' + interval '30 minutes'),

    (v_demo01, v_hotel, 'admin_announcement',
     'Welcome to IndabaCares!',
     'You can now give and receive recognitions, track your points, and redeem rewards. Thank you for being part of the team!',
     true, now() - interval '30 days');


  RAISE NOTICE '✅ Demo seed complete.';
  RAISE NOTICE '   DEMO01 points balance: 190 (30 from recognitions + 160 from reactions)';
  RAISE NOTICE '   Rewards: 4 items seeded (can afford Coffee Voucher at 20 pts and Spa at 75 pts)';
  RAISE NOTICE '   Mood: 5 entries seeded (last 5 days)';
  RAISE NOTICE '   Notifications: 4 seeded (2 unread recognition_received, 2 read)';

END;
$$;


-- =============================================================================
-- SECTION 2 — VERIFY (run separately to check the seed worked)
-- =============================================================================
--
-- SELECT full_name, employee_code, points_balance, status
-- FROM   public.employees
-- WHERE  employee_code IN ('DEMO01','IH002','IH003','IH004','IH005')
--   AND  hotel = 'Indaba Hotel'
-- ORDER BY employee_code;
--
-- SELECT COUNT(*) AS recognitions
-- FROM   public.recognitions
-- WHERE  hotel = 'Indaba Hotel'
--   AND  id::text LIKE 'b0000001%';


-- =============================================================================
-- SECTION 3 — CLEANUP (run after Apple & Google approval)
-- =============================================================================
--
-- Removes all demo rows. Also delete src/lib/demoCredentials.ts and the
-- Try Demo button in app/(auth)/employee-auth.tsx.
--
-- DO $$
-- DECLARE
--   v_demo01  uuid := 'a0000001-0000-0000-0000-000000000001';
--   v_zanele  uuid := 'a0000001-0000-0000-0000-000000000002';
--   v_sipho   uuid := 'a0000001-0000-0000-0000-000000000003';
--   v_ayanda  uuid := 'a0000001-0000-0000-0000-000000000004';
--   v_thandi  uuid := 'a0000001-0000-0000-0000-000000000005';
-- BEGIN
--   DELETE FROM public.employee_reaction_allocations
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.recognition_reactions
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.recognition_comments
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.recognition_likes
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.recognitions
--     WHERE id IN (
--       'b0000001-0000-0000-0000-000000000001',
--       'b0000001-0000-0000-0000-000000000002',
--       'b0000001-0000-0000-0000-000000000003',
--       'b0000001-0000-0000-0000-000000000004',
--       'b0000001-0000-0000-0000-000000000005',
--       'b0000001-0000-0000-0000-000000000006'
--     );
--   DELETE FROM public.mood_entries
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.notifications
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.redemptions
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.points_ledger
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.employee_active_sessions
--     WHERE employee_id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   DELETE FROM public.rewards
--     WHERE id IN (
--       'c0000001-0000-0000-0000-000000000001',
--       'c0000001-0000-0000-0000-000000000002',
--       'c0000001-0000-0000-0000-000000000003',
--       'c0000001-0000-0000-0000-000000000004'
--     );
--   DELETE FROM public.employees
--     WHERE id IN (v_demo01, v_zanele, v_sipho, v_ayanda, v_thandi);
--   RAISE NOTICE '✅ Demo data removed.';
-- END;
-- $$;
