import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  Pressable,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  Platform,
  Alert,
  ScrollView,
} from 'react-native';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import * as ImagePicker from 'expo-image-picker';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useEmployee } from '@/providers/EmployeeContext';
import { supabase } from '@/lib/supabase';
import { uploadImage } from '@/utils/image';
import { useQuery } from '@tanstack/react-query';
import { useReactionBalance, REACTION_TOTALS } from '@/hooks/use-reaction-balance';
import { useRecognitionBalance, MONTHLY_RECOGNITION_LIMIT } from '@/hooks/use-recognition-balance';
import { useSkillsBalance, MONTHLY_SKILLS_LIMIT } from '@/hooks/use-skills-balance';
import { usePointsBreakdown } from '@/hooks/use-points-breakdown';
import { useWalletStats, useConvertPoints } from '@/hooks/use-rewards';
import { useUserBadges } from '@/hooks/use-user-badges';
import { QUERY_KEYS } from '@/lib/constants';
import { BadgeGivenSheet } from '@/components/profile/BadgeGivenSheet';

// ─── Hotel background images ──────────────────────────────────────────────────

const HOTEL_BACKGROUNDS: Record<string, number> = {
  'Indaba Hotel':              require('../../assets/Indaba-long.jpg'),
  'Indaba Lodge Gaborone':     require('../../assets/ILG.jpg'),
  'Indaba Lodge Richards Bay': require('../../assets/ILRB.jpg'),
};
const DEFAULT_BG = require('../../assets/Indaba-long.jpg');

// ─── Brand colours ────────────────────────────────────────────────────────────

const PURPLE     = '#7B1FA2';
const PURPLE_MID = '#9C27B0';
const ACCENT     = '#CE21FB';
const LIGHT_TEXT = '#EDE7F6';

// ─── Status tiers ─────────────────────────────────────────────────────────────

const STATUS_TIERS = [
  { label: 'Gold',   min: 50, icon: 'trophy' as const, color: '#fbbf24' },
  { label: 'Silver', min: 20, icon: 'trophy' as const, color: '#cbd5e1' },
  { label: 'Bronze', min: 5,  icon: 'trophy' as const, color: '#cd7f32' },
];

function getStatus(weeklyRecognitions: number) {
  for (const tier of STATUS_TIERS) {
    if (weeklyRecognitions >= tier.min) return tier;
  }
  return { label: 'Unranked', icon: 'trophy-outline' as const, color: 'rgba(255,255,255,0.4)' };
}

// ─── Dropdown menu items ──────────────────────────────────────────────────────

