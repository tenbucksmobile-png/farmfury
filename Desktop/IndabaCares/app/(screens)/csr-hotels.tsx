import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { Image } from 'expo-image';
import { Stack, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { COLORS } from '@/lib/constants';
import { indabaHotel, chobeSafariLodge } from '@/lib/localImages';

// Only these two hotels have channels
const CHANNEL_HOTEL_NAMES = ['Indaba Hotel', 'Chobe Safari Lodge'] as const;

const HOTEL_LOGOS: Record<string, string> = {
  'Indaba Hotel':       indabaHotel,
  'Chobe Safari Lodge': chobeSafariLodge,
};

export default function ChannelPickerScreen() {
  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      {/* Header */}
      <View style={s.header}>
        <View style={s.titleRow}>
          <TouchableOpacity onPress={() => router.back()} style={s.backBtn} hitSlop={8}>
            <Ionicons name="arrow-back" size={22} color="#fff" />
          </TouchableOpacity>
          <Text style={s.title}>Channel</Text>
          <View style={{ width: 38 }} />
        </View>
        <Text style={s.subtitle}>Follow your hotel's story</Text>
      </View>

      {/* Hotel cards */}
      <View style={s.body}>
        {CHANNEL_HOTEL_NAMES.map((name) => (
          <TouchableOpacity
            key={name}
            activeOpacity={0.75}
            style={s.card}
            onPress={() =>
              router.push({
                pathname: '/(screens)/channel-feed' as any,
                params:   { hotel: name },
              })
            }
          >
            <View style={s.logoWrap}>
              <Image source={{ uri: HOTEL_LOGOS[name] }} style={s.logo} contentFit="contain" />
            </View>
            <View style={s.cardText}>
              <Text style={s.hotelName}>{name}</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color="#cbd5e1" />
          </TouchableOpacity>
        ))}
      </View>
    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: '#F2F2F2',
  },

  header: {
    backgroundColor: COLORS.primary,
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
    flex: 1,
    textAlign: 'center',
    fontSize: 18,
    fontWeight: '700',
    color: '#fff',
  },
  subtitle: {
    textAlign: 'center',
    fontSize: 13,
    color: 'rgba(255,255,255,0.7)',
    marginTop: 2,
  },

  body: {
    paddingHorizontal: 16,
    paddingTop: 28,
    gap: 14,
  },
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#fff',
    borderRadius: 18,
    paddingHorizontal: 16,
    paddingVertical: 18,
    gap: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.07,
    shadowRadius: 8,
    elevation: 3,
  },
  logoWrap: {
    width: 52,
    height: 52,
    borderRadius: 14,
    backgroundColor: '#f5f3ff',
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  logo: {
    width: 46,
    height: 46,
  },
  cardText: {
    flex: 1,
  },
  hotelName: {
    fontSize: 15,
    fontWeight: '700',
    color: '#1e293b',
  },
  hotelSub: {
    fontSize: 12,
    color: '#94a3b8',
    marginTop: 2,
  },
});
