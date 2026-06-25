/**
 * Canonical hotel list — mirrors is_valid_hotel() in migration 017.
 * Update here if hotels are added or renamed.
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
