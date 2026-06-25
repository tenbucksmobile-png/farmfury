import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { RewardsClient } from './rewards-client';

export const dynamic = 'force-dynamic';

async function getRewards(hotel?: string) {
  const db = createAdminClient();
  let q = db
    .from('rewards')
    .select('id, title, description, points_required, hotel, hotels, stock, image_url, category, wicode, created_at')
    .order('hotel')
    .order('points_required');

  if (hotel) q = q.eq('hotel', hotel);

  const { data, error } = await q;
  if (error) throw new Error(error.message);
  return data ?? [];
}

export default async function RewardsPage({
  searchParams,
}: {
  searchParams: Promise<{ hotel?: string }>;
}) {
  const { hotel } = await searchParams;
  const rewards = await getRewards(hotel);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Rewards"
        description="Manage the reward catalogue. Set points required, stock, and hotel."
      />
      <RewardsClient rewards={rewards} selectedHotel={hotel} />
    </div>
  );
}
