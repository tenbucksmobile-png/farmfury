import React from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  Linking,
} from 'react-native';
import { Image } from 'expo-image';
import { Stack, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useEmployee } from '@/providers/EmployeeContext';
import { useCampaigns } from '@/hooks/use-campaigns';
import type { Campaign } from '@/api/campaigns-service';

// ─── Constants ────────────────────────────────────────────────────────────────

const PURPLE     = '#7B1FA2';
const PURPLE_MID = '#9C27B0';
const AMBER      = '#D97706';
const AMBER_SOFT = '#FEF3C7';
const FUCHSIA    = '#A21CAF';
const FUCHSIA_SOFT = '#FAE8FF';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    day: 'numeric', month: 'short', year: 'numeric',
  });
}

function daysLabel(days: number): string {
  if (days <= 0) return 'Ends today';
  if (days === 1) return '1 day left';
  return `${days} days left`;
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function SponsorCard({ campaign }: { campaign: Campaign }) {
  function openLink() {
    if (campaign.banner_link_url) {
      Linking.openURL(campaign.banner_link_url).catch(() => {});
    }
  }

  return (
    <TouchableOpacity
      activeOpacity={campaign.banner_link_url ? 0.85 : 1}
      onPress={campaign.banner_link_url ? openLink : undefined}
      style={styles.sponsorCard}
    >
      {/* Banner image */}
      {campaign.banner_url ? (
        <Image
          source={{ uri: campaign.banner_url }}
          style={styles.bannerImage}
          contentFit="cover"
        />
      ) : (
        <View style={styles.bannerPlaceholder}>
          <Ionicons name="megaphone-outline" size={40} color={AMBER} />
        </View>
      )}

      <View style={styles.sponsorBody}>
        <View style={styles.sponsorHeader}>
          <View style={styles.sponsorBadge}>
            <Ionicons name="megaphone-outline" size={12} color={AMBER} />
            <Text style={styles.sponsorBadgeText}>Sponsored</Text>
          </View>
          {campaign.is_active && (
            <View style={styles.activeDot} />
          )}
        </View>

        <Text style={styles.sponsorName}>{campaign.sponsor_name ?? campaign.title}</Text>
        <Text style={styles.campaignTitle}>{campaign.title}</Text>

        {campaign.voucher_description ? (
          <View style={styles.voucherBox}>
            <Ionicons name="gift-outline" size={14} color={AMBER} />
            <Text style={styles.voucherText}>{campaign.voucher_description}</Text>
          </View>
        ) : null}

        <View style={styles.cardFooter}>
          <Text style={styles.dateText}>
            {formatDate(campaign.start_date)} – {formatDate(campaign.end_date)}
          </Text>
          {campaign.is_active && campaign.days_remaining >= 0 && (
            <Text style={styles.daysLeftText}>{daysLabel(campaign.days_remaining)}</Text>
          )}
        </View>

        {campaign.banner_link_url && (
          <TouchableOpacity onPress={openLink} style={styles.learnMore}>
            <Text style={styles.learnMoreText}>Learn more</Text>
            <Ionicons name="arrow-forward" size={14} color={AMBER} />
          </TouchableOpacity>
        )}
      </View>
    </TouchableOpacity>
  );
}

function RecognitionCard({ campaign }: { campaign: Campaign }) {
  return (
    <View style={styles.recognitionCard}>
      <View style={styles.recognitionHeader}>
        <View style={styles.multiplierBadge}>
          <Ionicons name="flash" size={14} color={FUCHSIA} />
          <Text style={styles.multiplierText}>{campaign.points_multiplier}×</Text>
        </View>
        {campaign.is_active && <View style={styles.activeDot} />}
      </View>

      <Text style={styles.recTitle}>{campaign.title}</Text>
      {campaign.description ? (
        <Text style={styles.recDescription}>{campaign.description}</Text>
      ) : null}

      <View style={styles.recPoints}>
        <Ionicons name="star" size={14} color={FUCHSIA} />
        <Text style={styles.recPointsText}>
          Earn {campaign.points_multiplier * 10} pts per recognition (base: 10)
        </Text>
      </View>

      <View style={styles.cardFooter}>
        <Text style={styles.dateText}>
          {formatDate(campaign.start_date)} – {formatDate(campaign.end_date)}
        </Text>
        {campaign.is_active && campaign.days_remaining >= 0 && (
          <Text style={styles.daysLeftText}>{daysLabel(campaign.days_remaining)}</Text>
        )}
      </View>
    </View>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function CampaignsScreen() {
  const { employee } = useEmployee();
  const hotel = employee?.hotel ?? '';

  const { data: campaigns = [], isLoading, isError } = useCampaigns(hotel);

  const sponsorCampaigns     = campaigns.filter((c) => c.type === 'sponsor' || c.type === 'both');
  const recognitionCampaigns = campaigns.filter((c) => c.type === 'recognition' || c.type === 'both');

  const hasSponsors     = sponsorCampaigns.length > 0;
  const hasRecognitions = recognitionCampaigns.filter((c) => c.type === 'recognition').length > 0;

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
            <Text style={styles.title}>Campaigns</Text>
            {hotel ? <Text style={styles.hotelName}>{hotel}</Text> : null}
          </View>
          <View style={{ width: 38 }} />
        </View>
        <Text style={styles.subtitle}>Active promotions and sponsor offers</Text>
      </View>

      {/* Body */}
      {isLoading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={PURPLE} />
        </View>
      ) : isError ? (
        <View style={styles.center}>
          <Ionicons name="alert-circle-outline" size={48} color="#e2d9f3" />
          <Text style={styles.emptyTitle}>Could not load campaigns</Text>
          <Text style={styles.emptyText}>Please check your connection and try again.</Text>
        </View>
      ) : campaigns.length === 0 ? (
        <View style={styles.center}>
          <Ionicons name="megaphone-outline" size={56} color="#e2d9f3" />
          <Text style={styles.emptyTitle}>No active campaigns</Text>
          <Text style={styles.emptyText}>Check back soon for promotions and offers.</Text>
        </View>
      ) : (
        <ScrollView contentContainerStyle={styles.body} showsVerticalScrollIndicator={false}>

          {/* Sponsor banners */}
          {hasSponsors && (
            <View style={styles.section}>
              <Text style={styles.sectionLabel}>Sponsor Offers</Text>
              {sponsorCampaigns.map((c) => (
                <SponsorCard key={c.id} campaign={c} />
              ))}
            </View>
          )}

          {/* Recognition campaigns */}
          {hasRecognitions && (
            <View style={styles.section}>
              <Text style={styles.sectionLabel}>Recognition Campaigns</Text>
              {recognitionCampaigns
                .filter((c) => c.type === 'recognition')
                .map((c) => (
                  <RecognitionCard key={c.id} campaign={c} />
                ))}
            </View>
          )}

        </ScrollView>
      )}
    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: PURPLE,
  },

  // ── Header
  header: {
    backgroundColor: PURPLE,
    paddingHorizontal: 20,
    paddingBottom: 16,
    paddingTop: 8,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    marginBottom: 6,
  },
  backBtn: {
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: 'rgba(255,255,255,0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: {
    fontSize: 22,
    fontWeight: '700',
    color: '#fff',
  },
  hotelName: {
    fontSize: 13,
    color: 'rgba(255,255,255,0.75)',
    marginTop: 1,
  },
  subtitle: {
    fontSize: 13,
    color: 'rgba(255,255,255,0.7)',
    marginLeft: 50,
  },

  // ── Body
  body: {
    padding: 16,
    paddingBottom: 40,
    backgroundColor: '#F5F3FF',
  },
  center: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    backgroundColor: '#F5F3FF',
    padding: 24,
  },
  emptyTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#6b21a8',
    marginTop: 8,
  },
  emptyText: {
    fontSize: 13,
    color: '#9e6fc4',
    textAlign: 'center',
  },

  // ── Section
  section: {
    marginBottom: 24,
  },
  sectionLabel: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.8,
    textTransform: 'uppercase',
    color: '#9e6fc4',
    marginBottom: 10,
  },

  // ── Sponsor card
  sponsorCard: {
    backgroundColor: '#fff',
    borderRadius: 14,
    overflow: 'hidden',
    marginBottom: 14,
    shadowColor: '#000',
    shadowOpacity: 0.07,
    shadowRadius: 8,
    shadowOffset: { width: 0, height: 2 },
    elevation: 3,
  },
  bannerImage: {
    width: '100%',
    height: 160,
  },
  bannerPlaceholder: {
    width: '100%',
    height: 120,
    backgroundColor: AMBER_SOFT,
    alignItems: 'center',
    justifyContent: 'center',
  },
  sponsorBody: {
    padding: 14,
    gap: 8,
  },
  sponsorHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  sponsorBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: AMBER_SOFT,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 20,
  },
  sponsorBadgeText: {
    fontSize: 11,
    fontWeight: '600',
    color: AMBER,
  },
  activeDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#22c55e',
  },
  sponsorName: {
    fontSize: 17,
    fontWeight: '700',
    color: '#1a1a1a',
  },
  campaignTitle: {
    fontSize: 13,
    color: '#555',
  },
  voucherBox: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 6,
    backgroundColor: AMBER_SOFT,
    borderRadius: 8,
    padding: 10,
  },
  voucherText: {
    flex: 1,
    fontSize: 13,
    color: '#92400e',
    lineHeight: 18,
  },
  cardFooter: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 2,
  },
  dateText: {
    fontSize: 12,
    color: '#888',
  },
  daysLeftText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#22c55e',
  },
  learnMore: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    alignSelf: 'flex-start',
    marginTop: 2,
  },
  learnMoreText: {
    fontSize: 13,
    fontWeight: '600',
    color: AMBER,
  },

  // ── Recognition card
  recognitionCard: {
    backgroundColor: '#fff',
    borderRadius: 14,
    padding: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOpacity: 0.06,
    shadowRadius: 6,
    shadowOffset: { width: 0, height: 2 },
    elevation: 2,
    gap: 8,
  },
  recognitionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  multiplierBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: FUCHSIA_SOFT,
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 20,
  },
  multiplierText: {
    fontSize: 14,
    fontWeight: '800',
    color: FUCHSIA,
  },
  recTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#1a1a1a',
  },
  recDescription: {
    fontSize: 13,
    color: '#666',
    lineHeight: 18,
  },
  recPoints: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    backgroundColor: FUCHSIA_SOFT,
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 7,
  },
  recPointsText: {
    fontSize: 13,
    fontWeight: '600',
    color: FUCHSIA,
  },
});
