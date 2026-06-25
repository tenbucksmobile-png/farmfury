import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { RedemptionsClient } from './redemptions-client';

export const dynamic = 'force-dynamic';

async function getRedemptions(hotel?: string, status?: string) {
  const db = createAdminClient();

  let q = db
    .from('redemptions')
    .select(`
      id, points_used, status, hotel, created_at,
      approved_at, rejected_at, fulfilled_at, rejection_reason,
      employee:employees!employee_id ( id, full_name, employee_code, points_balance ),
      reward:rewards!reward_id       ( id, title, points_required )
    `)
    .order('created_at', { ascending: false });

  if (hotel)  q = q.eq('hotel', hotel);
  if (status && status !== 'all') q = q.eq('status', status);

  const { data, error } = await q;
  if (error) throw new Error(error.message);
  return (data ?? []) as any[];
}

export default async function RedemptionsPage({
  searchParams,
}: {
  searchParams: Promise<{ hotel?: string; status?: string }>;
}) {
  const { hotel, status } = await searchParams;

  const redemptions = await getRedemptions(hotel, status);

  const counts = {
    pending:   redemptions.filter((r: any) => r.status === 'pending').length,
    approved:  redemptions.filter((r: any) => r.status === 'approved').length,
    fulfilled: redemptions.filter((r: any) => r.status === 'fulfilled').length,
    rejected:  redemptions.filter((r: any) => r.status === 'rejected').length,
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Redemptions"
        description="Review and action employee reward redemption requests."
      />
      <RedemptionsClient
        redemptions={redemptions}
        counts={counts}
        selectedHotel={hotel}
        selectedStatus={status}
      />
    </div>
  );
}
