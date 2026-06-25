import React, { useState } from 'react';
import {
  View,
  Text,
  FlatList,
  ScrollView,
  RefreshControl,
  ActivityIndicator,
  StyleSheet,
  Platform,
  TouchableOpacity,
  SafeAreaView as RNSafeAreaView,
} from 'react-native';
import { Image } from 'expo-image';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useLeaderboard } from '@/hooks/use-leaderboard';
import { useEmployee } from '@/providers/EmployeeContext';
import { HOTELS, APA_HOTEL } from '@/lib/hotels';
import { COLORS } from '@/lib/constants';
import { indabaHotel, indabalodgeRichardsBay, indabalodgeGaborone, chobeSafariLodge, nataLodge } from '@/lib/localImages';
import { type PeriodType } from '@/api/leaderboard-service';
import { LeaderboardRow } from '@/components/leaderboard/LeaderboardRow';
import { TopThreePodium } from '@/components/leaderboard/TopThreePodium';
import { PeriodTabs } from '@/components/leaderboard/PeriodTabs';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import type { LeaderboardEntry } from '@/api/leaderboard-service';
import { useMonthlyLegends } from '@/hooks/use-legends';
import type { MonthlyLegend } from '@/api/legends-service';

const PURPLE = '#7B1FA2';

const MONTH_NAMES = [
  'January','February','March','April','May','June',
  'July','August','September','October','November','December',
];

// ─── Leaderboard skeleton (PERF-08) ──────────────────────────────────────────

function LeaderboardSkeleton() {
  return (
    <View style={{ paddingHorizontal: 16, paddingTop: 8, gap: 2 }}>
      {Array.from({ length: 8 }).map((_, i) => (
        <View
          key={i}
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            backgroundColor: '#fff',
            paddingVertical: 12,
            paddingHorizontal: 14,
            gap: 12,
            borderTopLeftRadius:     i === 0 ? 16 : 0,
            borderTopRightRadius:    i === 0 ? 16 : 0,
            borderBottomLeftRadius:  i === 7 ? 16 : 0,
            borderBottomRightRadius: i === 7 ? 16 : 0,
          }}
        >
          <Skeleton width={34} height={32} borderRadius={4} />
          <Skeleton width={32} height={32} borderRadius={16} />
          <View style={{ flex: 1, gap: 6 }}>
            <Skeleton width="55%" height={13} />
            <Skeleton width="35%" height={11} />
          </View>
          <Skeleton width={40} height={14} borderRadius={4} />
        </View>
      ))}
    </View>
  );
}

// ─── Legends tab ──────────────────────────────────────────────────────────────

function LegendsTab() {
  const currentYear = new Date().getFullYear();
  const currentMonth = new Date().getMonth() + 1; // 1-based
  const [year, setYear] = useState(currentYear);
  const { data: legends = [], isLoading } = useMonthlyLegends(year);

  // Build a month→legend lookup
  const legendMap = Object.fromEntries(legends.map((l) => [l.month, l]));

  return (
    <ScrollView contentContainerStyle={legendStyles.body} showsVerticalScrollIndicator={false}>

      {/* Year selector */}
      <View style={legendStyles.yearRow}>
        <TouchableOpacity onPress={() => setYear((y) => y - 1)} hitSlop={12}>
          <Ionicons name="chevron-back" size={20} color={PURPLE} />
        </TouchableOpacity>
        <Text style={legendStyles.yearText}>{year}</Text>
        <TouchableOpacity
          onPress={() => setYear((y) => y + 1)}
          disabled={year >= currentYear}
          hitSlop={12}
        >
          <Ionicons name="chevron-forward" size={20} color={year >= currentYear ? '#cbd5e1' : PURPLE} />
        </TouchableOpacity>
      </View>

      {isLoading ? (
        <ActivityIndicator color={PURPLE} style={{ marginTop: 40 }} />
      ) : (
        MONTH_NAMES.map((name, idx) => {
          const month = idx + 1;
          const legend: MonthlyLegend | undefined = legendMap[month];
          const isFuture  = year === currentYear && month > currentMonth;
          const isCurrent = year === currentYear && month === currentMonth;

          return (
            <View key={month} style={legendStyles.card}>
              {/* Month label */}
              <View style={legendStyles.monthBadge}>
                <Text style={legendStyles.monthText}>{name}</Text>
                {isCurrent && <View style={legendStyles.activeDot} />}
              </View>

              {legend ? (
                /* Winner row */
                <View style={legendStyles.winnerRow}>
                  <View style={legendStyles.avatarWrap}>
                    {legend.avatar_url ? (
                      <Image
                        source={{ uri: legend.avatar_url }}
                        style={legendStyles.avatar}
                        contentFit="cover"
                      />
                    ) : (
                      <View style={legendStyles.avatarPlaceholder}>
                        <Text style={legendStyles.avatarInitial}>
                          {legend.full_name.charAt(0).toUpperCase()}
                        </Text>
                      </View>
                    )}
                    <View style={legendStyles.crownBadge}>
                      <Text style={{ fontSize: 10 }}>👑</Text>
                    </View>
                  </View>

                  <View style={{ flex: 1 }}>
                    <Text style={legendStyles.winnerName}>{legend.full_name}</Text>
                    {legend.job_title ? (
                      <Text style={legendStyles.winnerTitle}>{legend.job_title}</Text>
                    ) : null}
                    <View style={legendStyles.ptsBadge}>
                      <Ionicons name="star" size={11} color="#fbbf24" />
                      <Text style={legendStyles.ptsText}>{legend.total_points.toLocaleString()} pts</Text>
                    </View>
                  </View>

                  <View style={legendStyles.bonusBadge}>
                    <Text style={legendStyles.bonusText}>+{legend.points_awarded}</Text>
                    <Text style={legendStyles.bonusLabel}>bonus</Text>
                  </View>
                </View>
              ) : isFuture ? (
                <Text style={legendStyles.placeholderText}>Not yet</Text>
              ) : isCurrent ? (
                <Text style={legendStyles.inProgressText}>In progress…</Text>
              ) : (
                <Text style={legendStyles.placeholderText}>No data</Text>
              )}
            </View>
          );
        })
      )}
    </ScrollView>
  );
}

