import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, ScrollView, ActivityIndicator } from 'react-native';
import { Image } from 'expo-image';
import { Stack, router, useLocalSearchParams } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useInitiativeThumbnails } from '@/hooks/use-initiatives';

const PURPLE = '#7B1FA2';

export default function IndabaCaresListScreen() {
  const { hotel } = useLocalSearchParams<{ hotel: string }>();

  const { data: tabs = [], isLoading, isError } = useInitiativeThumbnails(hotel ?? '');

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
            <Text style={styles.title}>Indaba Cares</Text>
            {hotel ? <Text style={styles.hotelName}>{hotel}</Text> : null}
          </View>
          <View style={{ width: 38 }} />
        </View>
        <Text style={styles.subtitle}>Choose an initiative to explore</Text>
      </View>

      {/* List */}
      <ScrollView contentContainerStyle={styles.body}>

        {isLoading && (
          <View style={styles.center}>
            <ActivityIndicator size="large" color={PURPLE} />
          </View>
        )}

        {isError && (
          <View style={styles.center}>
            <Ionicons name="alert-circle-outline" size={48} color="#e2d9f3" />
            <Text style={styles.emptyTitle}>Could not load initiatives</Text>
            <Text style={styles.emptyText}>Please check your connection and try again.</Text>
          </View>
        )}

        {!isLoading && !isError && tabs.length === 0 && (
          <View style={styles.center}>
            <Ionicons name="ribbon-outline" size={56} color="#e2d9f3" />
            <Text style={styles.emptyTitle}>Coming Soon</Text>
            <Text style={styles.emptyText}>
              CSR content for {hotel ?? 'this property'} will appear here.
            </Text>
          </View>
        )}

        {tabs.map((item) => (
          <TouchableOpacity
            key={item.tab}
            activeOpacity={0.75}
            style={styles.row}
            onPress={() =>
              router.push({
                pathname: '/(screens)/initiative/[slug]',
                params: { slug: item.tab, hotel: hotel ?? '' },
              })
            }
          >
            {/* Thumbnail */}
            <View style={styles.thumb}>
              {item.mascot_url ? (
                <Image
                  source={{ uri: item.mascot_url }}
                  style={styles.thumbImage}
                  contentFit="contain"
                />
              ) : (
                <Ionicons name="ribbon-outline" size={22} color={PURPLE} />
              )}
            </View>

            <Text style={styles.rowLabel}>{item.tab}</Text>
            <Ionicons name="chevron-forward" size={18} color="#cbd5e1" />
          </TouchableOpacity>
        ))}

      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: '#F2F2F2',
  },

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
    marginTop: 2,
  },

  body: {
    paddingHorizontal: 16,
    paddingTop: 20,
    paddingBottom: 40,
    gap: 12,
  },

  center: {
    paddingTop: 80,
    alignItems: 'center',
    gap: 14,
  },
  emptyTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1e1b4b',
  },
  emptyText: {
    fontSize: 14,
    color: '#94a3b8',
    textAlign: 'center',
    maxWidth: 260,
    lineHeight: 22,
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

  thumb: {
    width: 56,
    height: 56,
    borderRadius: 12,
    backgroundColor: '#f5f3ff',
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  thumbImage: {
    width: 56,
    height: 56,
  },

  rowLabel: {
    flex: 1,
    fontSize: 16,
    fontWeight: '600',
    color: '#1e293b',
  },
});
