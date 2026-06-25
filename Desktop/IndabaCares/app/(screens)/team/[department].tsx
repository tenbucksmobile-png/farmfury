import React from 'react';
import {
  View, Text, TouchableOpacity, StyleSheet,
  FlatList, ActivityIndicator, Dimensions,
} from 'react-native';
import { Image } from 'expo-image';
import { Stack, router, useLocalSearchParams } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useEmployee } from '@/providers/EmployeeContext';
import { useTeamByDepartment } from '@/hooks/use-team';
import { APA_HOTEL } from '@/lib/hotels';
import type { TeamMember } from '@/api/team-service';

const PURPLE     = '#7B1FA2';
const COLUMNS    = 2;
const H_PADDING  = 16;
const CARD_GAP   = 10;
const CARD_WIDTH = (Dimensions.get('window').width - H_PADDING * 2 - CARD_GAP) / COLUMNS;

// ─── Member card (grid tile) ──────────────────────────────────────────────────

function MemberCard({ member }: { member: TeamMember }) {
  const initials = member.full_name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);

  return (
    <View style={styles.card}>
      {/* Photo */}
      <View style={styles.photoWrap}>
        {member.photo_url ? (
          <Image
            source={{ uri: member.photo_url }}
            style={styles.photo}
            contentFit="cover"
          />
        ) : (
          <View style={styles.photoPlaceholder}>
            <Text style={styles.initials}>{initials}</Text>
          </View>
        )}
      </View>

      {/* Info */}
      <View style={styles.info}>
        <Text style={styles.name} numberOfLines={2}>{member.full_name}</Text>
        {(member.position ?? member.job_title) ? (
          <Text style={styles.jobTitle} numberOfLines={1}>{member.position ?? member.job_title}</Text>
        ) : null}
        <View style={styles.pointsRow}>
          <Ionicons name="star" size={11} color="#f59e0b" />
          <Text style={styles.points}>{member.points_balance.toLocaleString()}</Text>
        </View>
      </View>
    </View>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function TeamDepartmentScreen() {
  const { employee } = useEmployee();
  const { department: raw, hotel: hotelParam } = useLocalSearchParams<{ department: string; hotel?: string }>();
  const department = decodeURIComponent(raw ?? '');

  // APA employees pass the chosen hotel via params; regular employees use their own.
  const isAPA = employee?.hotel === APA_HOTEL;
  const hotel = (isAPA && hotelParam) ? hotelParam : (employee?.hotel ?? '');

  const { data: members = [], isLoading, isError } = useTeamByDepartment(hotel, department);

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
            <Text style={styles.title} numberOfLines={2}>{department}</Text>
            {isAPA && hotel ? (
              <Text style={styles.hotelName}>{hotel}</Text>
            ) : null}
          </View>
          <View style={{ width: 38 }} />
        </View>
        {!isLoading && (
          <Text style={styles.subtitle}>
            {members.length} {members.length === 1 ? 'team member' : 'team members'}
          </Text>
        )}
      </View>

      {isLoading && (
        <ActivityIndicator color={PURPLE} style={{ marginTop: 40 }} />
      )}

      {isError && (
        <View style={styles.empty}>
          <Ionicons name="alert-circle-outline" size={48} color="#fca5a5" />
          <Text style={styles.emptyText}>Failed to load team</Text>
        </View>
      )}

      {!isLoading && !isError && (
        <FlatList
          data={members}
          keyExtractor={(m) => m.id}
          numColumns={COLUMNS}
          columnWrapperStyle={styles.row}
          contentContainerStyle={styles.body}
          renderItem={({ item }) => <MemberCard member={item} />}
          ListEmptyComponent={
            <View style={styles.empty}>
              <Ionicons name="people-outline" size={48} color="#cbd5e1" />
              <Text style={styles.emptyText}>No team members found</Text>
            </View>
          }
        />
      )}
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
  },

  body: {
    paddingHorizontal: H_PADDING,
    paddingTop: 20,
    paddingBottom: 40,
    gap: CARD_GAP,
  },

  row: {
    gap: CARD_GAP,
  },

  // ── Grid card ──────────────────────────────────────────────────────────────
  card: {
    width: CARD_WIDTH,
    backgroundColor: '#ffffff',
    borderRadius: 16,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.07,
    shadowRadius: 6,
    elevation: 3,
  },

  photoWrap: {
    width: '100%',
    height: CARD_WIDTH,
  },
  photo: {
    width: '100%',
    height: '100%',
  },
  photoPlaceholder: {
    width: '100%',
    height: '100%',
    backgroundColor: '#ede9fe',
    alignItems: 'center',
    justifyContent: 'center',
  },
  initials: {
    fontSize: 32,
    fontWeight: '700',
    color: PURPLE,
  },

  info: {
    padding: 10,
    gap: 2,
  },
  name: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1e293b',
    lineHeight: 17,
  },
  jobTitle: {
    fontSize: 11,
    color: '#64748b',
  },
  pointsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 3,
    marginTop: 4,
  },
  points: {
    fontSize: 11,
    fontWeight: '600',
    color: '#f59e0b',
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
