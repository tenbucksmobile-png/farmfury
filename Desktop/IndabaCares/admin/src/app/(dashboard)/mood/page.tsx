'use client';

import { useState, useMemo } from 'react';
import { useMoodAnalytics } from '@/hooks/use-mood';
import { useDepartments } from '@/hooks/use-departments';
import { PageHeader } from '@/components/layout/page-header';
import { StatCard } from '@/components/charts/stat-card';
import { DateRangePicker } from '@/components/charts/date-range-picker';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import { SmilePlus, Users, TrendingUp, BarChart3, Download } from 'lucide-react';
import {
  LineChart, Line, BarChart, Bar, XAxis, YAxis,
  CartesianGrid, Tooltip, ResponsiveContainer,
  PieChart, Pie, Cell, Legend,
} from 'recharts';
import type { PieLabelRenderProps } from 'recharts';
import { formatNumber } from '@/lib/utils';
import { MOOD_MAP } from '@/lib/constants';
import { exportMoodEntries } from '@/api/export';
import { format, subDays } from 'date-fns';
import { cn } from '@/lib/utils';

// ─── Sentiment grouping ───────────────────────────────────────────────────────

const NEGATIVE_MOODS = new Set(['awful', 'bad']);
const NEUTRAL_MOODS  = new Set(['okay']);
const POSITIVE_MOODS = new Set(['good', 'amazing']);