// ─── My rank strip ────────────────────────────────────────────────────────────

function MyRankStrip({ entries }: { entries: LeaderboardEntry[] }) {
  const { employee } = useEmployee();
  if (!employee) return null;
  const myEntry = entries.find((e) => e.employee_id === employee.employee_id);
  if (!myEntry || myEntry.rank <= 3) return null;

  return (
    <View style={styles.myRankStrip}>
      <Text style={styles.myRankLabel}>Your rank</Text>
      <Text style={styles.myRankValue}>#{myEntry.rank}</Text>
      <View style={{ flex: 1 }} />
      <Text style={styles.myRankPoints}>{myEntry.total_points.toLocaleString()} pts</Text>
    </View>
  );
}

// ─── APA Hotel Picker ─────────────────────────────────────────────────────────

const HOTEL_LOGOS: Record<string, string> = {
  'Indaba Hotel':              indabaHotel,
  'Indaba Lodge Richards Bay': indabalodgeRichardsBay,
  'Indaba Lodge Gaborone':     indabalodgeGaborone,
  'Chobe Safari Lodge':        chobeSafariLodge,
  'Nata Lodge':                nataLodge,
};

const HOTEL_ICON: Record<string, keyof typeof Ionicons.glyphMap> = {
  'African Procurement Agencies': 'briefcase-outline',
};

function HotelPickerView({ onSelect }: { onSelect: (hotel: string) => void }) {
  return (
    <ScrollView contentContainerStyle={pickerStyles.body}>
      <Text style={pickerStyles.hint}>Select a property to view its leaderboard</Text>
      {HOTELS.filter((h) => h !== APA_HOTEL).map((hotel) => (
        <TouchableOpacity
          key={hotel}
          activeOpacity={0.75}
          style={pickerStyles.row}
          onPress={() => onSelect(hotel)}
        >
          <View style={pickerStyles.iconWrap}>
            {HOTEL_LOGOS[hotel] ? (
              <Image source={{ uri: HOTEL_LOGOS[hotel] }} style={pickerStyles.hotelLogo} contentFit="contain" />
            ) : (
              <Ionicons name={HOTEL_ICON[hotel] ?? 'business-outline'} size={22} color={PURPLE} />
            )}
          </View>
          <Text style={pickerStyles.label}>{hotel}</Text>
          <Ionicons name="chevron-forward" size={18} color="#cbd5e1" />
        </TouchableOpacity>
      ))}
    </ScrollView>
  );
}

