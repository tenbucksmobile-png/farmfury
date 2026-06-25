import React, { memo } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import type { MonthlyLegend } from '@/hooks/use-legend-of-month';

const MONTH_NAMES = [
  '', 'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

interface Props {
  legend: MonthlyLegend;
}

export const LegendCard = memo(function LegendCard({ legend }: Props) {
  const monthLabel = `${MONTH_NAMES[legend.month]} ${legend.year}`;

  return (
    <LinearGradient
      colors={['#1a0a2e', '#3b0764', '#1a0a2e']}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={s.card}
    >
      {/* ── Header ────────────────────────────────────────── */}
      <View style={s.header}>
        <Text style={s.crown}>👑</Text>
        <View style={s.headerText}>
          <Text style={s.title} numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.85}>Legend of the Month</Text>
          <Text style={s.month} numberOfLines={1}>{monthLabel}</Text>
        </View>
        <View style={s.trophyWrap}>
          <Ionicons name="trophy" size={22} color="#fbbf24" />
        </View>
      </View>

      <View style={s.divider} />

      {/* ── Winner row ────────────────────────────────────── */}
      <View style={s.winnerRow}>
        <Avatar
          name={legend.full_name}
          uri={legend.avatar_url ?? undefined}
          size="lg"
        />
        <View style={s.winnerInfo}>
          <Text style={s.winnerName} numberOfLines={1}>{legend.full_name}</Text>
          {legend.job_title ? (
            <Text style={s.winnerTitle} numberOfLines={1}>{legend.job_title}</Text>
          ) : null}
        </View>
      </View>

      {/* ── Stats row ─────────────────────────────────────── */}
      <View style={s.statsRow}>
        <View style={s.stat}>
          <Text style={s.statValue}>{legend.total_points}</Text>
          <Text style={s.statLabel}>{'Recognition\nReceived'}</Text>
        </View>
        <View style={s.statDivider} />
        <View style={s.stat}>
          <Text style={[s.statValue, { color: '#fbbf24' }]}>#1</Text>
          <Text style={s.statLabel}>{'Hotel\nRank'}</Text>
        </View>
        <View style={s.statDivider} />
        <View style={s.stat}>
          <Text style={s.statValue}>{legend.points_awarded}</Text>
          <Text style={s.statLabel}>{'Reward\nWallet'}</Text>
        </View>
      </View>

      {/* ── Gold status badge ─────────────────────────────── */}
      <View style={s.badge}>
        <Ionicons name="star" size={13} color="#fbbf24" />
        <Text style={s.badgeText} numberOfLines={1}>Gold Status — Top Performer</Text>
      </View>
    </LinearGradient>
  );
});

const s = StyleSheet.create({
  card: {
    borderRadius: 20,
    marginBottom: 12,
    paddingHorizontal: 16,
    paddingTop: 16,
    paddingBottom: 16,
    borderWidth: 1,
    borderColor: 'rgba(251,191,36,0.25)',
    shadowColor: '#7B1FA2',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 12,
    elevation: 8,
    overflow: 'hidden',
  },

  // Header
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 12,
    gap: 10,
  },
  crown: {
    fontSize: 28,
  },
  headerText: {
    flex: 1,
    alignItems: 'center',
  },
  title: {
    fontSize: 19,
    fontWeight: '800',
    color: '#fbbf24',
    letterSpacing: 0.3,
    textAlign: 'center',
  },
  month: {
    fontSize: 12,
    color: 'rgba(255,255,255,0.6)',
    marginTop: 2,
    fontWeight: '700',
    textAlign: 'center',
  },
  trophyWrap: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(251,191,36,0.12)',
    borderWidth: 1,
    borderColor: 'rgba(251,191,36,0.25)',
    alignItems: 'center',
    justifyContent: 'center',
  },

  divider: {
    height: 1,
    backgroundColor: 'rgba(255,255,255,0.08)',
    marginBottom: 12,
  },

  // Winner row
  winnerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    backgroundColor: 'rgba(255,255,255,0.06)',
    borderRadius: 14,
    paddingHorizontal: 12,
    paddingVertical: 10,
    marginBottom: 14,
  },
  winnerInfo: {
    flex: 1,
  },
  winnerName: {
    fontSize: 16,
    fontWeight: '800',
    color: '#ffffff',
  },
  winnerTitle: {
    fontSize: 12,
    color: 'rgba(255,255,255,0.5)',
    marginTop: 2,
    fontWeight: '500',
  },

  // Stats
  statsRow: {
    flexDirection: 'row',
    marginBottom: 14,
  },
  stat: {
    flex: 1,
    alignItems: 'center',
  },
  statValue: {
    fontSize: 24,
    fontWeight: '800',
    color: '#ffffff',
    marginBottom: 4,
  },
  statLabel: {
    fontSize: 10,
    color: 'rgba(255,255,255,0.45)',
    textAlign: 'center',
    lineHeight: 14,
    fontWeight: '500',
    textTransform: 'uppercase',
    letterSpacing: 0.4,
  },
  statDivider: {
    width: 1,
    backgroundColor: 'rgba(255,255,255,0.08)',
    alignSelf: 'stretch',
  },

  // Badge
  badge: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    backgroundColor: 'rgba(251,191,36,0.1)',
    borderRadius: 20,
    paddingVertical: 8,
    paddingHorizontal: 16,
    borderWidth: 1,
    borderColor: 'rgba(251,191,36,0.25)',
  },
  badgeText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#fbbf24',
    letterSpacing: 0.3,
  },
});
