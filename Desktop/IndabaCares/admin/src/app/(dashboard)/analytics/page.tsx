import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { AnalyticsClient } from './analytics-client';

export const dynamic = 'force-dynamic';

async function getAnalytics(hotel?: string, days = 30) {
  const db    = createAdminClient();
  const since = new Date(Date.now() - days * 86_400_000).toISOString();

  // ── Queries ────────────────────────────────────────────────────────────────

  let recognitionsQ = db
    .from('recognitions')
    .select('id, badge, hotel, sender_id, receiver_id, created_at')
    .gte('created_at', since)
    .order('created_at');

  let ledgerQ = db
    .from('points_ledger')
    .select('points, source, hotel, created_at')
    .gte('created_at', since);

  // All active employees — used for engagement rate denominator + dept lookup
  let activeEmpQ = db
    .from('employees')
    .select('id, full_name, hotel, points_balance, department')
    .eq('status', 'active');

  let redemptionsQ = db
    .from('redemptions')
    .select('id, status, hotel, points_used, created_at')
    .gte('created_at', since);

  if (hotel) {
    recognitionsQ = recognitionsQ.eq('hotel', hotel);
    ledgerQ       = ledgerQ.eq('hotel', hotel);
    activeEmpQ    = activeEmpQ.eq('hotel', hotel);
    redemptionsQ  = redemptionsQ.eq('hotel', hotel);
  }

  const [
    { data: recognitions },
    { data: ledger },
    { data: activeEmployees },
    { data: redemptions },
  ] = await Promise.all([recognitionsQ, ledgerQ, activeEmpQ, redemptionsQ]);

  // ── Employee lookup map ────────────────────────────────────────────────────

  const empLookup = new Map<string, {
    full_name:   string;
    hotel:       string;
    department:  string | null;
    points_balance: number;
  }>();
  for (const e of activeEmployees ?? []) {
    empLookup.set(e.id, {
      full_name:      e.full_name,
      hotel:          e.hotel,
      department:     e.department,
      points_balance: e.points_balance ?? 0,
    });
  }

  // ── Daily recognition buckets ──────────────────────────────────────────────

  const dayMap = new Map<string, number>();
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(Date.now() - i * 86_400_000);
    dayMap.set(d.toISOString().slice(0, 10), 0);
  }
  for (const r of recognitions ?? []) {
    const day = (r.created_at as string).slice(0, 10);
    if (dayMap.has(day)) dayMap.set(day, (dayMap.get(day) ?? 0) + 1);
  }
  const dailyRecognitions = Array.from(dayMap, ([date, count]) => ({ date, count }));

  // ── Points per day ─────────────────────────────────────────────────────────

  const ptsDayMap = new Map<string, number>();
  for (const [k] of dayMap) ptsDayMap.set(k, 0);
  for (const row of ledger ?? []) {
    const day = (row.created_at as string).slice(0, 10);
    if (ptsDayMap.has(day)) ptsDayMap.set(day, (ptsDayMap.get(day) ?? 0) + (row.points ?? 0));
  }
  const dailyPoints = Array.from(ptsDayMap, ([date, points]) => ({ date, points }));

  // ── Badge breakdown ────────────────────────────────────────────────────────

  const badgeMap: Record<string, number> = {};
  for (const r of recognitions ?? []) {
    const b = (r.badge as string) ?? 'unknown';
    badgeMap[b] = (badgeMap[b] ?? 0) + 1;
  }
  const badgeBreakdown = Object.entries(badgeMap)
    .map(([badge, count]) => ({ badge, count }))
    .sort((a, b) => b.count - a.count);

  // ── Engagement rate ────────────────────────────────────────────────────────

  const participantIds = new Set<string>();
  for (const r of recognitions ?? []) {
    if (r.sender_id)   participantIds.add(r.sender_id);
    if (r.receiver_id) participantIds.add(r.receiver_id);
  }
  const totalActive     = (activeEmployees ?? []).length;
  const participantCount = participantIds.size;
  const engagementRate  = totalActive > 0
    ? Math.round((participantCount / totalActive) * 100)
    : 0;

  // ── Department participation ───────────────────────────────────────────────

  const deptMap: Record<string, number> = {};
  for (const r of recognitions ?? []) {
    const dept = empLookup.get(r.sender_id ?? '')?.department ?? 'Unknown';
    deptMap[dept] = (deptMap[dept] ?? 0) + 1;
  }
  const departmentParticipation = Object.entries(deptMap)
    .map(([department, count]) => ({ department, count }))
    .sort((a, b) => b.count - a.count)
    .slice(0, 12);

  // ── Top performers by period (most recognitions received) ──────────────────

  const perfMap: Record<string, {
    name:       string;
    hotel:      string;
    department: string | null;
    count:      number;
  }> = {};
  for (const r of recognitions ?? []) {
    if (!r.receiver_id) continue;
    const emp = empLookup.get(r.receiver_id);
    if (!perfMap[r.receiver_id]) {
      perfMap[r.receiver_id] = {
        name:       emp?.full_name ?? 'Unknown',
        hotel:      emp?.hotel     ?? '',
        department: emp?.department ?? null,
        count:      0,
      };
    }
    perfMap[r.receiver_id].count++;
  }
  const topPerformers = Object.entries(perfMap)
    .map(([id, v]) => ({ id, ...v }))
    .sort((a, b) => b.count - a.count)
    .slice(0, 10);

  // ── All-time leaderboard (highest points_balance) ──────────────────────────

  const topEmployees = [...(activeEmployees ?? [])]
    .sort((a, b) => (b.points_balance ?? 0) - (a.points_balance ?? 0))
    .slice(0, 10);

  // ── Redemption stats ───────────────────────────────────────────────────────

  const redemptionStats = {
    total:      (redemptions ?? []).length,
    pending:    (redemptions ?? []).filter((r: any) => r.status === 'pending').length,
    approved:   (redemptions ?? []).filter((r: any) => r.status === 'approved').length,
    rejected:   (redemptions ?? []).filter((r: any) => r.status === 'rejected').length,
    fulfilled:  (redemptions ?? []).filter((r: any) => r.status === 'fulfilled').length,
    pointsSpent:(redemptions ?? [])
      .filter((r: any) => r.status !== 'rejected')
      .reduce((sum: number, r: any) => sum + (r.points_used ?? 0), 0),
  };

  return {
    dailyRecognitions,
    dailyPoints,
    badgeBreakdown,
    topEmployees,
    topPerformers,
    departmentParticipation,
    redemptionStats,
    totalRecognitions: (recognitions ?? []).length,
    totalPoints: (ledger ?? []).reduce((s: number, r: any) => s + (r.points ?? 0), 0),
    engagementRate,
    totalActive,
    participantCount,
  };
}

export default async function AnalyticsPage({
  searchParams,
}: {
  searchParams: Promise<{ hotel?: string; days?: string }>;
}) {
  const { hotel, days: daysStr } = await searchParams;
  const days = Number(daysStr ?? '30');
  const analytics = await getAnalytics(hotel, days);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Analytics"
        description="Recognition activity, engagement rates, and top performers."
      />
      <AnalyticsClient
        {...analytics}
        selectedHotel={hotel}
        selectedDays={days}
      />
    </div>
  );
}
