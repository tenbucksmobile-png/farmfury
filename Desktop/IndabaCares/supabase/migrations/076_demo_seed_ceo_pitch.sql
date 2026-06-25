-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 076 — CEO Pitch Demo Seed Data
--
-- Creates 16 demo employees (8 management, 8 staff), points ledger entries,
-- feed recognitions (peer + skill), and today's celebrations (birthday +
-- anniversaries) — all scoped to 'Indaba Hotel'.
--
-- TO REMOVE AFTER TESTING:
--   DELETE FROM employees WHERE employee_code LIKE 'DEMO%';
--   DELETE FROM celebrations WHERE id IN (SELECT id FROM celebrations WHERE employee_id IN (SELECT id FROM employees WHERE employee_code LIKE 'DEMO%'));
--   (recognitions, points_ledger, celebrations cascade via FK or can be deleted similarly)
-- ─────────────────────────────────────────────────────────────────────────────

DO $$
DECLARE
  -- Management IDs
  id_thabo    uuid := gen_random_uuid();
  id_priya    uuid := gen_random_uuid();
  id_james    uuid := gen_random_uuid();
  id_naledi   uuid := gen_random_uuid();
  id_sasha    uuid := gen_random_uuid();
  id_michael  uuid := gen_random_uuid();
  id_zanele   uuid := gen_random_uuid();
  id_david    uuid := gen_random_uuid();

  -- Staff IDs
  id_lerato   uuid := gen_random_uuid();
  id_sipho    uuid := gen_random_uuid();
  id_amahle   uuid := gen_random_uuid();
  id_ryan     uuid := gen_random_uuid();
  id_fatima   uuid := gen_random_uuid();
  id_lindo    uuid := gen_random_uuid();
  id_neo      uuid := gen_random_uuid();
  id_kefiloe  uuid := gen_random_uuid();

  hotel text := 'Indaba Hotel';

BEGIN

-- ─── 1. Demo Employees ───────────────────────────────────────────────────────

INSERT INTO public.employees
  (id, full_name, employee_code, hotel, department, position, status, is_manager, points_balance, has_seen_welcome)
VALUES
  -- Management
  (id_thabo,   'Thabo Molefe',        'DEMO001', hotel, 'Operations',      'General Manager',        'active', true,  480, true),
  (id_priya,   'Priya Naidoo',        'DEMO002', hotel, 'Operations',      'Revenue Manager',        'active', true,  420, true),
  (id_james,   'James van der Berg',  'DEMO003', hotel, 'Food & Beverage', 'F&B Manager',            'active', true,  360, true),
  (id_naledi,  'Naledi Dlamini',      'DEMO004', hotel, 'Human Resources', 'HR Manager',             'active', true,  310, true),
  (id_sasha,   'Sasha Pereira',       'DEMO005', hotel, 'Front Office',    'Front Office Manager',   'active', true,  275, true),
  (id_michael, 'Michael Khumalo',     'DEMO006', hotel, 'Operations',      'Operations Manager',     'active', true,  240, true),
  (id_zanele,  'Zanele Mokoena',      'DEMO007', hotel, 'Events',          'Events Manager',         'active', true,  195, true),
  (id_david,   'David Pietersen',     'DEMO008', hotel, 'Operations',      'Finance Manager',        'active', true,  160, true),
  -- Staff
  (id_lerato,  'Lerato Sithole',      'DEMO009', hotel, 'Front Office',    'Front Desk Agent',       'active', false, 320, true),
  (id_sipho,   'Sipho Ndlovu',        'DEMO010', hotel, 'Food & Beverage', 'Senior Waiter',          'active', false, 290, true),
  (id_amahle,  'Amahle Zulu',         'DEMO011', hotel, 'Housekeeping',    'Head Housekeeper',       'active', false, 240, true),
  (id_ryan,    'Ryan Botha',          'DEMO012', hotel, 'Concierge',       'Senior Concierge',       'active', false, 210, true),
  (id_fatima,  'Fatima Abrahams',     'DEMO013', hotel, 'Guest Services',  'Guest Relations Officer','active', false, 175, true),
  (id_lindo,   'Lindo Dube',          'DEMO014', hotel, 'Food & Beverage', 'Barista',                'active', false, 140, true),
  (id_neo,     'Neo Mahlangu',        'DEMO015', hotel, 'Front Office',    'Receptionist',           'active', false, 110, true),
  (id_kefiloe, 'Kefiloe Moagi',       'DEMO016', hotel, 'Housekeeping',    'Housekeeping Supervisor','active', false,  75, true);


