import React from 'react';
import {
  View,
  Text,
  FlatList,
  RefreshControl,
  ActivityIndicator,
  Pressable,
  StyleSheet,
} from 'react-native';
import { Stack, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { SafeAreaView } from 'react-native-safe-area-context';
import { LinearGradient } from 'expo-linear-gradient';
import { useRedemptions } from '@/hooks/use-redemptions';
import { EmptyState } from '@/components/ui/EmptyState';
import { REDEMPTION_STATUS } from '@/lib/constants';
import { formatDate } from '@/utils/format';
import type { Redemption, RedemptionStatus } from '@/api/reward-service';

const PURPLE      = '#7B1FA2';
const PURPLE_SOFT = '#ede9fe';
const PURPLE_MID  = '#ddd6fe';

// ─── Status chip ──────────────────────────────────────────────────────────────

function StatusChip({ status }: { status: RedemptionStatus }) {
  const config = REDEMPTION_STATUS[status] ?? {
    label: status,
    color: '#94a3b8',
    icon:  'ellipse-outline',
  };

  return (
    <View style={[styles.chip, { backgroundColor: config.color + '20' }]}>
      <Ionicons name={config.icon as any} size={12} color={config.color} />
      <Text style={[styles.chipLabel, { color: config.color }]}>{config.label}</Text>
    </View>
  );
}

// ─── Order card ───────────────────────────────────────────────────────────────

function OrderCard({ item }: { item: Redemption }) {
  const reward = item.reward;

  return (
    <View style={styles.card}>
      {/* Main row */}
      <View style={styles.cardRow}>
        {/* Thumbnail */}
        {reward.image_url ? (
          <Image
            source={{ uri: reward.image_url }}
            style={styles.thumbnail}
            contentFit="cover"
          />
        ) : (
          <LinearGradient
            colors={[PURPLE_SOFT, PURPLE_MID]}
            style={styles.thumbnailPlaceholder}
          >
            <Ionicons name="gift-outline" size={24} color={PURPLE} style={{ opacity: 0.6 }} />
          </LinearGradient>
        )}

        {/* Details */}
        <View style={styles.cardDetails}>
          <View style={styles.cardTitleRow}>
            <Text style={styles.cardTitle} numberOfLines={2}>{reward.title}</Text>
            <StatusChip status={item.status} />
          </View>

          <View style={styles.pointsRow}>
            <Ionicons name="star" size={13} color={PURPLE} />
            <Text style={styles.pointsText}>{item.points_used} pts used</Text>
          </View>

          <Text style={styles.dateText}>Submitted {formatDate(item.created_at)}</Text>
        </View>
      </View>

      {/* Timeline strip */}
      {(item.approved_at || item.rejected_at || item.fulfilled_at) && (
        <View style={styles.timeline}>
          {item.approved_at && (
            <View style={styles.timelineRow}>
              <Ionicons name="checkmark-circle" size={13} color="#3b82f6" />
              <Text style={styles.timelineText}>Approved on {formatDate(item.approved_at)}</Text>
            </View>
          )}
          {item.fulfilled_at && (
            <View style={[styles.timelineRow, { marginTop: 4 }]}>
              <Ionicons name="gift" size={13} color="#22c55e" />
              <Text style={styles.timelineText}>Fulfilled on {formatDate(item.fulfilled_at)}</Text>
            </View>
          )}
          {item.rejected_at && (
            <View style={[styles.timelineRow, { marginTop: 4 }]}>
              <Ionicons name="close-circle" size={13} color="#ef4444" />
              <Text style={styles.timelineText}>Rejected on {formatDate(item.rejected_at)}</Text>
            </View>
          )}
        </View>
      )}

      {/* Rejection reason */}
      {item.status === 'rejected' && item.rejection_reason && (
        <View style={styles.rejectionBox}>
          <Text style={styles.rejectionLabel}>Reason</Text>
          <Text style={styles.rejectionReason}>{item.rejection_reason}</Text>
          <Text style={styles.rejectionRefund}>
            Your {item.points_used} points have been refunded.
          </Text>
        </View>
      )}
    </View>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function OrdersScreen() {
  const { data: orders = [], isLoading, refetch, isRefetching } = useRedemptions();

  if (isLoading) {
    return (
      <SafeAreaView style={styles.loadingContainer} edges={['top']}>
        <ActivityIndicator color="#ffffff" size="large" />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safeArea} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />
      <View style={styles.screen}>
        <FlatList
          data={orders as Redemption[]}
          keyExtractor={(item) => item.id}
          contentContainerStyle={styles.listContent}
          renderItem={({ item }) => <OrderCard item={item as Redemption} />}
          ListHeaderComponent={
            <View>
              {/* Purple header */}
              <View style={styles.header}>
                <Pressable
                  onPress={() => router.replace('/(tabs)/rewards' as any)}
                  style={styles.backBtn}
                  hitSlop={12}
                >
                  <Ionicons name="chevron-back" size={22} color="#ffffff" />
                </Pressable>
                <View style={styles.headerText}>
                  <Text style={styles.headerTitle}>My Orders</Text>
                  <Text style={styles.headerSub}>
                    {orders.length} redemption{orders.length !== 1 ? 's' : ''}
                  </Text>
                </View>
              </View>
              {/* White sheet */}
              <View style={styles.sheet}>
                <View style={styles.sheetHandle} />
              </View>
            </View>
          }
          ListEmptyComponent={
            <EmptyState
              icon="🛍️"
              title="No orders yet"
              description="Redeem rewards with your points to see them here."
            />
          }
          refreshControl={
            <RefreshControl
              refreshing={isRefetching}
              onRefresh={refetch}
              tintColor={PURPLE}
            />
          }
        />
      </View>
    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safeArea: {
    flex: 1,
    backgroundColor: PURPLE,
  },
  loadingContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: PURPLE,
  },
  screen: {
    flex: 1,
    backgroundColor: '#ffffff',
  },
  listContent: {
    paddingBottom: 110,
  },

  // Header
  header: {
    backgroundColor: PURPLE,
    paddingHorizontal: 20,
    paddingTop: 10,
    paddingBottom: 32,
    flexDirection: 'row',
    alignItems: 'center',
  },
  backBtn: {
    width: 36,
    height: 36,
    borderRadius: 10,
    backgroundColor: 'rgba(255,255,255,0.18)',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 12,
  },
  headerText: {
    flex: 1,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: '#ffffff',
  },
  headerSub: {
    fontSize: 12,
    color: 'rgba(255,255,255,0.65)',
    marginTop: 1,
  },

  // White sheet
  sheet: {
    backgroundColor: '#ffffff',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    marginTop: -20,
    paddingTop: 12,
    paddingBottom: 4,
  },
  sheetHandle: {
    width: 36,
    height: 4,
    borderRadius: 2,
    backgroundColor: PURPLE_MID,
    alignSelf: 'center',
    marginBottom: 16,
  },

  // Status chip
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 20,
    paddingHorizontal: 9,
    paddingVertical: 4,
    gap: 4,
  },
  chipLabel: {
    fontSize: 11,
    fontWeight: '700',
  },

  // Order card
  card: {
    marginHorizontal: 16,
    marginBottom: 14,
    borderRadius: 18,
    backgroundColor: '#ffffff',
    shadowColor: PURPLE,
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.09,
    shadowRadius: 10,
    elevation: 3,
    overflow: 'hidden',
  },
  cardRow: {
    flexDirection: 'row',
    padding: 14,
  },
  thumbnail: {
    width: 64,
    height: 64,
    borderRadius: 12,
  },
  thumbnailPlaceholder: {
    width: 64,
    height: 64,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  cardDetails: {
    flex: 1,
    marginLeft: 12,
  },
  cardTitleRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
  },
  cardTitle: {
    flex: 1,
    paddingRight: 8,
    fontSize: 13,
    fontWeight: '700',
    color: '#1e1b4b',
    lineHeight: 18,
  },
  pointsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 6,
    gap: 4,
  },
  pointsText: {
    fontSize: 12,
    fontWeight: '600',
    color: PURPLE,
  },
  dateText: {
    marginTop: 4,
    fontSize: 11,
    color: '#94a3b8',
  },

  // Timeline
  timeline: {
    borderTopWidth: 1,
    borderTopColor: '#f1f5f9',
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  timelineRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  timelineText: {
    fontSize: 11,
    color: '#64748b',
  },

  // Rejection box
  rejectionBox: {
    marginHorizontal: 14,
    marginBottom: 12,
    borderRadius: 12,
    backgroundColor: '#fff1f2',
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  rejectionLabel: {
    fontSize: 11,
    fontWeight: '700',
    color: '#e11d48',
  },
  rejectionReason: {
    marginTop: 2,
    fontSize: 12,
    color: '#f43f5e',
  },
  rejectionRefund: {
    marginTop: 6,
    fontSize: 10,
    color: '#fb7185',
  },
});
