import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Users, Award, Gift, TrendingUp } from 'lucide-react';
import Link from 'next/link';

async function getDashboardStats() {
  const db = createAdminClient();

  const [
    { count: totalEmployees },
    { count: recognitionsToday },
    { count: pendingRedemptions },
    { data: pointsData },
  ] = await Promise.all([
    db.from('employees').select('*', { count: 'exact', head: true }).eq('status', 'active'),
    db.from('recognitions').select('*', { count: 'exact', head: true })
      .gte('created_at', new Date(new Date().setHours(0, 0, 0, 0)).toISOString()),
    db.from('redemptions').select('*', { count: 'exact', head: true }).eq('status', 'pending'),
    db.from('points_ledger').select('points').gte(
      'created_at',
      new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString(),
    ),
  ]);

  const pointsThisMonth = (pointsData ?? []).reduce(
    (sum: number, row: { points: number }) => sum + (row.points ?? 0),
    0,
  );

  return {
    totalEmployees:    totalEmployees ?? 0,
    recognitionsToday: recognitionsToday ?? 0,
    pendingRedemptions: pendingRedemptions ?? 0,
    pointsThisMonth,
  };
}

async function getRecentRecognitions() {
  const db = createAdminClient();
  const { data } = await db
    .from('recognitions')
    .select(`
      id, message, badge, hotel, created_at,
      sender:employees!sender_id   ( full_name ),
      receiver:employees!receiver_id ( full_name )
    `)
    .order('created_at', { ascending: false })
    .limit(5);
  return data ?? [];
}

async function getPendingRedemptionsSummary() {
  const db = createAdminClient();
  const { data } = await db
    .from('redemptions')
    .select(`
      id, points_used, hotel, created_at,
      employee:employees!employee_id ( full_name ),
      reward:rewards!reward_id ( title )
    `)
    .eq('status', 'pending')
    .order('created_at', { ascending: true })
    .limit(5);
  return data ?? [];
}

// ─── Stat card ────────────────────────────────────────────────────────────────

function StatCard({
  title,
  value,
  icon: Icon,
  sub,
  href,
  color,
}: {
  title: string;
  value: string | number;
  icon:  React.ElementType;
  sub?:  string;
  href?: string;
  color: string;
}) {
  const content = (
    <Card className="relative overflow-hidden transition-shadow hover:shadow-md">
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">
          {title}
        </CardTitle>
        <div className={`rounded-lg p-2 ${color}`}>
          <Icon className="h-4 w-4 text-white" />
        </div>
      </CardHeader>
      <CardContent>
        <div className="text-3xl font-bold">{value.toLocaleString()}</div>
        {sub && <p className="mt-1 text-xs text-muted-foreground">{sub}</p>}
      </CardContent>
    </Card>
  );

  return href ? <Link href={href}>{content}</Link> : content;
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default async function DashboardPage() {
  const [stats, recentRecognitions, pendingRedemptions] = await Promise.all([
    getDashboardStats(),
    getRecentRecognitions(),
    getPendingRedemptionsSummary(),
  ]);

  return (
    <div className="space-y-8">
      <PageHeader
        title="Dashboard"
        description="Overview of IndabaCares activity across all hotels"
      />

      {/* KPI row */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          title="Active Employees"
          value={stats.totalEmployees}
          icon={Users}
          sub="Across all hotels"
          href="/employees"
          color="bg-violet-600"
        />
        <StatCard
          title="Recognitions Today"
          value={stats.recognitionsToday}
          icon={Award}
          sub="Sent in the last 24 h"
          color="bg-fuchsia-500"
        />
        <StatCard
          title="Pending Redemptions"
          value={stats.pendingRedemptions}
          icon={Gift}
          sub="Awaiting approval"
          href="/redemptions"
          color={stats.pendingRedemptions > 0 ? 'bg-amber-500' : 'bg-emerald-500'}
        />
        <StatCard
          title="Points This Month"
          value={stats.pointsThisMonth}
          icon={TrendingUp}
          sub="Distributed via ledger"
          href="/analytics"
          color="bg-blue-600"
        />
      </div>

      {/* Bottom panels */}
      <div className="grid gap-6 lg:grid-cols-2">
        {/* Recent recognitions */}
        <Card>
          <CardHeader className="border-b pb-3">
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">Recent Recognitions</CardTitle>
              <Link
                href="/analytics"
                className="text-xs text-muted-foreground hover:text-foreground"
              >
                View all →
              </Link>
            </div>
          </CardHeader>
          <CardContent className="pt-4">
            {recentRecognitions.length === 0 ? (
              <p className="text-sm text-muted-foreground">No recognitions yet.</p>
            ) : (
              <ul className="space-y-3">
                {recentRecognitions.map((r: any) => (
                  <li key={r.id} className="flex items-start gap-3">
                    <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-fuchsia-100 text-xs font-bold text-fuchsia-700">
                      {r.sender?.full_name?.charAt(0) ?? '?'}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">
                        {r.sender?.full_name ?? '—'}{' '}
                        <span className="font-normal text-muted-foreground">→</span>{' '}
                        {r.receiver?.full_name ?? '—'}
                      </p>
                      <p className="truncate text-xs text-muted-foreground">
                        {r.badge} · {r.hotel}
                      </p>
                    </div>
                    <span className="shrink-0 text-[10px] text-muted-foreground">
                      {new Date(r.created_at).toLocaleDateString()}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        {/* Pending redemptions */}
        <Card>
          <CardHeader className="border-b pb-3">
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">Pending Redemptions</CardTitle>
              <Link
                href="/redemptions"
                className="text-xs text-muted-foreground hover:text-foreground"
              >
                Manage →
              </Link>
            </div>
          </CardHeader>
          <CardContent className="pt-4">
            {pendingRedemptions.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No pending redemptions. 🎉
              </p>
            ) : (
              <ul className="space-y-3">
                {pendingRedemptions.map((r: any) => (
                  <li key={r.id} className="flex items-center gap-3">
                    <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-amber-100 text-xs font-bold text-amber-700">
                      {r.employee?.full_name?.charAt(0) ?? '?'}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">
                        {r.employee?.full_name ?? '—'}
                      </p>
                      <p className="truncate text-xs text-muted-foreground">
                        {r.reward?.title ?? '—'} · {r.points_used} pts
                      </p>
                    </div>
                    <span className="shrink-0 rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold text-amber-700">
                      Pending
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
