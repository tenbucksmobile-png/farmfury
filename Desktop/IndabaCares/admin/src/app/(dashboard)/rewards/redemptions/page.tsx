import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { RedemptionsClient } from './redemptions-client';
import type { RedemptionRow } from './redemptions-client';

export const dynamic = 'force-dynamic';

const PAGE_SIZE = 20;

async function getRedemptions(status: string, page: number) {
  const db   = createAdminClient();
  const from = page * PAGE_SIZE;
  const to   = from + PAGE_SIZE - 1;

  let q = db
    .from('redemptions')
    .select(
      `id, points_used, status, rejection_reason, hotel,
       created_at, approved_at, rejected_at, fulfilled_at,
       employee:employees!employee_id ( id, full_name, photo_url, employee_code ),
       reward:rewards!reward_id ( id, title, image_url, points_required )`,
      { count: 'exact' },
    )
    .order('created_at', { ascending: false })
    .range(from, to);

  if (status !== 'all') q = q.eq('status', status);

  const { data, error, count } = await q;
  if (error) throw new Error(error.message);
  return { redemptions: (data ?? []) as unknown as RedemptionRow[], total: count ?? 0 };
}

export default async function RedemptionsPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string; page?: string }>;
}) {
  const { status = 'pending', page: pageStr = '0' } = await searchParams;
  const page = Math.max(0, parseInt(pageStr, 10) || 0);

  const { redemptions, total } = await getRedemptions(status, page);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Redemption Queue"
        description="Review and process employee reward redemptions"
      />
      <RedemptionsClient
        redemptions={redemptions}
        total={total}
        status={status}
        page={page}
      />
    </div>
  );
}
