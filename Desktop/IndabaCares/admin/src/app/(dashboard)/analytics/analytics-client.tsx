'use client';

import { useRouter } from 'next/navigation';
import {
  LineChart, Line,
  BarChart, Bar,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Cell,
} from 'recharts';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { HOTELS } from '@/lib/hotels';

// ── Constants ──────────────────────────────────────────────────────────────────

const BADGE_COLORS: Record<string, string> = {
  teamwork:       '#CE21FB',
  innovation:     '#3b82f6',
  leadership:     '#f59e0b',
  customer_focus: '#22c55e',
  excellence:     '#8b5cf6',
  going_above:    '#ef4444',
};
const FALLBACK_COLOR = '#94a3b8';

const DEPT_PALETTE = [
  '#CE21FB', '#7c3aed', '#3b82f6', '#06b6d4',
  '#22c55e', '#f59e0b', '#ef4444', '#ec4899',
  '#8b5cf6', '#14b8a6', '#f97316', '#64748b',
];

const DAYS_OPTIONS = [
  { label: 'Last 7 days',  value: '7'  },
  { label: 'Last 30 days', value: '30' },
  { label: 'Last 90 days', value: '90' },
];

// ── Helpers ────────────────────────────────────────────────────────────────────

function formatDay(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

// ── Sub-components ─────────────────────────────────────────────────────────────

interface StatChipProps {
  label: string;
  value: string | number;
  sub?:  string;
  accent?: string;
}
function StatChip({ label, value, sub, accent }: StatChipProps) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</p>
        <p className={`mt-1 text-2xl font-bold ${accent ?? ''}`}>
          {typeof value === 'number' ? value.toLocaleString() : value}
        </p>
        {sub && <p className="text-xs text-muted-foreground">{sub}</p>}
      </CardContent>
    </Card>
  );
}

interface EngagementRingProps {
  rate:       number;
  active:     number;
  participants: number;
}
function EngagementRing({ rate, active, participants }: EngagementRingProps) {
  const r = 38;
  const circ = 2 * Math.PI * r;
  const dash = (rate / 100) * circ;

  return (
    <div className="flex items-center gap-5">
      <svg width={96} height={96} viewBox="0 0 96 96">
        <circle cx={48} cy={48} r={r} fill="none" stroke="#f1f5f9" strokeWidth={10} />
        <circle
          cx={48} cy={48} r={r}
          fill="none"
          stroke="#CE21FB"
          strokeWidth={10}
          strokeDasharray={`${dash} ${circ}`}
          strokeLinecap="round"
          transform="rotate(-90 48 48)"
        />
        <text x={48} y={44} textAnchor="middle" className="fill-foreground" fontSize={16} fontWeight={700}>{rate}%</text>
        <text x={48} y={60} textAnchor="middle" className="fill-muted-foreground" fontSize={9}>engaged</text>
      </svg>
      <div className="text-sm">
        <p className="font-semibold text-foreground">{participants.toLocaleString()} active</p>
        <p className="text-muted-foreground">out of {active.toLocaleString()} employees</p>
        <p className="text-muted-foreground">gave or received a recognition</p>
      </div>
    </div>
  );
}

// ── Props ──────────────────────────────────────────────────────────────────────

interface Props {
  dailyRecognitions:      { date: string; count: number }[];
  dailyPoints:            { date: string; points: number }[];
  badgeBreakdown:         { badge: string; count: number }[];
  topEmployees:           { id: string; full_name: string; hotel: string; points_balance: number; department: string | null }[];
  topPerformers:          { id: string; name: string; hotel: string; department: string | null; count: number }[];
  departmentParticipation:{ department: string; count: number }[];
  redemptionStats:        { total: number; pending: number; approved: number; rejected: number; fulfilled: number; pointsSpent: number };
  totalRecognitions:      number;
  totalPoints:            number;
  engagementRate:         number;
  totalActive:            number;
  participantCount:       number;
  selectedHotel?:         string;
  selectedDays:           number;
}

// ── Main component ─────────────────────────────────────────────────────────────

