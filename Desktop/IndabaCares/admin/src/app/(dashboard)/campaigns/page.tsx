import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { CampaignsClient } from './campaigns-client';

export const dynamic = 'force-dynamic';

export type Campaign = {
  id:                   string;
  title:                string;
  description:          string | null;
  type:                 'recognition' | 'sponsor' | 'both';
  points_multiplier:    number;
  hotel:                string;
  start_date:           string;
  end_date:             string;
  created_at:           string;
  sponsor_name:         string | null;
  banner_url:           string | null;
  banner_link_url:      string | null;
  voucher_description:  string | null;
};

async function getCampaigns(hotel?: string): Promise<Campaign[]> {
  const db = createAdminClient();

  let q = db
    .from('campaigns')
    .select('id, title, description, type, points_multiplier, hotel, start_date, end_date, created_at, sponsor_name, banner_url, banner_link_url, voucher_description')
    .order('start_date', { ascending: false });

  if (hotel) q = q.eq('hotel', hotel);

  const { data, error } = await q;
  if (error) throw new Error(error.message);
  return (data ?? []) as Campaign[];
}

export default async function CampaignsPage({
  searchParams,
}: {
  searchParams: Promise<{ hotel?: string }>;
}) {
  const { hotel } = await searchParams;
  const campaigns  = await getCampaigns(hotel);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Campaigns"
        description="Boost recognition points during special events and periods."
      />
      <CampaignsClient campaigns={campaigns} selectedHotel={hotel} />
    </div>
  );
}
