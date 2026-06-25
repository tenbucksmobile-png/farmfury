-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 072 — CSR multi-hotel: relax initiatives RLS
--
-- Previously employees could only read their own hotel's initiatives.
-- We now allow any authenticated/anon user to read ALL hotels' CSR content
-- so that the mobile app can show a hotel-picker and browse any property's
-- Indaba Cares initiatives.
--
-- CSR content (Billy Says, Feed the Kids, Mandela Day, Mobile Clinic …) is
-- public-facing, non-sensitive community/charity material — not employee data.
-- ─────────────────────────────────────────────────────────────────────────────

-- Drop the previous hotel-scoped policies (created in migrations 051 and 059)
DROP POLICY IF EXISTS "initiatives_hotel_select" ON public.initiatives;
DROP POLICY IF EXISTS "initiatives_hotel_read"   ON public.initiatives;

-- Allow any session to read initiatives from any hotel.
-- Filtering to a specific hotel is done at query level in the application.
CREATE POLICY "initiatives_public_read"
  ON public.initiatives
  FOR SELECT TO anon, authenticated
  USING (true);