-- ─── 2. Points Ledger (for leaderboard — current month + year) ───────────────

INSERT INTO public.points_ledger (employee_id, points, source, hotel, created_at) VALUES
  -- Management
  (id_thabo,   180, 'recognition_received', hotel, now() - interval '2 days'),
  (id_thabo,   300, 'admin_bonus',          hotel, now() - interval '10 days'),
  (id_priya,   220, 'recognition_received', hotel, now() - interval '3 days'),
  (id_priya,   200, 'admin_bonus',          hotel, now() - interval '8 days'),
  (id_james,   160, 'recognition_received', hotel, now() - interval '1 day'),
  (id_james,   200, 'admin_bonus',          hotel, now() - interval '12 days'),
  (id_naledi,  130, 'recognition_received', hotel, now() - interval '4 days'),
  (id_naledi,  180, 'admin_bonus',          hotel, now() - interval '9 days'),
  (id_sasha,   125, 'recognition_received', hotel, now() - interval '2 days'),
  (id_sasha,   150, 'admin_bonus',          hotel, now() - interval '11 days'),
  (id_michael, 100, 'recognition_received', hotel, now() - interval '5 days'),
  (id_michael, 140, 'admin_bonus',          hotel, now() - interval '7 days'),
  (id_zanele,   95, 'recognition_received', hotel, now() - interval '3 days'),
  (id_zanele,  100, 'admin_bonus',          hotel, now() - interval '14 days'),
  (id_david,    80, 'recognition_received', hotel, now() - interval '6 days'),
  (id_david,    80, 'admin_bonus',          hotel, now() - interval '15 days'),
  -- Staff
  (id_lerato,  120, 'recognition_received', hotel, now() - interval '1 day'),
  (id_lerato,  200, 'recognition_received', hotel, now() - interval '5 days'),
  (id_sipho,   140, 'recognition_received', hotel, now() - interval '2 days'),
  (id_sipho,   150, 'recognition_received', hotel, now() - interval '6 days'),
  (id_amahle,  100, 'recognition_received', hotel, now() - interval '3 days'),
  (id_amahle,  140, 'recognition_received', hotel, now() - interval '8 days'),
  (id_ryan,     90, 'recognition_received', hotel, now() - interval '2 days'),
  (id_ryan,    120, 'recognition_received', hotel, now() - interval '7 days'),
  (id_fatima,   75, 'recognition_received', hotel, now() - interval '4 days'),
  (id_fatima,  100, 'recognition_received', hotel, now() - interval '9 days'),
  (id_lindo,    60, 'recognition_received', hotel, now() - interval '3 days'),
  (id_lindo,    80, 'recognition_received', hotel, now() - interval '10 days'),
  (id_neo,      50, 'recognition_received', hotel, now() - interval '5 days'),
  (id_neo,      60, 'recognition_received', hotel, now() - interval '11 days'),
  (id_kefiloe,  35, 'recognition_received', hotel, now() - interval '4 days'),
  (id_kefiloe,  40, 'recognition_received', hotel, now() - interval '12 days');