function getHotelMoodLabel(positivePct: number, negativePct: number) {
  if (positivePct >= 65) return { label: 'Very Positive',      emoji: '🌟', bg: 'bg-emerald-500', text: 'text-white' };
  if (positivePct >= 45) return { label: 'Generally Positive', emoji: '😊', bg: 'bg-green-500',   text: 'text-white' };
  if (negativePct >= 40) return { label: 'Needs Attention',    emoji: '⚠️', bg: 'bg-red-500',     text: 'text-white' };
  if (negativePct >= 25) return { label: 'Mixed Sentiment',    emoji: '😕', bg: 'bg-orange-400',  text: 'text-white' };
  return                        { label: 'Neutral',             emoji: '😐', bg: 'bg-yellow-400',  text: 'text-gray-900' };
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function MoodPage() {
  const [dateFrom, setDateFrom] = useState(format(subDays(new Date(), 30), 'yyyy-MM-dd'));
  const [dateTo,   setDateTo]   = useState(format(new Date(), 'yyyy-MM-dd'));

  const { data, isLoading } = useMoodAnalytics(dateFrom, dateTo);
  const { data: departments } = useDepartments();

  const stats = useMemo(() => {
    if (!data) return null;
    const { happinessScores, moodEntries, activeUserCount } = data;

    // ── Overall happiness ───────────────────────────────────────────────────
    const companyScores = happinessScores.filter((s) => s.department_id === null);
    const avgHappiness  = companyScores.length > 0
      ? Math.round(companyScores.reduce((sum, s) => sum + s.happiness_score, 0) / companyScores.length * 10) / 10
      : 0;

    // ── Participation ───────────────────────────────────────────────────────
    const uniqueParticipants = new Set(moodEntries.map((e) => e.user_id)).size;
    const participationRate  = activeUserCount > 0
      ? Math.round((uniqueParticipants / activeUserCount) * 100)
      : 0;

    // ── Consolidated sentiment buckets ──────────────────────────────────────
    let negativeCount = 0;
    let neutralCount  = 0;
    let positiveCount = 0;
    const moodCount: Record<string, number> = {};

    moodEntries.forEach((e) => {
      moodCount[e.mood] = (moodCount[e.mood] ?? 0) + 1;
      if (NEGATIVE_MOODS.has(e.mood)) negativeCount++;
      else if (NEUTRAL_MOODS.has(e.mood)) neutralCount++;
      else if (POSITIVE_MOODS.has(e.mood)) positiveCount++;
    });

    const total = moodEntries.length || 1;
    const negativePct = Math.round((negativeCount / total) * 100);
    const neutralPct  = Math.round((neutralCount  / total) * 100);
    const positivePct = Math.round((positiveCount / total) * 100);

    const hotelMood = getHotelMoodLabel(positivePct, negativePct);

    // ── Mood breakdown (individual 5 moods) ─────────────────────────────────
    const moodData = (['amazing', 'good', 'okay', 'bad', 'awful'] as const).map((mood) => ({
      name:  MOOD_MAP[mood]?.label ?? mood,
      value: moodCount[mood] ?? 0,
      color: MOOD_MAP[mood]?.color ?? '#94a3b8',
    })).filter((m) => m.value > 0);

    // ── Happiness trend ─────────────────────────────────────────────────────
    const trendData = companyScores.map((s) => ({
      date:  s.entry_date,
      score: s.happiness_score,
      count: s.submission_count,
    }));

    // ── By department ───────────────────────────────────────────────────────
    const deptScores = happinessScores.filter((s) => s.department_id !== null);
    const deptMap: Record<string, { total: number; count: number }> = {};
    deptScores.forEach((s) => {
      if (!deptMap[s.department_id!]) deptMap[s.department_id!] = { total: 0, count: 0 };
      deptMap[s.department_id!].total += s.happiness_score;
      deptMap[s.department_id!].count++;
    });
    const deptData = Object.entries(deptMap).map(([deptId, { total, count }]) => {
      const dept = (departments as any[])?.find((d) => d.id === deptId);
      return { name: dept?.name ?? 'Unknown', score: Math.round((total / count) * 10) / 10 };
    }).sort((a, b) => b.score - a.score);

    return {
      avgHappiness, totalSubmissions: moodEntries.length,
      participationRate, uniqueParticipants,
      negativeCount, neutralCount, positiveCount,
      negativePct, neutralPct, positivePct,
      hotelMood,
      trendData, deptData, moodData,
    };
  }, [data, departments]);

  return (
    <div>
      <PageHeader
        title="Mood Board"
        description="Daily employee sentiment — consolidated hotel mood overview"
        actions={
          <>
            <DateRangePicker dateFrom={dateFrom} dateTo={dateTo} onDateFromChange={setDateFrom} onDateToChange={setDateTo} />
            <Button variant="outline" size="sm" onClick={() => data && exportMoodEntries(data.moodEntries)} disabled={!data}>
              <Download className="mr-2 h-4 w-4" />Export
            </Button>
          </>
        }
      />

      {isLoading ? (
        <div className="space-y-4">
          <Skeleton className="h-28 w-full rounded-2xl" />
          <div className="grid gap-4 md:grid-cols-3"><Skeleton className="h-28" /><Skeleton className="h-28" /><Skeleton className="h-28" /></div>
          <div className="grid gap-4 md:grid-cols-4">{[1,2,3,4].map(i => <Skeleton key={i} className="h-24" />)}</div>
        </div>
      ) : stats && (
        <>
          {/* ── Hotel Mood Banner ───────────────────────────────────────────── */}
          <div className={cn('mb-6 flex items-center justify-between rounded-2xl px-6 py-5', stats.hotelMood.bg)}>
            <div>
              <p className={cn('text-sm font-semibold uppercase tracking-widest opacity-80', stats.hotelMood.text)}>
                Hotel Mood
              </p>
              <p className={cn('mt-1 text-3xl font-black', stats.hotelMood.text)}>
                {stats.hotelMood.emoji}  {stats.hotelMood.label}
              </p>
              <p className={cn('mt-1 text-sm opacity-75', stats.hotelMood.text)}>
                Based on {formatNumber(stats.totalSubmissions)} submissions from {stats.uniqueParticipants} employees
              </p>
            </div>
            <div className={cn('text-right', stats.hotelMood.text)}>
              <p className="text-5xl font-black">{stats.avgHappiness}</p>
              <p className="text-sm font-semibold opacity-75">/ 100 avg score</p>
            </div>
          </div>

          {/* ── Consolidated Sentiment Cards ─────────────────────────────────── */}
          <div className="mb-6 grid gap-4 md:grid-cols-3">
            {/* Negative */}
            <Card className="border-red-200 bg-red-50 dark:bg-red-950/20">
              <CardContent className="pt-5">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-semibold text-red-600 dark:text-red-400">😞 Negative</p>
                    <p className="text-xs text-muted-foreground">Awful + Bad</p>
                  </div>
                  <p className="text-4xl font-black text-red-600 dark:text-red-400">{stats.negativePct}%</p>
                </div>
                <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-red-200 dark:bg-red-900">
                  <div className="h-full rounded-full bg-red-500 transition-all" style={{ width: `${stats.negativePct}%` }} />
                </div>
                <p className="mt-2 text-xs text-muted-foreground">{stats.negativeCount} submissions</p>
              </CardContent>
            </Card>

            {/* Neutral */}
            <Card className="border-yellow-200 bg-yellow-50 dark:bg-yellow-950/20">
              <CardContent className="pt-5">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-semibold text-yellow-600 dark:text-yellow-400">😐 Neutral</p>
                    <p className="text-xs text-muted-foreground">Okay</p>
                  </div>
                  <p className="text-4xl font-black text-yellow-600 dark:text-yellow-400">{stats.neutralPct}%</p>
                </div>
                <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-yellow-200 dark:bg-yellow-900">
                  <div className="h-full rounded-full bg-yellow-400 transition-all" style={{ width: `${stats.neutralPct}%` }} />
                </div>
                <p className="mt-2 text-xs text-muted-foreground">{stats.neutralCount} submissions</p>
              </CardContent>
            </Card>

            {/* Positive */}
            <Card className="border-green-200 bg-green-50 dark:bg-green-950/20">
              <CardContent className="pt-5">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-semibold text-green-600 dark:text-green-400">😊 Positive</p>
                    <p className="text-xs text-muted-foreground">Good + Amazing</p>
                  </div>
                  <p className="text-4xl font-black text-green-600 dark:text-green-400">{stats.positivePct}%</p>
                </div>
                <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-green-200 dark:bg-green-900">
                  <div className="h-full rounded-full bg-green-500 transition-all" style={{ width: `${stats.positivePct}%` }} />
                </div>
                <p className="mt-2 text-xs text-muted-foreground">{stats.positiveCount} submissions</p>
              </CardContent>
            </Card>
          </div>

          {/* ── KPI Strip ────────────────────────────────────────────────────── */}
          <div className="mb-6 grid gap-4 md:grid-cols-4">
            <StatCard title="Happiness Score"    value={`${stats.avgHappiness}/100`}              icon={SmilePlus}  />
            <StatCard title="Participation Rate" value={`${stats.participationRate}%`}             icon={Users}      description={`${stats.uniqueParticipants} employees`} />
            <StatCard title="Total Submissions"  value={formatNumber(stats.totalSubmissions)}      icon={TrendingUp} />
            <StatCard title="Departments"        value={stats.deptData.length}                     icon={BarChart3}  />
          </div>

          {/* ── Mood Breakdown — all 5 moods as a stacked bar ────────────────── */}
          <Card className="mb-6">
            <CardHeader><CardTitle className="text-base">Mood Breakdown</CardTitle></CardHeader>
            <CardContent>
              {/* Stacked horizontal bar */}
              <div className="mb-3 flex h-8 w-full overflow-hidden rounded-full">
                {(['amazing', 'good', 'okay', 'bad', 'awful'] as const).map((mood) => {
                  const count = stats.moodData.find(m => m.name === MOOD_MAP[mood]?.label)?.value ?? 0;
                  const pct   = stats.totalSubmissions > 0 ? (count / stats.totalSubmissions) * 100 : 0;
                  if (pct === 0) return null;
                  return (
                    <div
                      key={mood}
                      title={`${MOOD_MAP[mood]?.label}: ${Math.round(pct)}%`}
                      style={{ width: `${pct}%`, backgroundColor: MOOD_MAP[mood]?.color }}
                    />
                  );
                })}
              </div>
              {/* Legend */}
              <div className="flex flex-wrap gap-4">
                {stats.moodData.map((m) => (
                  <div key={m.name} className="flex items-center gap-1.5">
                    <div className="h-3 w-3 rounded-full" style={{ backgroundColor: m.color }} />
                    <span className="text-xs text-muted-foreground">{m.name} ({m.value})</span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          {/* ── Charts row ───────────────────────────────────────────────────── */}
          <div className="mb-6 grid gap-6 md:grid-cols-2">
            <Card>
              <CardHeader><CardTitle className="text-base">Happiness Trend</CardTitle></CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={260}>
                  <LineChart data={stats.trendData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                    <YAxis domain={[0, 100]} />
                    <Tooltip />
                    <Line type="monotone" dataKey="score" stroke="#22c55e" strokeWidth={2} dot={false} name="Score" />
                  </LineChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle className="text-base">Mood Distribution</CardTitle></CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={260}>
                  <PieChart>
                    <Pie
                      data={stats.moodData}
                      cx="50%" cy="50%"
                      outerRadius={90}
                      dataKey="value"
                      nameKey="name"
                      label={(props: PieLabelRenderProps) =>
                        `${props.name ?? ''} ${(((props.percent as number) ?? 0) * 100).toFixed(0)}%`
                      }
                    >
                      {stats.moodData.map((entry, i) => <Cell key={i} fill={entry.color} />)}
                    </Pie>
                    <Tooltip />
                    <Legend />
                  </PieChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          </div>

          {/* ── Happiness by Department ──────────────────────────────────────── */}
          {stats.deptData.length > 0 && (
            <Card>
              <CardHeader><CardTitle className="text-base">Happiness by Department</CardTitle></CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={Math.max(200, stats.deptData.length * 44)}>
                  <BarChart data={stats.deptData} layout="vertical">
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis type="number" domain={[0, 100]} tickFormatter={(v) => `${v}`} />
                    <YAxis type="category" dataKey="name" width={130} tick={{ fontSize: 12 }} />
                    <Tooltip formatter={(v) => [`${v} / 100`, 'Happiness']} />
                    <Bar dataKey="score" radius={[0, 4, 4, 0]} name="Happiness Score">
                      {stats.deptData.map((entry, i) => (
                        <Cell
                          key={i}
                          fill={entry.score >= 65 ? '#22c55e' : entry.score >= 45 ? '#84cc16' : entry.score >= 30 ? '#eab308' : '#ef4444'}
                        />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
