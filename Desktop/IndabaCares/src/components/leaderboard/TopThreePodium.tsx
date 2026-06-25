import React from 'react';
import { View, Text, StyleSheet, Image } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useEmployee } from '@/providers/EmployeeContext';
import type { LeaderboardEntry } from '@/api/leaderboard-service';

const PURPLE      = '#7B1FA2';
const PURPLE_MID  = '#9C27B0';
const ACCENT      = '#CE21FB';
const BADGE_GOLD   = '#F5C518';
const BADGE_SILVER = '#A8A9AD';
const BADGE_BRONZE = '#CD7F32';

interface TopThreePodiumProps {
  entries: LeaderboardEntry[];
}

interface CardProps {
  entry?: LeaderboardEntry;
  rank: 1 | 2 | 3;
  isMe?: boolean;
}

// ─── Single card ──────────────────────────────────────────────────────────────

function PodiumCard({ entry, rank, isMe }: CardProps) {
  const isCenter        = rank === 1;
  const splashW         = isCenter ? 150 : 124;
  const splashH         = isCenter ? 168 : 140;
  const imgSize         = isCenter ? 100 : 82;
  const badgeColor      = rank === 1 ? BADGE_GOLD : rank === 2 ? BADGE_SILVER : BADGE_BRONZE;
  const containerH      = splashH + 16;
  const photoBottomTop  = containerH / 2 + imgSize / 2 + 4; // px from container top

  // Each rank gets a slightly different splash rotation for variety
  const rotations: Record<1 | 2 | 3, [number, number]> = {
    1: [22, -14],
    2: [18, -20],
    3: [28, -10],
  };
  const [r1, r2] = rotations[rank];

  return (
    <View style={[styles.card, isCenter && styles.cardCenter]}>

      {/* ── Image + splash area ─────────────────────────────────────────── */}
      <View style={{ width: splashW + 16, height: splashH + 16, alignItems: 'center', justifyContent: 'center' }}>

        {/* Splash layer 1 — accent blob (behind) */}
        <View
          style={[
            styles.splashAccent,
            {
              width: splashW * 0.88,
              height: splashH * 0.88,
              top: 0,
              right: 2,
              transform: [{ rotate: `${r2}deg` }],
            },
          ]}
        />

        {/* Splash layer 2 — deep purple base blob (on top) */}
        <View
          style={[
            styles.splashBase,
            {
              width: splashW,
              height: splashH,
              transform: [{ rotate: `${r1}deg` }],
            },
          ]}
        />

        {/* Photo — background-removed PNG preferred, raw avatar fallback */}
        {(entry?.podium_photo_url ?? entry?.avatar_url) ? (
          <Image
            source={{ uri: (entry.podium_photo_url ?? entry.avatar_url)! }}
            style={[styles.photo, { width: imgSize, height: imgSize }]}
            resizeMode="cover"
          />
        ) : (
          <View style={[styles.photoPlaceholder, { width: imgSize, height: imgSize }]}>
            <Ionicons name="person" size={imgSize * 0.45} color="rgba(255,255,255,0.6)" />
          </View>
        )}

        {/* Rank number — touches left edge of splash */}
        <View style={styles.rankBadge}>
          <Text style={[styles.rankText, { fontSize: isCenter ? 22 : 18, color: badgeColor }]}>{rank}</Text>
        </View>

        {/* Points — top-right, nudged inward */}
        {entry && (
          <View style={styles.pointsPill}>
            <Text style={styles.pointsStar}>⭐</Text>
            <Text style={styles.pointsNum}>{entry.total_points}</Text>
          </View>
        )}

        {/* Name — directly under photo, inside container */}
        <Text
          style={[styles.nameOverlay, { fontSize: 14, width: splashW - 8, top: photoBottomTop }]}
          numberOfLines={1}
          adjustsFontSizeToFit
          minimumFontScale={0.6}
        >
          {entry?.full_name ?? '—'}
        </Text>

      </View>

    </View>
  );
}

// ─── Podium ───────────────────────────────────────────────────────────────────

export function TopThreePodium({ entries }: TopThreePodiumProps) {
  const { employee } = useEmployee();
  const isMe = (e?: LeaderboardEntry) => !!e && e.employee_id === employee?.employee_id;

  return (
    <View style={styles.container}>

      {/* Rank 1 — centred and largest */}
      <View style={styles.topRow}>
        <PodiumCard entry={entries[0]} rank={1} isMe={isMe(entries[0])} />
      </View>

      {/* Ranks 2 & 3 */}
      <View style={styles.bottomRow}>
        <PodiumCard entry={entries[1]} rank={2} isMe={isMe(entries[1])} />
        <PodiumCard entry={entries[2]} rank={3} isMe={isMe(entries[2])} />
      </View>

    </View>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: 12,
    paddingTop: 6,
    paddingBottom: 0,
  },

  topRow: {
    alignItems: 'center',
    marginBottom: 0,
  },

  bottomRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: 4,
  },

  card: {
    alignItems: 'center',
    width: 140,  },

  cardCenter: {
    width: 160,
  },

  // ── Splash layers ────────────────────────────────────────────────────────────

  splashBase: {
    position: 'absolute',
    backgroundColor: PURPLE,
    borderTopLeftRadius: 72,
    borderTopRightRadius: 20,
    borderBottomRightRadius: 72,
    borderBottomLeftRadius: 20,
    opacity: 0.95,
  },

  splashAccent: {
    position: 'absolute',
    backgroundColor: ACCENT,
    borderTopLeftRadius: 20,
    borderTopRightRadius: 60,
    borderBottomRightRadius: 20,
    borderBottomLeftRadius: 60,
    opacity: 0.65,
  },

  // ── Photo ────────────────────────────────────────────────────────────────────

  photo: {
    borderRadius: 12,
    zIndex: 3,
  },

  photoPlaceholder: {
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 3,
  },

  // ── Name overlay ─────────────────────────────────────────────────────────────

  nameOverlay: {
    position: 'absolute',
    textAlign: 'center',
    fontWeight: '700',
    color: '#000000',
    letterSpacing: 0.2,
    zIndex: 4,
  },

  // ── Rank badge ───────────────────────────────────────────────────────────────

  rankBadge: {
    position: 'absolute',
    top: 6,
    left: -4,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 5,
  },

  rankText: {
    fontWeight: '900',
    lineHeight: 24,
    color: '#1e1b4b',
  },

  // ── Points pill ──────────────────────────────────────────────────────────────

  pointsPill: {
    position: 'absolute',
    top: 6,
    right: 2,         // nudged to right edge
    flexDirection: 'row',
    alignItems: 'center',
    zIndex: 5,
    gap: 3,
  },
  pointsStar: { fontSize: 18 },
  pointsNum:  { fontSize: 13, fontWeight: '800', color: '#000000' },

  // ── Text ─────────────────────────────────────────────────────────────────────

  name: {
    color: '#1e293b',
    fontWeight: '700',
    fontSize: 13,
    textAlign: 'center',
    marginTop: 10,
    lineHeight: 17,
  },

  nameLarge: {
    fontSize: 15,
    lineHeight: 21,
  },

  jobTitle: {
    color: '#94a3b8',
    fontSize: 11,
    textAlign: 'center',
    marginTop: 3,
  },

  jobTitleLarge: {
    fontSize: 12,
  },
});