const pickerStyles = StyleSheet.create({
  body: { paddingHorizontal: 16, paddingTop: 16, paddingBottom: 40, gap: 12 },
  hint: { fontSize: 13, color: '#94a3b8', textAlign: 'center', marginBottom: 4 },
  row: {
    flexDirection: 'row', alignItems: 'center', backgroundColor: '#ffffff',
    borderRadius: 16, paddingHorizontal: 16, paddingVertical: 14, gap: 14,
    shadowColor: '#000', shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06, shadowRadius: 6, elevation: 3,
  },
  iconWrap: {
    width: 44, height: 44, borderRadius: 12, backgroundColor: '#ede9fe',
    alignItems: 'center', justifyContent: 'center', overflow: 'hidden',
  },
  hotelLogo: { width: 44, height: 44 },
  label: { flex: 1, fontSize: 15, fontWeight: '600', color: '#1e293b' },
});

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function LeaderboardScreen() {
  const { employee } = useEmployee();
  const isAPA = employee?.hotel === APA_HOTEL;

  const [selectedHotel, setSelectedHotel] = useState<string | null>(
    isAPA ? null : (employee?.hotel ?? ''),
  );
  const [period, setPeriod] = useState<PeriodType>('monthly');

  const hotel = selectedHotel ?? '';
  const { data: liveEntries = [], isLoading, refetch, isRefetching } = useLeaderboard(hotel, 'monthly');

  const allEntries  = liveEntries;

  // Podium always shows top 3 employees only
  const employees   = allEntries.filter((e) => !e.is_manager);
  const management  = allEntries.filter((e) => e.is_manager);
  const topThree    = employees.slice(0, 3) as LeaderboardEntry[];

  // List shows the active tab (employees skip top 3 since they're in podium)
  const isManagement = period === 'annual';
  const isLegends    = period === 'quarterly';
  const listEntries  = isManagement ? management : isLegends ? allEntries : employees.slice(3);
  const rest         = listEntries as LeaderboardEntry[];

  // APA with no hotel — show picker
  if (isAPA && !selectedHotel) {
    return (
      <SafeAreaView style={[styles.safeArea, { backgroundColor: COLORS.primary }]} edges={['top']}>
        <View style={styles.screen}>
          <View style={styles.pickerHeader}>
            <Text style={styles.pickerTitle}>Leaderboard</Text>
            <Text style={styles.pickerSubtitle}>Choose a property</Text>
          </View>
          <HotelPickerView onSelect={setSelectedHotel} />
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safeArea} edges={['top']}>
      <View style={styles.screen}>

      {/* APA hotel strip */}
      {isAPA && selectedHotel && (
        <View style={styles.hotelStrip}>
          <Ionicons name="business-outline" size={14} color={PURPLE} />
          <Text style={styles.hotelStripText}>{selectedHotel}</Text>
          <TouchableOpacity onPress={() => setSelectedHotel(null)} hitSlop={8}>
            <Ionicons name="swap-horizontal-outline" size={18} color={PURPLE} />
          </TouchableOpacity>
        </View>
      )}

      {/* ── Frozen purple header ─────────────────────────────── */}
      <View style={styles.header}>
        {isLoading ? (
          <View style={styles.loadingInner}>
            <ActivityIndicator size="large" color="rgba(255,255,255,0.8)" />
          </View>
        ) : (
          <TopThreePodium entries={topThree} />
        )}
      </View>

      {/* ── Period pill tabs (below header) ──────────────────── */}
      <PeriodTabs value={period} onChange={setPeriod} />

      {/* ── Legends tab — monthly winners grid ───────────────── */}
      {isLegends ? (
        <LegendsTab />
      ) : (
        /* ── Employees / Management ranked list ──────────────── */
        <FlatList
          style={styles.list}
          data={isLoading ? [] : rest}
          keyExtractor={(item) => item.employee_id}
          renderItem={({ item, index }) => (
            <LeaderboardRow
              entry={item}
              isFirst={index === 0}
              isLast={index === rest.length - 1}
            />
          )}
          ItemSeparatorComponent={() => <View style={styles.separator} />}
          showsVerticalScrollIndicator={false}
          contentContainerStyle={styles.listContent}
          refreshControl={
            <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={PURPLE} />
          }
          ListHeaderComponent={
            <>
              {!isLoading && !isManagement && employees.length > 0 && <MyRankStrip entries={employees} />}
            </>
          }
          ListEmptyComponent={
            isLoading ? (
              <LeaderboardSkeleton />
            ) : (
              <EmptyState
                icon="🏆"
                title="No rankings yet"
                description="Start recognizing colleagues to earn points and appear here!"
              />
            )
          }
          windowSize={5}
          maxToRenderPerBatch={10}
          initialNumToRender={12}
          removeClippedSubviews
          getItemLayout={(_, index) => ({ length: 57, offset: 57 * index, index })}
        />
      )}
      </View>
    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safeArea: {
    flex: 1,
    backgroundColor: '#ffffff',
  },

  screen: {
    flex: 1,
    backgroundColor: '#F2F2F2',
  },

  list: {
    flex: 1,
    backgroundColor: '#F2F2F2',
  },

  listContent: {
    paddingBottom: 100,
  },

  // Matches profile header exactly — always rendered, always tall
  header: {
    backgroundColor: '#ffffff',
    borderBottomLeftRadius: 30,
    borderBottomRightRadius: 30,
    paddingHorizontal: 20,
    paddingTop: Platform.OS === 'android' ? 12 : 8,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.07,
    shadowRadius: 10,
    elevation: 6,
  },

  loadingInner: {
    height: 280,
    alignItems: 'center',
    justifyContent: 'center',
  },

  myRankStrip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#EDE9FE',
    marginHorizontal: 20,
    marginTop: 14,
    marginBottom: 4,
    borderRadius: 14,
    paddingVertical: 10,
    paddingHorizontal: 14,
    borderWidth: 1,
    borderColor: '#DDD6FE',
  },

  myRankLabel: {
    fontSize: 12,
    color: PURPLE,
    fontWeight: '600',
    marginRight: 6,
  },

  myRankValue: {
    fontSize: 14,
    color: '#5B21B6',
    fontWeight: '800',
  },

  myRankPoints: {
    fontSize: 13,
    color: PURPLE,
    fontWeight: '700',
  },

  separator: {
    height: 1,
    backgroundColor: '#F1F5F9',
    marginHorizontal: 16,
  },

  hotelStrip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 20,
    paddingVertical: 8,
    backgroundColor: '#ede9fe',
  },
  hotelStripText: {
    flex: 1,
    fontSize: 13,
    fontWeight: '600',
    color: PURPLE,
  },

  dividerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginHorizontal: 20,
    marginTop: 12,
    marginBottom: 10,
  },

  dividerLine: {
    flex: 1,
    height: 1,
    backgroundColor: '#E5E7EB',
  },

  dividerText: {
    marginHorizontal: 10,
    fontSize: 11,
    fontWeight: '600',
    color: '#9CA3AF',
  },

  pickerHeader: {
    backgroundColor: COLORS.primary,
    paddingHorizontal: 20,
    paddingTop: 16,
    paddingBottom: 24,
    alignItems: 'center',
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
  },
  pickerTitle: {
    fontSize: 20,
    fontWeight: '800',
    color: '#fff',
  },
  pickerSubtitle: {
    fontSize: 13,
    color: 'rgba(255,255,255,0.7)',
    marginTop: 4,
  },
});

