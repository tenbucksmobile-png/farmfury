import React from 'react';
import { View, Text, Pressable, StyleSheet, Dimensions } from 'react-native';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import type { Reward } from '@/api/reward-service';

const PURPLE = '#7B1FA2';

const PLACEHOLDER_COLORS = [
  '#7B1FA2',
  '#5B21B6',
  '#1D4ED8',
  '#0F766E',
  '#B45309',
  '#BE185D',
];

const { width: SCREEN_W } = Dimensions.get('window');
export const CARD_WIDTH  = (SCREEN_W - 16 * 2 - 14) / 2;
const        CARD_HEIGHT = CARD_WIDTH * 1.4;

interface RewardCardProps {
  reward: Reward;
  pointsBalance: number;
  onPress: () => void;
}

export function RewardCard({ reward, pointsBalance, onPress }: RewardCardProps) {
  const outOfStock = reward.stock <= 0;
  const canAfford  = pointsBalance >= reward.points_required;
  const lowStock   = !outOfStock && reward.stock <= 5;

  const placeholderColor =
    PLACEHOLDER_COLORS[parseInt(reward.id, 10) % PLACEHOLDER_COLORS.length] ?? PURPLE;

  return (
    <Pressable
      onPress={outOfStock ? undefined : onPress}
      style={({ pressed }) => [
        styles.card,
        outOfStock && styles.cardDisabled,
        pressed && styles.cardPressed,
      ]}
    >
      {/* ── Background ──────────────────────────────────────────────────── */}
      {reward.image_url ? (
        <Image
          source={{ uri: reward.image_url }}
          style={StyleSheet.absoluteFillObject}
          contentFit="cover"
        />
      ) : (
        <View
          style={[
            StyleSheet.absoluteFillObject,
            { backgroundColor: placeholderColor, alignItems: 'center', justifyContent: 'center' },
          ]}
        >
          <Ionicons name="gift-outline" size={52} color="rgba(255,255,255,0.3)" />
        </View>
      )}

      {/* ── Top scrim — darkens top so title is readable ────────────────── */}
      <LinearGradient
        colors={['rgba(0,0,0,0.72)', 'transparent']}
        style={styles.topScrim}
      />

      {/* ── Title — top of card ─────────────────────────────────────────── */}
      <View style={styles.titleBlock}>
        <Text style={styles.title} numberOfLines={2}>
          {reward.title}
        </Text>
      </View>

      {/* ── Top-right: Stock badge ──────────────────────────────────────── */}
      {outOfStock ? (
        <View style={[styles.badge, styles.badgeTopRight, styles.badgeDark]}>
          <Text style={styles.badgeLabelWhite}>OUT OF STOCK</Text>
        </View>
      ) : lowStock ? (
        <View style={[styles.badge, styles.badgeTopRight, styles.badgeAmber]}>
          <Ionicons name="flame" size={10} color="#92400e" />
          <Text style={[styles.badgeLabel, { color: '#92400e', marginLeft: 3 }]}>{reward.stock} left</Text>
        </View>
      ) : null}

      {/* ── Bottom-right: Points badge ──────────────────────────────────── */}
      <View style={[styles.pointsBadge, canAfford ? styles.pointsBadgeGreen : styles.pointsBadgeRed]}>
        <Ionicons name="cash-outline" size={22} color={canAfford ? '#16a34a' : '#db2777'} />
        <Text style={[styles.pointsLabel, { color: canAfford ? '#16a34a' : '#db2777' }]}>
          {reward.points_required}
        </Text>
      </View>

      {/* ── Bottom-left: Affordability indicator ───────────────────────── */}
      {!outOfStock && canAfford && (
        <View style={styles.tickBadge}>
          <Ionicons name="checkmark-circle" size={26} color="#86efac" />
          <Text style={styles.tickLabel}>Can redeem</Text>
        </View>
      )}
      {!outOfStock && !canAfford && (
        <View style={styles.tickBadge}>
          <Text style={styles.needLabel}>
            Need {reward.points_required - pointsBalance} pts
          </Text>
        </View>
      )}

    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    width: CARD_WIDTH,
    height: CARD_HEIGHT,
    marginBottom: 14,
    borderRadius: 18,
    overflow: 'hidden',
    backgroundColor: PURPLE,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.2,
    shadowRadius: 8,
    elevation: 5,
  },
  cardDisabled: { opacity: 0.5 },
  cardPressed:  { opacity: 0.82 },

  // Top gradient scrim
  topScrim: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    height: CARD_HEIGHT * 0.52,
  },

  // Title — top of card
  titleBlock: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    padding: 12,
  },
  title: {
    fontSize: 13,
    fontWeight: '700',
    color: '#ffffff',
    lineHeight: 18,
  },

  // Stock badge — top-right
  badge: {
    position: 'absolute',
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 20,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  badgeTopRight: { top: 10, right: 10 },
  badgeDark:  { backgroundColor: 'rgba(0,0,0,0.65)' },
  badgeAmber: { backgroundColor: 'rgba(254,243,199,0.95)' },
  badgeLabel: {
    fontSize: 11,
    fontWeight: '700',
  },
  badgeLabelWhite: {
    fontSize: 9,
    fontWeight: '700',
    letterSpacing: 0.4,
    color: '#ffffff',
  },

  // Points badge — bottom-right
  pointsBadge: {
    position: 'absolute',
    bottom: 10,
    right: 10,
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 14,
    paddingHorizontal: 10,
    paddingVertical: 6,
    gap: 5,
  },
  pointsBadgeGreen: { backgroundColor: 'rgba(220,252,231,0.95)' },
  pointsBadgeRed:   { backgroundColor: 'rgba(252,231,243,0.95)' },
  pointsLabel: {
    fontSize: 16,
    fontWeight: '800',
  },

  // Tick / need pts — bottom-left
  tickBadge: {
    position: 'absolute',
    bottom: 10,
    left: 10,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  tickLabel: {
    fontSize: 10,
    fontWeight: '600',
    color: '#86efac',
  },
  needLabel: {
    fontSize: 10,
    fontWeight: '500',
    color: 'rgba(255,255,255,0.6)',
  },
});
