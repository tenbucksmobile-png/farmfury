/**
 * Canonical hotel list — mirrors is_valid_hotel() in migration 017
 * and admin/src/lib/hotels.ts.
 * Update all three locations if hotels are added or renamed.
 */

export const HOTELS = [
  'Indaba Hotel',
  'Indaba Lodge Richards Bay',
  'Indaba Lodge Gaborone',
  'Chobe Safari Lodge',
  'Nata Lodge',
  'African Procurement Agencies',
] as const;

export type Hotel = (typeof HOTELS)[number];

export const APA_HOTEL = 'African Procurement Agencies' as const;