-- ─── 3. Feed — Individual Recognition Cards ───────────────────────────────────
-- Disable notification trigger during seed (notifications.company_id NOT NULL
-- would fire and fail — demo data doesn't need notifications).

ALTER TABLE public.recognitions DISABLE TRIGGER USER;

INSERT INTO public.recognitions (id, sender_id, receiver_id, message, badge, hotel, created_at) VALUES

  (gen_random_uuid(), id_naledi, id_lerato,
   'Lerato went above and beyond for our VIP guests this week — calm, professional, and warm. Exactly what Indaba stands for! 🌟',
   'Customer Excellence', hotel, now() - interval '1 hour'),

  (gen_random_uuid(), id_james, id_sipho,
   'Sipho ran the entire dinner service single-handedly last night when we were short-staffed. That''s dedication. The team noticed and guests raved! 🚀',
   'Going the Extra Mile', hotel, now() - interval '3 hours'),

  (gen_random_uuid(), id_sasha, id_ryan,
   'Ryan''s local knowledge and genuine warmth has guests asking for him by name. He is the definition of a Hospitality Hero 🏨',
   'Hospitality Hero', hotel, now() - interval '5 hours'),

  (gen_random_uuid(), id_lerato, id_amahle,
   'Amahle turned around 20 rooms in record time without compromising on quality. The whole floor was immaculate! 🤝',
   'Team Player', hotel, now() - interval '8 hours'),

  (gen_random_uuid(), id_thabo, id_priya,
   'Priya''s revenue strategy this quarter has delivered outstanding results. Her leadership and analytical thinking is truly exceptional 👑',
   'Leadership', hotel, now() - interval '12 hours'),

  (gen_random_uuid(), id_sipho, id_fatima,
   'Fatima resolved a tricky guest complaint in minutes — turned a negative experience into a 5-star review. Pure innovation under pressure! 💡',
   'Innovation', hotel, now() - interval '18 hours'),

  (gen_random_uuid(), id_ryan, id_neo,
   'Neo handled the busiest check-in morning of the year without missing a beat. Guests left the desk smiling every single time 🌟',
   'Customer Excellence', hotel, now() - interval '1 day'),

  (gen_random_uuid(), id_michael, id_zanele,
   'The gala dinner last weekend was flawless. Zanele''s attention to detail and coordination made it one of the best events we''ve hosted 🚀',
   'Going the Extra Mile', hotel, now() - interval '2 days');


-- ─── 4. Feed — More Recognition Cards (covering all badge types) ─────────────

INSERT INTO public.recognitions (id, sender_id, receiver_id, message, badge, hotel, created_at) VALUES

  (gen_random_uuid(), id_naledi, id_kefiloe,
   'Kefiloe demonstrates exceptional leadership on the floor — her team follows her lead and standards have never been higher.',
   'Leadership', hotel, now() - interval '2 hours'),

  (gen_random_uuid(), id_james, id_lindo,
   'Lindo''s energy and creativity behind the counter is infectious — guests ask for him by name every morning. Keep shining! ☀️',
   'Going the Extra Mile', hotel, now() - interval '6 hours'),

  (gen_random_uuid(), id_sasha, id_fatima,
   'Fatima''s innovative approach to the guest welcome process has already received two 5-star mentions this month. Brilliant thinking!',
   'Innovation', hotel, now() - interval '10 hours'),

  (gen_random_uuid(), id_thabo, id_michael,
   'Michael''s calm and resourceful response to the maintenance crisis kept operations running without guests noticing a thing.',
   'Hospitality Hero', hotel, now() - interval '1 day'),

  (gen_random_uuid(), id_priya, id_david,
   'David pulled the team together during the audit crunch — available, collaborative, and always reliable under pressure.',
   'Team Player', hotel, now() - interval '2 days');

ALTER TABLE public.recognitions ENABLE TRIGGER USER;


-- ─── 5. Celebrations — Today's Birthday & Anniversaries ──────────────────────

INSERT INTO public.celebrations (id, employee_id, type, milestone, celebrated_on, hotel, created_at) VALUES

  -- Birthday
  (gen_random_uuid(), id_sipho,
   'birthday', null, CURRENT_DATE, hotel, now() - interval '30 minutes'),

  -- Work anniversaries
  (gen_random_uuid(), id_lerato,
   'anniversary', 3, CURRENT_DATE, hotel, now() - interval '25 minutes'),

  (gen_random_uuid(), id_amahle,
   'anniversary', 5, CURRENT_DATE, hotel, now() - interval '20 minutes'),

  (gen_random_uuid(), id_ryan,
   'anniversary', 1, CURRENT_DATE, hotel, now() - interval '15 minutes');

END $$;
