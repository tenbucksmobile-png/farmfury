import { COLORS } from '@/lib/constants';

export interface CompanyTheme {
  primaryColor: string;
}

/**
 * Returns the app brand theme.
 * Company-specific branding has been removed with System 1 (Supabase Auth).
 * Theme is now a single static palette driven by COLORS constants.
 */
export function useCompanyTheme(): CompanyTheme {
  return { primaryColor: COLORS.primary };
}
