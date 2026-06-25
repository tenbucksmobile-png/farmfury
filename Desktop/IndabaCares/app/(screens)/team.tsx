import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, ScrollView, ActivityIndicator } from 'react-native';
import { Image } from 'expo-image';
import { Stack, router, useLocalSearchParams } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useDepartments } from '@/hooks/use-team';
import { useEmployee } from '@/providers/EmployeeContext';
import { indabaHotel, indabalodgeRichardsBay, indabalodgeGaborone, chobeSafariLodge, nataLodge } from '@/lib/localImages';
import { HOTELS, APA_HOTEL } from '@/lib/hotels';

const PURPLE = '#7B1FA2';

// ─── Department icon map ──────────────────────────────────────────────────────

const DEPT_ICONS: Record<string, { icon: keyof typeof import('@expo/vector-icons').Ionicons.glyphMap; color: string; bg: string }> = {
  'Front Office':     { icon: 'desktop-outline',      color: '#0277bd', bg: '#e1f5fe' },
  'Human Resources':  { icon: 'people-outline',        color: '#6a1b9a', bg: '#f3e5f5' },
  'Concierge':        { icon: 'key-outline',            color: '#e65100', bg: '#fff3e0' },
  'Food & Beverage':  { icon: 'restaurant-outline',    color: '#2e7d32', bg: '#e8f5e9' },
  'Events':           { icon: 'calendar-outline',      color: '#c62828', bg: '#ffebee' },
  'Housekeeping':     { icon: 'home-outline',          color: '#00695c', bg: '#e0f2f1' },
  'Operations':       { icon: 'settings-outline',      color: '#4527a0', bg: '#ede7f6' },
  'Guest Services':   { icon: 'star-outline',          color: '#f9a825', bg: '#fff8e1' },
  'Reservations':     { icon: 'book-outline',          color: '#1565c0', bg: '#e3f2fd' },
  'Spa & Wellness':   { icon: 'leaf-outline',          color: '#558b2f', bg: '#f1f8e9' },
};

const DEFAULT_ICON = { icon: 'briefcase-outline' as const, color: PURPLE, bg: '#ede9fe' };

const HOTEL_LOGOS: Record<string, string> = {
  'Indaba Hotel':              indabaHotel,
  'Indaba Lodge Richards Bay': indabalodgeRichardsBay,
  'Indaba Lodge Gaborone':     indabalodgeGaborone,
  'Chobe Safari Lodge':        chobeSafariLodge,
  'Nata Lodge':                nataLodge,
};

// ─── Hotel picker (APA only) ──────────────────────────────────────────────────

function HotelPicker() {
  return (
    <ScrollView contentContainerStyle={styles.body}>
      {HOTELS.map((hotel) => (
        <TouchableOpacity
          key={hotel}
          activeOpacity={0.75}
          style={styles.row}
          onPress={() =>
            router.push({
              pathname: '/(screens)/team',
              params: { hotel },
            })
          }
        >
          <View style={styles.iconWrap}>
            {HOTEL_LOGOS[hotel] ? (
              <Image source={{ uri: HOTEL_LOGOS[hotel] }} style={styles.hotelLogo} contentFit="contain" />
            ) : (
              <Ionicons name="business-outline" size={22} color={PURPLE} />
            )}
          </View>
          <Text style={styles.rowLabel}>{hotel}</Text>
          <Ionicons name="chevron-forward" size={18} color="#cbd5e1" />
        </TouchableOpacity>
      ))}
    </ScrollView>
  );
}

// ─── Department list ──────────────────────────────────────────────────────────