const MENU_ITEMS = [
  { label: 'Channel',         icon: 'ribbon-outline'      as const, route: '/(screens)/csr-hotels' },
  { label: 'Campaigns',       icon: 'megaphone-outline'   as const, route: '/(screens)/campaigns'  },
  { label: 'Know your team',  icon: 'people-outline'      as const, route: '/(screens)/team'       },
  { label: "FAQ's",           icon: 'help-circle-outline' as const, route: '/(screens)/faq'        },
];

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function ProfileScreen() {
  const { employee, clearEmployee } = useEmployee();

  const [photoUrl,    setPhotoUrl]    = useState<string | null>(null);
  const [uploading,   setUploading]   = useState(false);
  const [activeTab,   setActiveTab]   = useState<'balance' | 'utilise' | 'achieve'>('balance');
  const [menuOpen,    setMenuOpen]    = useState(false);
  const [badgeSheet,  setBadgeSheet]  = useState<'recognition' | 'skills' | null>(null);

  const { data: profileData, isLoading: profileLoading } = useQuery({
    queryKey: QUERY_KEYS.employeeProfile(employee?.employee_id ?? ''),
    queryFn: async () => {
      const { data } = await supabase
        .from('employees')
        .select('points_balance, job_title, position, photo_url')
        .eq('id', employee!.employee_id)
        .single();
      return data as { points_balance: number; job_title: string | null; position: string | null; photo_url: string | null } | null;
    },
    enabled: !!employee,
    staleTime: 60_000,
  });

  // Seed photo URL from fetched data (only if not already set by an upload)
  useEffect(() => {
    if (profileData?.photo_url && !photoUrl) setPhotoUrl(profileData.photo_url);
  }, [profileData?.photo_url]);

  const pointsBalance = profileData?.points_balance ?? null;
  const pointsLoading = profileLoading;
  const jobTitle      = profileData?.position ?? profileData?.job_title ?? null;

  const { data: reactionBalance,     isLoading: reactionLoading }       = useReactionBalance();
  const { data: recognitionRemaining, isLoading: recognitionLoading }   = useRecognitionBalance();
  const { data: skillsRemaining,     isLoading: skillsLoading }         = useSkillsBalance();
  const { data: pointsBreakdown,     isLoading: breakdownLoading }      = usePointsBreakdown();
  const { data: walletStats,         isLoading: walletLoading }         = useWalletStats();
  const convertPoints = useConvertPoints();
  const { data: badgeCount,           isLoading: badgesLoading }        = useUserBadges();

  // Number of recognitions given this month (used for status tier calculation).
  // Only computed once recognitionRemaining has resolved to avoid "Unranked" flash.
  const recognitionsGiven = recognitionLoading
    ? null
    : MONTHLY_RECOGNITION_LIMIT - (recognitionRemaining ?? MONTHLY_RECOGNITION_LIMIT);

  // Days until end-of-month reset
  const _now          = new Date();
  const _endOfMonth   = new Date(_now.getFullYear(), _now.getMonth() + 1, 0);
  const daysUntilReset = Math.max(1, Math.ceil((_endOfMonth.getTime() - _now.getTime()) / 86_400_000));

  if (!employee) return null;

  // ── Initials fallback ──────────────────────────────────────────────────────
  const initials = employee.full_name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);

  // ── Stats row ──────────────────────────────────────────────────────────────
  const stats = [
    { icon: '❤️', value: reactionBalance?.hearts_remaining ?? REACTION_TOTALS.heart    },
    { icon: '😊', value: reactionBalance?.smiles_remaining ?? REACTION_TOTALS.smile    },
    { icon: '👍', value: reactionBalance?.thumbs_remaining ?? REACTION_TOTALS.thumbs_up },
  ];

  const remainingReactionPts = reactionBalance?.total_remaining ?? REACTION_TOTALS.total;

  // ── Photo upload ───────────────────────────────────────────────────────────

  async function handleUploadFromSource(source: 'camera' | 'library') {
    if (!employee) return;

    let result: ImagePicker.ImagePickerResult;

    if (source === 'camera') {
      const { status } = await ImagePicker.requestCameraPermissionsAsync();
      if (status !== 'granted') {
        Alert.alert('Permission Required', 'Camera access is needed to take a photo.');
        return;
      }
      result = await ImagePicker.launchCameraAsync({
        allowsEditing: true,
        aspect:        [1, 1],
        quality:       0.8,
      });
    } else {
      const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (status !== 'granted') {
        Alert.alert('Permission Required', 'Photo library access is needed to choose a photo.');
        return;
      }
      result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ['images'],
        allowsEditing: true,
        aspect:        [1, 1],
        quality:       0.8,
      });
    }

    if (result.canceled || !result.assets[0]) return;

    const uri = result.assets[0].uri;

    // Snapshot current photo so we can restore it on failure
    const previousPhotoUrl = photoUrl;

    // Optimistic preview
    setPhotoUrl(uri);
    setUploading(true);

    try {
      const { publicUrl } = await uploadImage(
        uri,
        'avatars',
        `${employee.employee_id}/avatar`,
      );

      await supabase.rpc('update_employee_avatar', { p_photo_url: publicUrl });
      setPhotoUrl(publicUrl);
    } catch (err: any) {
      // Restore the previous photo — do not leave the profile blank
      setPhotoUrl(previousPhotoUrl);
      Alert.alert('Upload Failed', err.message ?? 'Something went wrong. Please try again.');
    } finally {
      setUploading(false);
    }
  }

  function handleAvatarPress() {
    Alert.alert(
      'Profile Photo',
      'Choose how to update your photo',
      [
        { text: 'Take Photo',          onPress: () => handleUploadFromSource('camera')  },
        { text: 'Choose from Library', onPress: () => handleUploadFromSource('library') },
        { text: 'Cancel',              style: 'cancel' },
      ],
      { cancelable: true },
    );
  }

  // ──────────────────────────────────────────────────────────────────────────

  return (
    <SafeAreaView style={styles.safeArea} edges={['top']}>

      <View style={styles.screen}>

        {/* ── Image header card ───────────────────────────────────────────── */}
        <View style={styles.header}>
          <Image
            source={HOTEL_BACKGROUNDS[employee.hotel] ?? DEFAULT_BG}
            style={StyleSheet.absoluteFillObject}
            contentFit="cover"
          />
          {/* Dark overlay — full height of header */}
          <View style={styles.headerOverlay}>

          {/* Top navigation */}
          <View style={styles.topNav}>
            <Pressable
              onPress={() => setMenuOpen((o) => !o)}
              hitSlop={10}
              style={styles.navIcon}
            >
              <Ionicons
                name={menuOpen ? 'close' : 'menu'}
                size={26}
                color="#ffffff"
              />
            </Pressable>
            <Pressable
              onPress={() => router.push('/(screens)/notifications')}
              hitSlop={10}
              style={styles.navIcon}
            >
              <Ionicons name="notifications-outline" size={26} color="#ffffff" />
            </Pressable>
          </View>

          {/* Profile row: avatar left, text right */}
          <View style={styles.profileRow}>

            {/* Avatar — tappable, splash style matching leaderboard podium */}
            <Pressable onPress={handleAvatarPress} style={styles.avatarWrapper}>
              {/* Splash layer 1 — accent blob (behind) */}
              <View style={styles.splashAccent} />
              {/* Splash layer 2 — deep purple base blob (on top) */}
              <View style={styles.splashBase} />

              {/* Photo + badges in a relative sub-container */}
              <View style={styles.photoContainer}>
                {photoUrl ? (
                  <Image
                    source={{ uri: photoUrl }}
                    style={styles.avatarImage}
                    contentFit="cover"
                    onError={() => setPhotoUrl(null)}
                  />
                ) : (
                  <View style={styles.avatarPlaceholder}>
                    <Text style={styles.avatarInitials}>{initials}</Text>
                  </View>
                )}
                {uploading && (
                  <View style={styles.avatarSpinner}>
                    <ActivityIndicator color="#ffffff" size="small" />
                  </View>
                )}
                <View style={styles.cameraBadge}>
                  <Ionicons name="camera" size={12} color="#ffffff" />
                </View>
              </View>
            </Pressable>

            {/* Name / title / meta */}
            <View style={styles.profileInfo}>
              <Text style={styles.name}>{employee.full_name}</Text>
              {jobTitle ? <Text style={styles.subtitle}>{jobTitle}</Text> : null}
              <View style={styles.metaRow}>
                <Text style={styles.metaText}>{employee.hotel}</Text>
              </View>
            </View>

          </View>

          </View>{/* end headerOverlay */}

        </View>{/* end header */}

        {/* ── Pill tab selector — sits on the header's rounded bottom edge ── */}
        <View style={styles.tabContainer}>
          <View style={styles.tabPill}>
            {(['balance', 'utilise', 'achieve'] as const).map((tab) => {
              const active = activeTab === tab;
              const label = tab === 'balance' ? 'Share' : tab === 'utilise' ? 'Engage' : 'Reward';
              return (
                <TouchableOpacity
                  key={tab}
                  onPress={() => setActiveTab(tab)}
                  style={[styles.tabButton, active && styles.tabButtonActive]}
                  activeOpacity={0.7}
                >
                  <Text style={[styles.tabText, active && styles.tabTextActive]}>
                    {label}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>
        </View>

        {/* ── Engage tab — hero frozen, breakdown scrollable ──────────────── */}
        {activeTab === 'utilise' && (
          <View style={styles.engageWrapper}>
            {/* Frozen hero card */}
            <View style={styles.engageHeroWrap}>
              <LinearGradient
                colors={['#3b0764', '#6d28d9', '#7B1FA2']}
                start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }}
                style={styles.utiliseFeedCard}
              >
                <View style={styles.utiliseFeedIconWrap}>
                  <Text style={styles.utiliseFeedIcon}>🌟</Text>
                </View>
                <View style={styles.utiliseFeedContent}>
                  <Text style={styles.utiliseFeedTitle}>Recognition Points</Text>
                  <Text style={styles.utiliseFeedDesc}>Points earned through all engagement activity this month.</Text>
                </View>
                {breakdownLoading ? (
                  <ActivityIndicator size="small" color="#fff" style={{ alignSelf: 'center' }} />
                ) : (
                  <Text style={styles.utiliseFeedBigCount}>{pointsBreakdown?.total ?? 0}</Text>
                )}
              </LinearGradient>
            </View>

            {/* Scrollable breakdown */}
            <ScrollView style={styles.contentScroll} contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 24 }} showsVerticalScrollIndicator={false}>
              <View style={styles.rewardBreakdownCard}>
                {([
                  { emoji: '🏅', title: 'Recognition Received', desc: 'The number of times you\'ve been recognised for your work.',     value: pointsBreakdown?.recognition_received ?? 0 },
                  { emoji: '🎓', title: 'Skills Shoutout',      desc: 'Endorsements received for your skills and expertise.',           value: pointsBreakdown?.skills_received ?? 0 },
                  { emoji: '💬', title: 'Responses Made',       desc: 'Your engagement to recognition you have received.',              value: pointsBreakdown?.responses ?? 0 },
                  { emoji: '😊', title: 'Mood Board',           desc: 'Updating your mood everyday earns you points.',                  value: pointsBreakdown?.mood_checkin ?? 0 },
                  { emoji: '🎂', title: 'Birthday',             desc: 'Celebrations and messages received on your birthday.',           value: pointsBreakdown?.birthday ?? 0 },
                  { emoji: '🎖', title: 'Service Milestone',    desc: 'Recognition for your time and loyalty in the company.',          value: pointsBreakdown?.anniversary ?? 0 },
                  { emoji: '⭐', title: 'Status Unlock',        desc: 'New levels achieved through consistent engagement.',             value: pointsBreakdown?.status_unlock ?? 0 },
                  { emoji: '🏆', title: 'Badges Achieved',      desc: 'Awards earned through performance and participation.',           value: pointsBreakdown?.badge_achieved ?? 0 },
                  { emoji: '👑', title: 'Legend of the Month',       desc: 'Top performer recognition awarded monthly.',                         value: pointsBreakdown?.legend_of_month          ?? 0 },
                  { emoji: '📢', title: 'Campaign Points',           desc: 'Earn points by engaging in campaigns.',                              value: pointsBreakdown?.campaign_points          ?? 0 },
                  { emoji: '🌟', title: 'Special Management Award',  desc: 'Special recognition for extraordinary Guest mentions.',              value: pointsBreakdown?.special_management_award ?? 0 },
                ]).map((item, i, arr) => (
                  <View key={item.title}>
                    <View style={styles.engageBreakdownRow}>
                      <Text style={styles.engageBreakdownEmoji}>{item.emoji}</Text>
                      <View style={styles.engageBreakdownContent}>
                        <Text style={styles.engageBreakdownTitle}>{item.title}</Text>
                        <Text style={styles.engageBreakdownDesc}>{item.desc}</Text>
                      </View>
                      <Text style={styles.engageBreakdownValue}>{item.value}</Text>
                    </View>
                    {i < arr.length - 1 && <View style={styles.achieveDivider} />}
                  </View>
                ))}
              </View>
            </ScrollView>
          </View>
        )}

        {/* ── Content area (Balance + Reward tabs) ────────────────────────── */}
        <ScrollView style={[styles.contentScroll, activeTab === 'utilise' && { display: 'none' }]} contentContainerStyle={[styles.content, { paddingBottom: 24 }]} showsVerticalScrollIndicator={false}>

          {/* ── Balance tab ─────────────────────────────────────────────── */}
          {activeTab === 'balance' && (
            <>
              {/* Recognition Badges */}
              <TouchableOpacity
                activeOpacity={0.85}
                onPress={() => setBadgeSheet('recognition')}
              >
                <LinearGradient
                  colors={['#3b0764', '#6d28d9', '#7B1FA2']}
                  start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }}
                  style={styles.utiliseFeedCard}
                >
                  <View style={styles.utiliseFeedIconWrap}>
                    <Text style={styles.utiliseFeedIcon}>🏅</Text>
                  </View>
                  <View style={styles.utiliseFeedContent}>
                    <Text style={styles.utiliseFeedTitle}>Recognition Badges</Text>
                    <Text style={styles.utiliseFeedDesc}>You receive ten badges a month to give to colleagues.</Text>
                    <Text style={styles.utiliseFeedInsight}>Use these to highlight great performance or behaviour towards Guest or in general.</Text>
                  </View>
                  <View style={styles.utiliseFeedBigCountWrap}>
                    {recognitionLoading ? (
                      <ActivityIndicator size="small" color="#fff" />
                    ) : (
                      <Text style={styles.utiliseFeedBigCount}>{recognitionRemaining ?? MONTHLY_RECOGNITION_LIMIT}</Text>
                    )}
                    <Ionicons name="chevron-forward" size={14} color="rgba(255,255,255,0.5)" style={{ marginTop: 4 }} />
                  </View>
                </LinearGradient>
              </TouchableOpacity>

              {/* Skills Badges */}
              <TouchableOpacity
                activeOpacity={0.85}
                onPress={() => setBadgeSheet('skills')}
              >
                <LinearGradient
                  colors={['#1e3a5f', '#1d4ed8', '#2563eb']}
                  start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }}
                  style={styles.utiliseFeedCard}
                >
                  <View style={styles.utiliseFeedIconWrap}>
                    <Text style={styles.utiliseFeedIcon}>🎓</Text>
                  </View>
                  <View style={styles.utiliseFeedContent}>
                    <Text style={styles.utiliseFeedTitle}>Skills Badges</Text>
                    <Text style={styles.utiliseFeedDesc}>You receive ten badges a month to give to colleagues.</Text>
                    <Text style={styles.utiliseFeedInsight}>Use these to endorse skills and talent of a colleague.</Text>
                  </View>
                  <View style={styles.utiliseFeedBigCountWrap}>
                    {skillsLoading ? (
                      <ActivityIndicator size="small" color="#fff" />
                    ) : (
                      <Text style={styles.utiliseFeedBigCount}>{skillsRemaining ?? MONTHLY_SKILLS_LIMIT}</Text>
                    )}
                    <Ionicons name="chevron-forward" size={14} color="rgba(255,255,255,0.5)" style={{ marginTop: 4 }} />
                  </View>
                </LinearGradient>
              </TouchableOpacity>

              {/* Emoji */}
              <LinearGradient
                colors={['#064e3b', '#065f46', '#059669']}
                start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }}
                style={styles.utiliseFeedCard}
              >
                <View style={styles.utiliseFeedIconWrap}>
                  <Text style={styles.utiliseFeedIcon}>😀</Text>
                </View>
                <View style={styles.utiliseFeedContent}>
                  <Text style={styles.utiliseFeedTitle}>Emoji</Text>
                  <Text style={styles.utiliseFeedDesc}>You receive a hundred emoji's a month to give to colleagues.</Text>
                  <Text style={styles.utiliseFeedInsight}>Use these to engage recognitions given to colleagues.</Text>
                </View>
                {reactionLoading ? (
                  <ActivityIndicator size="small" color="#fff" style={{ alignSelf: 'center' }} />
                ) : (
                  <Text style={styles.utiliseFeedBigCount}>
                    {reactionBalance?.total_remaining ?? REACTION_TOTALS.total}
                  </Text>
                )}
              </LinearGradient>

              {/* Reset Timer */}
              <LinearGradient
                colors={['#451a03', '#92400e', '#b45309']}
                start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }}
                style={styles.utiliseFeedCard}
              >
                <View style={styles.utiliseFeedIconWrap}>
                  <Text style={styles.utiliseFeedIcon}>🔄</Text>
                </View>
                <View style={styles.utiliseFeedContent}>
                  <Text style={styles.utiliseFeedTitle}>Reset Timer</Text>
                  <Text style={styles.utiliseFeedDesc}>Your balance will refresh soon.</Text>
                  <Text style={styles.utiliseFeedInsight}>Unused allocations do not roll over.</Text>
                </View>
                <Text style={[styles.utiliseFeedBigCount, { fontSize: 18, alignSelf: 'center' }]}>
                  {daysUntilReset}{'\n'}
                  <Text style={{ fontSize: 10, fontWeight: '600', opacity: 0.7 }}>days left</Text>
                </Text>
              </LinearGradient>

            </>
          )}

          {/* ── Reward tab ──────────────────────────────────────────────────── */}
          {activeTab === 'achieve' && (
            <>
              {/* Reward Wallet hero card */}
              <LinearGradient
                colors={['#3d2c00', '#92630a', '#d97706']}
                start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }}
                style={styles.utiliseFeedCard}
              >
                <View style={styles.utiliseFeedIconWrap}>
                  <Text style={styles.utiliseFeedIcon}>💰</Text>
                </View>
                <View style={styles.utiliseFeedContent}>
                  <Text style={styles.utiliseFeedTitle}>Reward Wallet</Text>
                  <Text style={styles.utiliseFeedDesc}>Your spendable reward credits.</Text>
                </View>
                {walletLoading ? (
                  <ActivityIndicator size="small" color="#fff" style={{ alignSelf: 'center' }} />
                ) : (
                  <Text style={styles.utiliseFeedBigCount}>{walletStats?.wallet_balance ?? 0}</Text>
                )}
              </LinearGradient>

              {/* Wallet breakdown */}
              <View style={[styles.rewardBreakdownCard, { marginBottom: 8 }]}>
                <View style={styles.rewardBreakdownRow}>
                  <Text style={styles.rewardBreakdownLabel}>Available</Text>
                  {walletLoading ? (
                    <ActivityIndicator size="small" color={PURPLE} />
                  ) : (
                    <Text style={styles.rewardBreakdownValue}>{walletStats?.wallet_balance ?? 0}</Text>
                  )}
                </View>
                <View style={styles.achieveDivider} />
                <View style={styles.rewardBreakdownRow}>
                  <Text style={styles.rewardBreakdownLabel}>Redeemed</Text>
                  {walletLoading ? (
                    <ActivityIndicator size="small" color={PURPLE} />
                  ) : (
                    <Text style={styles.rewardBreakdownValue}>{walletStats?.redeemed_total ?? 0}</Text>
                  )}
                </View>
                <View style={styles.achieveDivider} />
                <View style={styles.rewardBreakdownRow}>
                  <Text style={styles.rewardBreakdownLabel}>Earned this month</Text>
                  {walletLoading ? (
                    <ActivityIndicator size="small" color={PURPLE} />
                  ) : (
                    <Text style={styles.rewardBreakdownValue}>{walletStats?.converted_this_month ?? 0}</Text>
                  )}
                </View>
              </View>

              {/* Convert Points card */}
              <LinearGradient
                colors={['#1e1b4b', '#3730a3', '#4338ca']}
                start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }}
                style={styles.utiliseFeedCard}
              >
                <View style={styles.utiliseFeedIconWrap}>
                  <Text style={styles.utiliseFeedIcon}>🔄</Text>
                </View>
                <View style={styles.utiliseFeedContent}>
                  <Text style={styles.utiliseFeedTitle}>Convert Points</Text>
                  {walletLoading ? (
                    <Text style={styles.utiliseFeedDesc}>Loading…</Text>
                  ) : (walletStats?.available_to_convert ?? 0) < 5 ? (
                    <Text style={styles.utiliseFeedDesc}>Earn more Recognition Points to unlock conversions.</Text>
                  ) : (
                    <Text style={styles.utiliseFeedDesc}>
                      You are about to convert {walletStats!.available_to_convert} recognition points to {walletStats!.max_credits} Reward Point{walletStats!.max_credits !== 1 ? 's' : ''}
                    </Text>
                  )}
                </View>
                {!walletLoading && (walletStats?.available_to_convert ?? 0) >= 5 && (
                  <TouchableOpacity
                    style={styles.convertBtn}
                    activeOpacity={0.8}
                    disabled={convertPoints.isPending}
                    onPress={() => {
                      const amount = Math.floor((walletStats!.available_to_convert) / 5) * 5;
                      Alert.alert(
                        'Convert Points',
                        `Convert ${amount} Recognition Points into ${amount / 5} reward credit${amount / 5 !== 1 ? 's' : ''}?`,
                        [
                          { text: 'Cancel', style: 'cancel' },
                          {
                            text: 'Convert',
                            onPress: () =>
                              convertPoints.mutate(amount, {
                                onError: (err: Error) =>
                                  Alert.alert('Conversion Failed', err.message),
                              }),
                          },
                        ],
                      );
                    }}
                  >
                    {convertPoints.isPending ? (
                      <ActivityIndicator size="small" color="#fff" />
                    ) : (
                      <Text style={styles.convertBtnTxt}>Convert</Text>
                    )}
                  </TouchableOpacity>
                )}
              </LinearGradient>
            </>
          )}

        </ScrollView>

      </View>

      {/* ── Dropdown menu overlay ────────────────────────────────────────── */}
      {menuOpen && (
        <View style={StyleSheet.absoluteFillObject} pointerEvents="box-none">
          {/* Backdrop — tap outside to close */}
          <Pressable
            style={StyleSheet.absoluteFillObject}
            onPress={() => setMenuOpen(false)}
          />

          {/* Dropdown card */}
          <View style={styles.dropdown}>
            {MENU_ITEMS.map((item, index) => (
              <Pressable
                key={item.label}
                onPress={() => {
                  setMenuOpen(false);
                  router.push(item.route as any);
                }}
                style={[
                  styles.dropdownItem,
                  index < MENU_ITEMS.length - 1 && styles.dropdownItemBorder,
                ]}
              >
                <Ionicons name={item.icon} size={18} color={PURPLE} />
                <Text style={styles.dropdownItemLabel}>{item.label}</Text>
                <Ionicons name="chevron-forward" size={16} color="#cbd5e1" />
              </Pressable>
            ))}

            {/* Divider before sign out */}
            <View style={styles.dropdownDivider} />

            <Pressable
              onPress={async () => {
                setMenuOpen(false);
                await clearEmployee();
                router.replace('/(auth)/employee-auth');
              }}
              style={styles.dropdownItem}
            >
              <Ionicons name="log-out-outline" size={18} color="#ef4444" />
              <Text style={[styles.dropdownItemLabel, styles.signOutLabel]}>Sign Out</Text>
            </Pressable>
          </View>
        </View>
      )}

      {/* ── Badge given sheet ────────────────────────────────────────────── */}
      {badgeSheet !== null && (
        <BadgeGivenSheet
          cardType={badgeSheet}
          onClose={() => setBadgeSheet(null)}
        />
      )}

    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safeArea: {
    flex: 1,
    backgroundColor: '#1a0a2e',
  },

  screen: {
    flex: 1,
    backgroundColor: '#F2F2F2',
  },

  // ── Header ──────────────────────────────────────────────────────────────────
  header: {
    borderBottomLeftRadius: 30,
    borderBottomRightRadius: 30,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.25,
    shadowRadius: 14,
    elevation: 12,
  },

  headerOverlay: {
    backgroundColor: 'rgba(20,0,40,0.45)',
    paddingHorizontal: 20,
    paddingTop: Platform.OS === 'android' ? 12 : 8,
    paddingBottom: 20,
  },

  topNav: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },

  navIcon: {
    padding: 4,
  },

  // ── Profile row ─────────────────────────────────────────────────────────────
  profileRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 14,
    marginTop: 6,
    marginBottom: 4,
  },

  profileInfo: {
    flex: 1,
    justifyContent: 'center',
  },

  // ── Avatar — splash style (matches leaderboard podium) ─────────────────────
  avatarWrapper: {
    width: 146,
    height: 164,
    alignItems: 'center',
    justifyContent: 'center',
  },

  splashBase: {
    position: 'absolute',
    width: 130,
    height: 148,
    backgroundColor: PURPLE,
    borderTopLeftRadius: 72,
    borderTopRightRadius: 20,
    borderBottomRightRadius: 72,
    borderBottomLeftRadius: 20,
    opacity: 0.95,
    transform: [{ rotate: '22deg' }],
  },

  splashAccent: {
    position: 'absolute',
    width: 114,
    height: 130,
    top: 0,
    right: 2,
    backgroundColor: ACCENT,
    borderTopLeftRadius: 20,
    borderTopRightRadius: 60,
    borderBottomRightRadius: 20,
    borderBottomLeftRadius: 60,
    opacity: 0.65,
    transform: [{ rotate: '-14deg' }],
  },

  photoContainer: {
    position: 'relative',
    width: 90,
    height: 90,
    zIndex: 3,
  },

  avatarImage: {
    width: 90,
    height: 90,
    borderRadius: 12,
  },

  avatarPlaceholder: {
    width: 90,
    height: 90,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },

  avatarInitials: {
    fontSize: 32,
    fontWeight: 'bold',
    color: '#ffffff',
  },

  avatarSpinner: {
    position: 'absolute',
    top: 0, left: 0, right: 0, bottom: 0,
    borderRadius: 12,
    backgroundColor: 'rgba(0,0,0,0.45)',
    alignItems: 'center',
    justifyContent: 'center',
  },

  cameraBadge: {
    position: 'absolute',
    bottom: -4,
    right: -4,
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: ACCENT,
    borderWidth: 2,
    borderColor: '#ffffff',
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 1,
  },

  // ── Name & subtitle ──────────────────────────────────────────────────────────
  name: {
    fontSize: 22,
    fontWeight: '600',
    color: '#ffffff',
    marginBottom: 1,
  },

  subtitle: {
    fontSize: 15,
    fontWeight: '500',
    color: '#ffffff',
    marginBottom: 2,
  },

  metaRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },

  metaChip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },

  metaText: {
    fontSize: 13,
    color: '#ffffff',
    fontWeight: '400',
  },

  // ── Stats card (white) — in normal flow below tabs ──────────────────────────
  statsCard: {
    marginHorizontal: 20,
    marginTop: 8,
    backgroundColor: '#ffffff',
    borderRadius: 16,
    paddingHorizontal: 14,
    paddingVertical: 12,
    borderWidth: 1.5,
    borderColor: '#D1C4E9',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 8,
    elevation: 4,
    zIndex: 10,
    gap: 10,
  },

  // ── Pills row ─────────────────────────────────────────────────────────────────
  pillsRow: {
    flexDirection: 'row',
    gap: 8,
    alignSelf: 'stretch',
  },

  pill: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f5f3ff',
    borderRadius: 14,
    paddingHorizontal: 10,
    paddingVertical: 8,
    gap: 7,
    borderWidth: 1,
    borderColor: 'rgba(0,0,0,0.1)',
  },

  pillText: {
    fontSize: 15,
    fontWeight: '700',
    color: '#ffffff',
  },

  pillTextDark: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1e1b4b',
  },

  pillHeader: {
    fontSize: 9,
    fontWeight: '600',
    color: '#94a3b8',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    textAlign: 'center',
    minHeight: 24,
    lineHeight: 12,
  },



  // ── Stats row ────────────────────────────────────────────────────────────────
  statsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
  },

  statsPill: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 10,
    paddingVertical: 6,
    backgroundColor: '#f5f3ff',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(0,0,0,0.1)',
  },

  reactionMerged: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },

  reactionPts: {
    fontSize: 11,
    fontWeight: '700',
    color: '#ffffff',
    flexShrink: 1,
  },

  reactionPtsDark: {
    fontSize: 11,
    fontWeight: '700',
    color: '#1e1b4b',
    flexShrink: 1,
  },

  statCol: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 4,
    position: 'relative',
  },

  statInner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },

  statDivider: {
    position: 'absolute',
    left: 0,
    top: 6,
    bottom: 6,
    width: 1,
    backgroundColor: 'rgba(255,255,255,0.25)',
  },

  statValue: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#ffffff',
  },

  statIcon: {
    fontSize: 18,
  },

  statPts: {
    fontSize: 12,
    color: 'rgba(255,255,255,0.65)',
    marginTop: 3,
  },

  moodContainer: {
    paddingHorizontal: 16,
    marginTop: 16,
    marginBottom: 8,
  },

  // ── Tab selector — overlaps the header's rounded bottom edge ────────────────
  tabContainer: {
    paddingHorizontal: 20,
    marginTop: -18,
    zIndex: 10,
    marginBottom: 2,
  },

  tabPill: {
    flexDirection: 'row',
    backgroundColor: '#8E24AA',
    borderRadius: 20,
    padding: 4,
  },

  tabButton: {
    flex: 1,
    alignItems: 'center',
    paddingVertical: 10,
    borderRadius: 16,
  },

  tabButtonActive: {
    backgroundColor: '#ffffff',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.12,
    shadowRadius: 4,
    elevation: 3,
  },

  tabText: {
    fontSize: 13,
    fontWeight: '600',
    color: 'rgba(255,255,255,0.75)',
    letterSpacing: 0.3,
    textTransform: 'uppercase',
  },

  tabTextActive: {
    color: PURPLE,
  },

  // ── Content area ─────────────────────────────────────────────────────────────
  content: {
    paddingHorizontal: 16,
    paddingTop: 4,
  },

  // Skills card
  skillsCard: {
    backgroundColor: '#ffffff',
    borderRadius: 16,
    paddingVertical: 40,
    alignItems: 'center',
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 3,
  },

  skillsCardTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1e293b',
    marginTop: 12,
  },

  skillsCardSub: {
    fontSize: 13,
    color: '#94a3b8',
    marginTop: 4,
  },

  // Achievements card
  achieveCard: {
    backgroundColor: '#ffffff',
    borderRadius: 16,
    paddingVertical: 8,
    paddingHorizontal: 16,
    alignSelf: 'stretch',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 3,
  },

  achieveRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 14,
    gap: 12,
  },

  achieveIconWrap: {
    width: 40,
    height: 40,
    borderRadius: 20,
    alignItems: 'center',
    justifyContent: 'center',
  },

  achieveInfo: {
    flex: 1,
  },

  achieveLabel: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1e293b',
  },

  achieveSub: {
    fontSize: 11,
    color: '#94a3b8',
    marginTop: 2,
  },

  achieveValue: {
    fontSize: 18,
    fontWeight: '800',
    color: PURPLE,
  },

  achieveDivider: {
    height: 1,
    backgroundColor: '#f1f5f9',
  },

  progressBg: {
    height: 6,
    backgroundColor: '#f1f5f9',
    borderRadius: 3,
    marginBottom: 14,
    marginTop: 4,
    overflow: 'hidden',
  },

  progressFill: {
    height: '100%',
    borderRadius: 3,
  },

  // ── Dropdown ─────────────────────────────────────────────────────────────────
  dropdown: {
    position: 'absolute',
    top: Platform.OS === 'android' ? 56 : 52,
    left: 16,
    width: 230,
    backgroundColor: '#ffffff',
    borderRadius: 16,
    paddingVertical: 4,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.15,
    shadowRadius: 16,
    elevation: 12,
    zIndex: 999,
  },

  dropdownItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 13,
  },

  dropdownItemBorder: {
    borderBottomWidth: 1,
    borderBottomColor: '#f1f5f9',
  },

  dropdownItemLabel: {
    flex: 1,
    marginLeft: 12,
    fontSize: 14,
    color: '#334155',
    fontWeight: '500',
  },

  dropdownDivider: {
    height: 1,
    backgroundColor: '#f1f5f9',
    marginHorizontal: 16,
  },

  signOutLabel: {
    color: '#ef4444',
  },

  contentScroll: {
    flex: 1,
  },

  // ── Balance feed-style 2×2 grid ──────────────────────────────────────────────
  balanceFeedGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    marginBottom: 2,
  },

  balanceFeedCard: {
    width: '47.5%',
    borderRadius: 16,
    padding: 12,
    minHeight: 118,
    justifyContent: 'space-between',
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.25,
    shadowRadius: 8,
    elevation: 5,
  },

  balanceFeedHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginBottom: 2,
  },

  balanceFeedEmoji: {
    fontSize: 16,
  },

  balanceFeedTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: '#fff',
  },

  balanceFeedSub: {
    fontSize: 10,
    color: 'rgba(255,255,255,0.6)',
    fontWeight: '500',
    marginBottom: 4,
  },

  balanceFeedValue: {
    fontSize: 17,
    fontWeight: '900',
    color: '#fff',
  },

  balanceFeedTrack: {
    height: 4,
    backgroundColor: 'rgba(255,255,255,0.2)',
    borderRadius: 2,
    marginTop: 8,
    overflow: 'hidden',
  },

  balanceFeedFill: {
    height: 4,
    borderRadius: 2,
  },

  // ── Balance info sections ─────────────────────────────────────────────────────
  balanceSection: {
    marginTop: 16,
  },

  balanceSectionTitle: {
    fontSize: 10,
    fontWeight: '700',
    color: '#94a3b8',
    letterSpacing: 1,
    textTransform: 'uppercase',
    marginBottom: 8,
  },

  balanceInfoCard: {
    backgroundColor: '#ffffff',
    borderRadius: 14,
    paddingHorizontal: 14,
    paddingVertical: 4,
    borderWidth: 1,
    borderColor: '#e2e8f0',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 3,
  },

  balanceDescRow: {
    flexDirection: 'row',
    paddingVertical: 12,
    gap: 10,
  },

  balanceDescEmoji: {
    fontSize: 20,
    paddingTop: 1,
  },

  balanceDescText: {
    flex: 1,
  },

  balanceDescTitle: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1e293b',
    marginBottom: 3,
  },

  balanceDescBody: {
    fontSize: 11,
    color: '#64748b',
    lineHeight: 16,
  },

  balanceUsageRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
  },

  balanceUsageLabel: {
    flex: 1,
    fontSize: 13,
    color: '#334155',
    fontWeight: '500',
  },

  balanceUsageValue: {
    fontSize: 14,
    fontWeight: '800',
    color: PURPLE,
  },

  balanceActivitySub: {
    fontSize: 11,
    color: '#94a3b8',
    marginBottom: 8,
    marginTop: -4,
  },

  balanceBarChart: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: 6,
    paddingHorizontal: 8,
    paddingVertical: 12,
    height: 100,
  },

  balanceBarCol: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'flex-end',
    gap: 5,
  },

  balanceBarFill: {
    width: '100%',
    backgroundColor: PURPLE,
    borderRadius: 4,
    opacity: 0.85,
  },

  balanceBarLabel: {
    fontSize: 9,
    color: '#94a3b8',
    fontWeight: '600',
    textTransform: 'uppercase',
  },

  // ── Utilise vertical list cards ──────────────────────────────────────────────
  utiliseFeedCard: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 12,
    borderRadius: 16,
    padding: 14,
    marginBottom: 10,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.2,
    shadowRadius: 8,
    elevation: 5,
  },

  utiliseFeedIconWrap: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: 'rgba(255,255,255,0.15)',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },

  utiliseFeedIcon: {
    fontSize: 22,
  },

  utiliseFeedContent: {
    flex: 1,
  },

  utiliseFeedTitle: {
    fontSize: 14,
    fontWeight: '800',
    color: '#fff',
    marginBottom: 4,
  },

  rewardBreakdownCard: {
    backgroundColor: '#ffffff',
    borderRadius: 14,
    paddingHorizontal: 16,
    paddingVertical: 4,
    borderWidth: 1,
    borderColor: '#e2e8f0',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 3,
    marginTop: 10,
  },

  rewardBreakdownRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 14,
  },

  rewardBreakdownLabel: {
    fontSize: 14,
    color: '#334155',
    fontWeight: '500',
  },

  rewardBreakdownValue: {
    fontSize: 18,
    fontWeight: '800',
    color: PURPLE,
  },

  // Engage tab layout
  engageWrapper: {
    flex: 1,
  },
  engageHeroWrap: {
    paddingHorizontal: 16,
    paddingTop: 4,
  },

  // Engage tab breakdown
  engageBreakdownRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    gap: 10,
  },
  engageBreakdownEmoji: {
    fontSize: 22,
    width: 30,
    textAlign: 'center',
  },
  engageBreakdownContent: {
    flex: 1,
  },
  engageBreakdownTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1e1b4b',
    marginBottom: 2,
  },
  engageBreakdownDesc: {
    fontSize: 12,
    color: '#64748b',
    lineHeight: 16,
  },
  engageBreakdownValue: {
    fontSize: 20,
    fontWeight: '900',
    color: PURPLE,
    minWidth: 28,
    textAlign: 'right',
  },

  utiliseFeedBigCountWrap: {
    alignItems: 'center',
    alignSelf: 'center',
    flexShrink: 0,
  },

  utiliseFeedBigCount: {
    fontSize: 36,
    fontWeight: '900',
    color: '#fff',
    minWidth: 44,
    textAlign: 'right',
    opacity: 0.9,
  },

  utiliseFeedDesc: {
    fontSize: 12,
    color: 'rgba(255,255,255,0.8)',
    lineHeight: 17,
    marginBottom: 5,
  },

  utiliseFeedInsight: {
    fontSize: 11,
    color: 'rgba(255,255,255,0.5)',
    fontStyle: 'italic',
    lineHeight: 15,
  },

  convertBtn: {
    backgroundColor: 'rgba(255,255,255,0.2)',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 8,
    alignSelf: 'center',
    minWidth: 80,
    alignItems: 'center',
  },
  convertBtnTxt: {
    color: '#fff',
    fontSize: 13,
    fontWeight: '700',
  },

  emojiValuesRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },

  emojiCountItem: {
    fontSize: 12,
    fontWeight: '700',
    color: PURPLE,
  },

  // ── Balance grid ──────────────────────────────────────────────────────────────
  balanceGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
  },

  balanceCard: {
    width: '47.5%',
    backgroundColor: '#ffffff',
    borderRadius: 14,
    padding: 14,
    borderWidth: 1,
    borderColor: '#e2e8f0',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 3,
  },

  balanceCardHighlight: {
    borderColor: '#D1C4E9',
    backgroundColor: '#f5f3ff',
  },

  balanceCardLabel: {
    fontSize: 10,
    fontWeight: '600',
    color: '#94a3b8',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: 6,
  },

  balanceCardValue: {
    fontSize: 26,
    fontWeight: '800',
    color: '#1e293b',
    marginBottom: 4,
  },

  balanceTrendRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },

  balanceTrendUp: {
    fontSize: 11,
    fontWeight: '600',
    color: '#059669',
  },

  balanceTrendDown: {
    fontSize: 11,
    fontWeight: '600',
    color: '#ef4444',
  },

  balanceTrendNeutral: {
    fontSize: 11,
    fontWeight: '500',
    color: '#94a3b8',
  },
});
