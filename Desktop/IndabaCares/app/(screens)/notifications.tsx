import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  FlatList,
  Pressable,
  RefreshControl,
  ActivityIndicator,
  Switch,
  StyleSheet,
} from 'react-native';
import { Stack, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useNotifications, useMarkRead, useMarkAllRead } from '@/hooks/use-notifications';
import { NotificationItem } from '@/components/notifications/NotificationItem';
import type { AppNotification } from '@/api/notification-service';

const PURPLE      = '#7B1FA2';
const PURPLE_SOFT = '#ede9fe';
const PURPLE_MID  = '#ddd6fe';

const PREF_KEY               = '@indabacares/notif_recognition';
const PREF_KEY_YOUR_RECOG    = '@indabacares/notif_your_recognition';
const PREF_KEY_ANNOUNCEMENTS = '@indabacares/notif_announcements';
const PREF_KEY_REWARDS        = '@indabacares/notif_rewards';
const PREF_KEY_GAMIFICATION   = '@indabacares/notif_gamification';

// ─── Navigation helper ────────────────────────────────────────────────────────

function navigateFromNotification(n: AppNotification) {
  switch (n.type) {
    case 'recognition_received':
      router.push('/');
      break;
    case 'reward_approved':
    case 'reward_rejected':
      router.push('/(screens)/orders');
      break;
    default:
      break;
  }
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function NotificationsScreen() {
  const { data: notifications = [], isLoading, refetch, isRefetching } = useNotifications();
  const markRead    = useMarkRead();
  const markAllRead = useMarkAllRead();

  const [recognitionEnabled, setRecognitionEnabled] = useState(true);
  const [yourRecognitionEnabled, setYourRecognitionEnabled] = useState(true);
  const [announcementsEnabled, setAnnouncementsEnabled] = useState(true);
  const [rewardsEnabled, setRewardsEnabled] = useState(true);
  const [gamificationEnabled, setGamificationEnabled] = useState(true);

  // Load saved preferences
  useEffect(() => {
    AsyncStorage.getItem(PREF_KEY).then((val) => {
      if (val !== null) setRecognitionEnabled(val === 'true');
    });
    AsyncStorage.getItem(PREF_KEY_YOUR_RECOG).then((val) => {
      if (val !== null) setYourRecognitionEnabled(val === 'true');
    });
    AsyncStorage.getItem(PREF_KEY_ANNOUNCEMENTS).then((val) => {
      if (val !== null) setAnnouncementsEnabled(val === 'true');
    });
    AsyncStorage.getItem(PREF_KEY_REWARDS).then((val) => {
      if (val !== null) setRewardsEnabled(val === 'true');
    });
    AsyncStorage.getItem(PREF_KEY_GAMIFICATION).then((val) => {
      if (val !== null) setGamificationEnabled(val === 'true');
    });
  }, []);

  function handleToggle(val: boolean) {
    setRecognitionEnabled(val);
    AsyncStorage.setItem(PREF_KEY, String(val));
  }

  function handleYourRecognitionToggle(val: boolean) {
    setYourRecognitionEnabled(val);
    AsyncStorage.setItem(PREF_KEY_YOUR_RECOG, String(val));
  }

  function handleAnnouncementsToggle(val: boolean) {
    setAnnouncementsEnabled(val);
    AsyncStorage.setItem(PREF_KEY_ANNOUNCEMENTS, String(val));
  }

  function handleRewardsToggle(val: boolean) {
    setRewardsEnabled(val);
    AsyncStorage.setItem(PREF_KEY_REWARDS, String(val));
  }

  function handleGamificationToggle(val: boolean) {
    setGamificationEnabled(val);
    AsyncStorage.setItem(PREF_KEY_GAMIFICATION, String(val));
  }

  const unreadCount = notifications.filter((n) => !n.read).length;

  const handlePress = (n: AppNotification) => {
    if (!n.read) markRead.mutate(n.id);
    navigateFromNotification(n);
  };

  // Split unread / earlier
  const unread  = notifications.filter((n) => !n.read);
  const earlier = notifications.filter((n) =>  n.read);

  type ListItem = AppNotification | { _section: string };

  const listData: ListItem[] = [
    ...(unread.length  > 0 ? [{ _section: `New (${unread.length})` } as ListItem, ...unread]  : []),
    ...(earlier.length > 0 ? [{ _section: 'Earlier' }               as ListItem, ...earlier] : []),
  ];

  const renderItem = ({ item }: { item: ListItem }) => {
    if ('_section' in item) {
      return <Text style={s.sectionLabel}>{item._section}</Text>;
    }
    return (
      <>
        <NotificationItem notification={item} onPress={() => handlePress(item)} />
        <View style={s.divider} />
      </>
    );
  };

  if (isLoading) {
    return (
      <SafeAreaView style={{ flex: 1, backgroundColor: PURPLE }} edges={['top']}>
        <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#fff' }}>
          <ActivityIndicator size="large" color={PURPLE} />
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />
      <View style={s.screen}>
        <FlatList
          data={listData}
          keyExtractor={(item, i) => ('_section' in item ? item._section : item.id)}
          renderItem={renderItem}
          contentContainerStyle={{ paddingBottom: 40 }}
          refreshControl={
            <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={PURPLE} />
          }
          ListHeaderComponent={
            <View>
              {/* ── Purple header ────────────────────── */}
              <View style={s.header}>
                <Pressable onPress={() => router.replace('/(tabs)/profile' as any)} style={s.backBtn} hitSlop={12}>
                  <Ionicons name="chevron-back" size={22} color="#fff" />
                </Pressable>
                <View style={{ flex: 1 }}>
                  <Text style={s.headerTitle}>Notifications</Text>
                  {unreadCount > 0 && (
                    <Text style={s.headerSub}>{unreadCount} unread</Text>
                  )}
                </View>
                {unreadCount > 0 && (
                  <Pressable
                    onPress={() => markAllRead.mutate()}
                    disabled={markAllRead.isPending}
                    style={s.markAllBtn}
                  >
                    {markAllRead.isPending
                      ? <ActivityIndicator size="small" color={PURPLE} />
                      : <Text style={s.markAllText}>Mark all read</Text>}
                  </Pressable>
                )}
              </View>

              {/* ── White sheet ──────────────────────── */}
              <View style={s.sheet}>
                <View style={s.sheetHandle} />

                {/* ── Notification preferences ─────── */}
                <View style={s.prefContainer}>
                  <View style={s.prefRow}>
                    <View style={{ flex: 1 }}>
                      <Text style={s.prefLabel}>Recognitions</Text>
                      <Text style={s.prefSub}>Receive instant notification for all recognitions</Text>
                    </View>
                    <Switch
                      value={recognitionEnabled}
                      onValueChange={handleToggle}
                      trackColor={{ false: '#e2e8f0', true: PURPLE_MID }}
                      thumbColor={recognitionEnabled ? PURPLE : '#cbd5e1'}
                      ios_backgroundColor="#e2e8f0"
                    />
                  </View>
                  <View style={s.prefDivider} />
                  <View style={s.prefRow}>
                    <View style={{ flex: 1 }}>
                      <Text style={s.prefLabel}>Your Recognition</Text>
                      <Text style={s.prefSub}>Receive instant notifications when you are recognised</Text>
                    </View>
                    <Switch
                      value={yourRecognitionEnabled}
                      onValueChange={handleYourRecognitionToggle}
                      trackColor={{ false: '#e2e8f0', true: PURPLE_MID }}
                      thumbColor={yourRecognitionEnabled ? PURPLE : '#cbd5e1'}
                      ios_backgroundColor="#e2e8f0"
                    />
                  </View>
                  <View style={s.prefDivider} />
                  <View style={s.prefRow}>
                    <View style={{ flex: 1 }}>
                      <Text style={s.prefLabel}>Announcements</Text>
                      <Text style={s.prefSub}>Receive instant notifications for announcements</Text>
                    </View>
                    <Switch
                      value={announcementsEnabled}
                      onValueChange={handleAnnouncementsToggle}
                      trackColor={{ false: '#e2e8f0', true: PURPLE_MID }}
                      thumbColor={announcementsEnabled ? PURPLE : '#cbd5e1'}
                      ios_backgroundColor="#e2e8f0"
                    />
                  </View>
                  <View style={s.prefDivider} />
                  <View style={s.prefRow}>
                    <View style={{ flex: 1 }}>
                      <Text style={s.prefLabel}>Rewards</Text>
                      <Text style={s.prefSub}>Receive instant notifications when rewards are available</Text>
                    </View>
                    <Switch
                      value={rewardsEnabled}
                      onValueChange={handleRewardsToggle}
                      trackColor={{ false: '#e2e8f0', true: PURPLE_MID }}
                      thumbColor={rewardsEnabled ? PURPLE : '#cbd5e1'}
                      ios_backgroundColor="#e2e8f0"
                    />
                  </View>
                </View>
              </View>
            </View>
          }
        />
      </View>
    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const s = StyleSheet.create({
  safe:   { flex: 1, backgroundColor: PURPLE },
  screen: { flex: 1, backgroundColor: '#ffffff' },

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
    width: 36, height: 36, borderRadius: 10,
    backgroundColor: 'rgba(255,255,255,0.18)',
    alignItems: 'center', justifyContent: 'center',
    marginRight: 12,
  },
  headerTitle: { fontSize: 20, fontWeight: '700', color: '#fff' },
  headerSub:   { fontSize: 12, color: 'rgba(255,255,255,0.65)', marginTop: 1 },
  markAllBtn: {
    backgroundColor: 'rgba(255,255,255,0.18)',
    borderRadius: 20,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  markAllText: { fontSize: 12, fontWeight: '700', color: '#fff' },

  // White sheet
  sheet: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    marginTop: -20,
    paddingTop: 12,
  },
  sheetHandle: {
    width: 36, height: 4, borderRadius: 2,
    backgroundColor: PURPLE_MID,
    alignSelf: 'center',
    marginBottom: 16,
  },

  // Preferences
  prefsTitle: {
    fontSize: 13,
    fontWeight: '700',
    color: '#64748b',
    letterSpacing: 0.6,
    textTransform: 'uppercase',
    paddingHorizontal: 20,
    marginBottom: 12,
  },
  prefContainer: {
    marginHorizontal: 20,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: PURPLE_MID,
    borderRadius: 14,
    overflow: 'hidden',
  },
  prefRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 14,
  },
  prefLabel:   { fontSize: 17, fontWeight: '700', color: '#1e1b4b' },
  prefSub:     { fontSize: 12, color: '#94a3b8', marginTop: 3 },
  prefDivider: { height: 1, backgroundColor: PURPLE_MID, marginHorizontal: 16 },

  // List
  sectionLabel: {
    backgroundColor: '#f8f7ff',
    paddingHorizontal: 20,
    paddingVertical: 8,
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.8,
    textTransform: 'uppercase',
    color: '#94a3b8',
  },
  divider: { height: 1, backgroundColor: '#f1f5f9', marginHorizontal: 20 },

});