function DepartmentList({ hotel }: { hotel: string }) {
  const { data: departments = [], isLoading } = useDepartments(hotel);

  if (isLoading) {
    return <ActivityIndicator color={PURPLE} style={{ marginTop: 40 }} />;
  }

  if (departments.length === 0) {
    return (
      <View style={styles.empty}>
        <Ionicons name="people-outline" size={48} color="#cbd5e1" />
        <Text style={styles.emptyText}>No departments found</Text>
      </View>
    );
  }

  return (
    <ScrollView contentContainerStyle={styles.body}>
      {departments.map((dept) => {
        const cfg = DEPT_ICONS[dept] ?? DEFAULT_ICON;
        return (
          <TouchableOpacity
            key={dept}
            activeOpacity={0.75}
            style={styles.row}
            onPress={() =>
              router.push({
                pathname: '/(screens)/team/[department]',
                params: { department: encodeURIComponent(dept), hotel },
              })
            }
          >
            <View style={[styles.iconWrap, { backgroundColor: cfg.bg }]}>
              <Ionicons name={cfg.icon} size={22} color={cfg.color} />
            </View>
            <Text style={styles.rowLabel}>{dept}</Text>
            <Ionicons name="chevron-forward" size={18} color="#cbd5e1" />
          </TouchableOpacity>
        );
      })}
    </ScrollView>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function TeamListScreen() {
  const { employee } = useEmployee();
  const { hotel: hotelParam } = useLocalSearchParams<{ hotel?: string }>();

  const isAPA = employee?.hotel === APA_HOTEL;

  // APA with no hotel chosen → show picker
  // APA with hotel chosen → show that hotel's departments
  // Regular employee → always show their own hotel's departments
  const effectiveHotel = isAPA
    ? (hotelParam ?? '')
    : (employee?.hotel ?? '');

  const showPicker = isAPA && !hotelParam;

  const subtitle = showPicker
    ? 'Select a property to explore'
    : `Explore ${effectiveHotel || 'your hotel'} departments`;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      {/* Header */}
      <View style={styles.header}>
        <View style={styles.titleRow}>
          <TouchableOpacity onPress={() => router.back()} style={styles.backBtn} hitSlop={8}>
            <Ionicons name="arrow-back" size={22} color="#fff" />
          </TouchableOpacity>
          <View style={{ flex: 1 }}>
            <Text style={styles.title}>Know Your Team</Text>
            {!showPicker && effectiveHotel ? (
              <Text style={styles.hotelName}>{effectiveHotel}</Text>
            ) : null}
          </View>
          {/* APA viewing a specific hotel: quick-switch back to picker */}
          {isAPA && !showPicker ? (
            <TouchableOpacity
              onPress={() => router.push('/(screens)/team')}
              style={styles.switchBtn}
              hitSlop={8}
            >
              <Ionicons name="swap-horizontal-outline" size={20} color="#fff" />
            </TouchableOpacity>
          ) : (
            <View style={{ width: 38 }} />
          )}
        </View>
        <Text style={styles.subtitle}>{subtitle}</Text>
      </View>

      {showPicker ? <HotelPicker /> : <DepartmentList hotel={effectiveHotel} />}

    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#F2F2F2' },

  header: {
    backgroundColor: PURPLE,
    paddingHorizontal: 16,
    paddingTop: 14,
    paddingBottom: 24,
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 6,
  },
  backBtn: {
    width: 38,
    height: 38,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  switchBtn: {
    width: 38,
    height: 38,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: {
    textAlign: 'center',
    fontSize: 18,
    fontWeight: '700',
    color: '#fff',
  },
  hotelName: {
    textAlign: 'center',
    fontSize: 12,
    color: 'rgba(255,255,255,0.75)',
    fontWeight: '500',
    marginTop: 2,
  },
  subtitle: {
    textAlign: 'center',
    fontSize: 13,
    color: 'rgba(255,255,255,0.7)',
    marginTop: 2,
  },

  body: {
    paddingHorizontal: 16,
    paddingTop: 20,
    paddingBottom: 40,
    gap: 12,
  },

  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#ffffff',
    borderRadius: 16,
    paddingHorizontal: 16,
    paddingVertical: 14,
    gap: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 3,
  },
  iconWrap: {
    width: 44,
    height: 44,
    borderRadius: 12,
    backgroundColor: '#ede9fe',
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  hotelLogo: {
    width: 44,
    height: 44,
  },
  rowLabel: {
    flex: 1,
    fontSize: 16,
    fontWeight: '600',
    color: '#1e293b',
  },

  empty: {
    alignItems: 'center',
    paddingTop: 80,
    gap: 12,
  },
  emptyText: {
    fontSize: 15,
    color: '#94a3b8',
  },
});