export function AnalyticsClient({
  dailyRecognitions,
  dailyPoints,
  badgeBreakdown,
  topEmployees,
  topPerformers,
  departmentParticipation,
  redemptionStats,
  totalRecognitions,
  totalPoints,
  engagementRate,
  totalActive,
  participantCount,
  selectedHotel,
  selectedDays,
}: Props) {
  const router = useRouter();

  function applyFilter(hotel?: string, days?: number) {
    const url = new URLSearchParams();
    if (hotel) url.set('hotel', hotel);
    if (days)  url.set('days', String(days));
    router.push(`/analytics${url.toString() ? '?' + url.toString() : ''}`);
  }

  return (
    <div className="space-y-8">

      {/* ── Filters ──────────────────────────────────────────────────────────── */}
      <div className="flex flex-wrap gap-3">
        <Select
          value={selectedHotel ?? 'all'}
          onValueChange={(v) => applyFilter(v === 'all' ? undefined : v, selectedDays)}
        >
          <SelectTrigger className="w-52">
            <SelectValue placeholder="All hotels" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Hotels</SelectItem>
            {HOTELS.map((h) => <SelectItem key={h} value={h}>{h}</SelectItem>)}
          </SelectContent>
        </Select>

        <Select
          value={String(selectedDays)}
          onValueChange={(v) => applyFilter(selectedHotel, Number(v))}
        >
          <SelectTrigger className="w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {DAYS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* ── KPI strip ────────────────────────────────────────────────────────── */}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatChip
          label="Recognitions"
          value={totalRecognitions}
          sub={`Last ${selectedDays} days`}
        />
        <StatChip
          label="Points Distributed"
          value={totalPoints}
          sub="Via recognition ledger"
        />
        <StatChip
          label="Redemptions"
          value={redemptionStats.total}
          sub={`${redemptionStats.pending} pending`}
        />
        <StatChip
          label="Points Spent"
          value={redemptionStats.pointsSpent}
          sub="On approved redemptions"
        />
      </div>

      {/* ── Engagement + Department participation ────────────────────────────── */}
      <div className="grid gap-6 lg:grid-cols-2">

        {/* Engagement rate */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Engagement Rate</CardTitle>
          </CardHeader>
          <CardContent>
            {totalActive === 0 ? (
              <p className="text-sm text-muted-foreground">No active employees found.</p>
            ) : (
              <EngagementRing
                rate={engagementRate}
                active={totalActive}
                participants={participantCount}
              />
            )}
          </CardContent>
        </Card>

        {/* Department participation */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Recognitions Sent by Department</CardTitle>
          </CardHeader>
          <CardContent>
            {departmentParticipation.length === 0 ? (
              <p className="text-sm text-muted-foreground">No data for this period.</p>
            ) : (
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={departmentParticipation} layout="vertical">
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" horizontal={false} />
                  <XAxis type="number" tick={{ fontSize: 10 }} allowDecimals={false} />
                  <YAxis
                    type="category"
                    dataKey="department"
                    width={120}
                    tick={{ fontSize: 10 }}
                    tickFormatter={(v: string) => v.length > 16 ? v.slice(0, 15) + '…' : v}
                  />
                  <Tooltip formatter={(v) => [v, 'Recognitions sent']} />
                  <Bar dataKey="count" radius={[0, 3, 3, 0]}>
                    {departmentParticipation.map((_, i) => (
                      <Cell key={i} fill={DEPT_PALETTE[i % DEPT_PALETTE.length]} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>
      </div>

      {/* ── Activity charts ───────────────────────────────────────────────────── */}
      <div className="grid gap-6 lg:grid-cols-2">

        {/* Daily recognitions */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Recognition Activity</CardTitle>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={dailyRecognitions}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis
                  dataKey="date"
                  tickFormatter={formatDay}
                  tick={{ fontSize: 10 }}
                  interval="preserveStartEnd"
                />
                <YAxis tick={{ fontSize: 10 }} allowDecimals={false} />
                <Tooltip
                  labelFormatter={(label) => formatDay(String(label))}
                  formatter={(v) => [v, 'Recognitions']}
                />
                <Line
                  type="monotone"
                  dataKey="count"
                  stroke="#CE21FB"
                  strokeWidth={2}
                  dot={false}
                  activeDot={{ r: 4 }}
                />
              </LineChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        {/* Points distribution */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Points Distribution</CardTitle>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={dailyPoints}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis
                  dataKey="date"
                  tickFormatter={formatDay}
                  tick={{ fontSize: 10 }}
                  interval="preserveStartEnd"
                />
                <YAxis tick={{ fontSize: 10 }} allowDecimals={false} />
                <Tooltip
                  labelFormatter={(label) => formatDay(String(label))}
                  formatter={(v) => [v, 'Points']}
                />
                <Bar dataKey="points" fill="#7c3aed" radius={[3, 3, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      {/* ── Badge breakdown + Top performers (period) ────────────────────────── */}
      <div className="grid gap-6 lg:grid-cols-2">

        {/* Badge breakdown */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Recognition Badges</CardTitle>
          </CardHeader>
          <CardContent>
            {badgeBreakdown.length === 0 ? (
              <p className="text-sm text-muted-foreground">No data for this period.</p>
            ) : (
              <ResponsiveContainer width="100%" height={240}>
                <BarChart data={badgeBreakdown} layout="vertical">
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" horizontal={false} />
                  <XAxis type="number" tick={{ fontSize: 10 }} allowDecimals={false} />
                  <YAxis
                    type="category"
                    dataKey="badge"
                    width={120}
                    tick={{ fontSize: 10 }}
                    tickFormatter={(v: string) => v.replace(/_/g, ' ')}
                  />
                  <Tooltip formatter={(v) => [v, 'Count']} />
                  <Bar dataKey="count" radius={[0, 3, 3, 0]}>
                    {badgeBreakdown.map((entry) => (
                      <Cell
                        key={entry.badge}
                        fill={BADGE_COLORS[entry.badge] ?? FALLBACK_COLOR}
                      />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>

        {/* Top performers — most recognitions received this period */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Top Performers — Last {selectedDays} Days
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-8">#</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead className="text-right">Received</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {topPerformers.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={3} className="text-center text-muted-foreground">
                      No recognitions in this period.
                    </TableCell>
                  </TableRow>
                )}
                {topPerformers.map((e, i) => (
                  <TableRow key={e.id}>
                    <TableCell className="font-bold text-muted-foreground">
                      {i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : i + 1}
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{e.name}</div>
                      <div className="text-xs text-muted-foreground">
                        {e.department ?? e.hotel}
                      </div>
                    </TableCell>
                    <TableCell className="text-right font-bold text-fuchsia-600">
                      {e.count}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

      {/* ── All-time leaderboard ──────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">All-Time Points Leaderboard</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-8">#</TableHead>
                <TableHead>Employee</TableHead>
                <TableHead>Department</TableHead>
                <TableHead className="text-right">Points Balance</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {topEmployees.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} className="text-center text-muted-foreground">
                    No data.
                  </TableCell>
                </TableRow>
              )}
              {topEmployees.map((e, i) => (
                <TableRow key={e.id}>
                  <TableCell className="font-bold text-muted-foreground">{i + 1}</TableCell>
                  <TableCell>
                    <div className="font-medium">{e.full_name}</div>
                    <div className="text-xs text-muted-foreground">{e.hotel}</div>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {e.department ?? '—'}
                  </TableCell>
                  <TableCell className="text-right font-bold text-violet-700">
                    {(e.points_balance ?? 0).toLocaleString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* ── Redemption breakdown ─────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Redemption Status Breakdown</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
            {(
              [
                { label: 'Total',     value: redemptionStats.total,     color: 'text-foreground'  },
                { label: 'Pending',   value: redemptionStats.pending,   color: 'text-amber-600'   },
                { label: 'Approved',  value: redemptionStats.approved,  color: 'text-blue-600'    },
                { label: 'Fulfilled', value: redemptionStats.fulfilled, color: 'text-emerald-600' },
                { label: 'Rejected',  value: redemptionStats.rejected,  color: 'text-red-600'     },
              ] as const
            ).map(({ label, value, color }) => (
              <div key={label} className="rounded-lg border p-3 text-center">
                <p className="text-xs text-muted-foreground">{label}</p>
                <p className={`text-2xl font-bold ${color}`}>{value}</p>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

    </div>
  );
}