// ─── Legends styles ───────────────────────────────────────────────────────────

const legendStyles = StyleSheet.create({
  body: {
    paddingHorizontal: 16,
    paddingTop: 8,
    paddingBottom: 100,
    gap: 10,
  },

  yearRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 20,
    paddingVertical: 6,
  },
  yearText: {
    fontSize: 16,
    fontWeight: '700',
    color: '#1e293b',
    minWidth: 50,
    textAlign: 'center',
  },

  card: {
    backgroundColor: '#ffffff',
    borderRadius: 16,
    paddingHorizontal: 14,
    paddingVertical: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 3,
  },

  monthBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginBottom: 8,
  },
  monthText: {
    fontSize: 11,
    fontWeight: '700',
    color: PURPLE,
    textTransform: 'uppercase',
    letterSpacing: 0.6,
  },
  activeDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: '#22c55e',
  },

  winnerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },

  avatarWrap: {
    width: 48,
    height: 48,
    position: 'relative',
  },
  avatar: {
    width: 48,
    height: 48,
    borderRadius: 24,
  },
  avatarPlaceholder: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: '#ede9fe',
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarInitial: {
    fontSize: 20,
    fontWeight: '700',
    color: PURPLE,
  },
  crownBadge: {
    position: 'absolute',
    bottom: -2,
    right: -2,
    width: 20,
    height: 20,
    borderRadius: 10,
    backgroundColor: '#fef3c7',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1.5,
    borderColor: '#ffffff',
  },

  winnerName: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1e293b',
  },
  winnerTitle: {
    fontSize: 12,
    color: '#64748b',
    marginTop: 1,
  },
  ptsBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 3,
    marginTop: 4,
  },
  ptsText: {
    fontSize: 11,
    fontWeight: '600',
    color: '#64748b',
  },

  bonusBadge: {
    alignItems: 'center',
    backgroundColor: '#f0fdf4',
    borderRadius: 10,
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderWidth: 1,
    borderColor: '#bbf7d0',
  },
  bonusText: {
    fontSize: 13,
    fontWeight: '800',
    color: '#16a34a',
  },
  bonusLabel: {
    fontSize: 10,
    color: '#16a34a',
    fontWeight: '600',
  },

  placeholderText: {
    fontSize: 13,
    color: '#cbd5e1',
    fontStyle: 'italic',
  },
  inProgressText: {
    fontSize: 13,
    color: '#f59e0b',
    fontWeight: '600',
  },
});
