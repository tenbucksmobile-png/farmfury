import { supabase } from '@/lib/supabase';

export interface Campaign {
  id:                   string;
  title:                string;
  description:          string | null;
  type:                 'recognition' | 'sponsor' | 'both';
  points_multiplier:    number;
  hotel:                string;
  start_date:           string;
  end_date:             string;
  days_remaining:       number;
  is_active:            boolean;
  sponsor_name:         string | null;
  banner_url:           string | null;
  banner_link_url:      string | null;
  voucher_description:  string | null;
}

/**
 * Returns active and upcoming campaigns for the employee's hotel.
 * Calls the get_campaigns_for_hotel RPC (migration 080).
 * Active campaigns are returned first, sponsor types before recognition-only.
 */
export async function getCampaignsForHotel(hotel: string): Promise<Campaign[]> {
  const { data, error } = await supabase
    .rpc('get_campaigns_for_hotel', { p_hotel: hotel });

  if (error) throw new Error(error.message);
  return (data ?? []) as Campaign[];
}
