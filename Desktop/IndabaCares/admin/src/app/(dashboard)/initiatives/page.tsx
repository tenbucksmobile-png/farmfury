import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { InitiativesClient } from './initiatives-client';

export const dynamic = 'force-dynamic';

async function getInitiatives(hotel?: string) {
  const db = createAdminClient();
  let q = db
    .from('initiatives')
    .select('id, hotel, tab, mascot_url, image_urls, video_url, sort_order, created_at')
    .order('hotel')
    .order('tab')
    .order('sort_order');

  if (hotel) q = q.eq('hotel', hotel);

  const { data, error } = await q;
  if (error) throw new Error(error.message);
  return data ?? [];
}

export default async function InitiativesPage({
  searchParams,
}: {
  searchParams: Promise<{ hotel?: string }>;
}) {
  const { hotel } = await searchParams;
  const initiatives = await getInitiatives(hotel);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Indaba Cares"
        description="Manage CSR initiative content per hotel. Upload mascot images, photo galleries, and videos."
      />
      <InitiativesClient initiatives={initiatives} selectedHotel={hotel} />
    </div>
  );
}
