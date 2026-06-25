-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 077 — Remove Chobe Bush Lodge
--
-- Chobe Bush Lodge is being merged into Chobe Safari Lodge.
-- 1. Move all Chobe Bush Lodge employees to Chobe Safari Lodge
-- 2. Drop the hotel CHECK constraint and recreate it without Chobe Bush Lodge
-- ─────────────────────────────────────────────────────────────────────────────

-- ─── 1. Merge employees ───────────────────────────────────────────────────────

UPDATE public.employees
SET    hotel = 'Chobe Safari Lodge'
WHERE  hotel = 'Chobe Bush Lodge';

-- ─── 2. Migrate any related hotel-scoped data ─────────────────────────────────

UPDATE public.recognitions
SET    hotel = 'Chobe Safari Lodge'
WHERE  hotel = 'Chobe Bush Lodge';

UPDATE public.points_ledger
SET    hotel = 'Chobe Safari Lodge'
WHERE  hotel = 'Chobe Bush Lodge';

UPDATE public.redemptions
SET    hotel = 'Chobe Safari Lodge'
WHERE  hotel = 'Chobe Bush Lodge';

UPDATE public.rewards
SET    hotel = 'Chobe Safari Lodge'
WHERE  hotel = 'Chobe Bush Lodge';

-- ─── 3. Rebuild hotel CHECK constraint ───────────────────────────────────────

ALTER TABLE public.employees
  DROP CONSTRAINT IF EXISTS employees_hotel_check;

ALTER TABLE public.employees
  ADD CONSTRAINT employees_hotel_check CHECK (hotel IN (
    'Indaba Hotel',
    'Indaba Lodge Richards Bay',
    'Indaba Lodge Gaborone',
    'Chobe Safari Lodge',
    'Nata Lodge',
    'African Procurement Agencies'
  ));

-- ─── 4. Update is_valid_hotel() if it exists ─────────────────────────────────

CREATE OR REPLACE FUNCTION public.is_valid_hotel(h text)
RETURNS boolean
LANGUAGE sql
IMMUTABLE
AS $$
  SELECT h IN (
    'Indaba Hotel',
    'Indaba Lodge Richards Bay',
    'Indaba Lodge Gaborone',
    'Chobe Safari Lodge',
    'Nata Lodge',
    'African Procurement Agencies'
  );
$$;
