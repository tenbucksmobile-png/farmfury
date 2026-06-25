import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  Pressable,
  StyleSheet,
  LayoutAnimation,
  Platform,
  UIManager,
} from 'react-native';
import { Stack, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';

if (Platform.OS === 'android') {
  UIManager.setLayoutAnimationEnabledExperimental?.(true);
}

const PURPLE      = '#7B1FA2';
const PURPLE_SOFT = '#ede9fe';
const PURPLE_MID  = '#ddd6fe';

// ─── FAQ Data ─────────────────────────────────────────────────────────────────

const FAQS = [
  {
    category: 'Recognition',
    icon: 'star-outline' as const,
    items: [
      {
        q: 'How do I recognise a colleague?',
        a: 'Tap the "Give" tab at the bottom of the screen. Select a colleague, choose a recognition badge, write a message, and submit. They\'ll be notified immediately.',
      },
      {
        q: 'How many recognitions can I give per day?',
        a: 'This depends on your role and your hotel\'s budget configuration. Generally, employees can give up to 5 recognitions per day. Managers may have a higher allowance.',
      },
      {
        q: 'Can I react to a recognition on the feed?',
        a: 'Yes! On the home feed, tap the ❤️, 😊, or 👍 reaction buttons on any recognition card. Each reaction awards a small number of points to the recipient.',
      },
      {
        q: 'What are badges?',
        a: 'Badges are recognition categories that describe the type of achievement being celebrated. When you recognise a colleague, you select a badge that best fits the moment — for example, Hospitality Hero, Leadership, Innovation, or Going the Extra Mile. Badges appear on the feed card alongside your message.',
      },
    ],
  },
  {
    category: 'Rewards & Points',
    icon: 'gift-outline' as const,
    items: [
      {
        q: 'How do reward points work?',
        a: 'Points are the currency of IndabaCares. You earn them by receiving recognitions from colleagues and managers, and by reacting to posts on the feed. Once you accumulate enough points, you can spend them in the Rewards catalogue on vouchers, merchandise, experiences, and more.',
      },
      {
        q: 'How do I earn points?',
        a: 'You earn points in several ways:\n• Receiving a peer recognition — points awarded depend on the badge type.\n• Receiving reaction emojis (❤️, 😊, 👍) on your recognitions — each emoji type awards a different number of points.\n• Manager or admin boosts can award bonus points directly.',
      },
      {
        q: 'How many points is each reaction worth?',
        a: '❤️ Heart — 20 pts each\n😊 Smiley — 15 pts each\n👍 Thumbs Up — 10 pts each\n\nPoints are added to your balance each time someone reacts to a recognition you received.',
      },
      {
        q: 'Do my points expire?',
        a: 'Points do not expire automatically. However, your hotel\'s admin team may apply a reset policy. Check with your HR administrator if you are unsure about your hotel\'s specific rules.',
      },
      {
        q: 'How do I redeem my points?',
        a: 'Go to the Rewards tab, browse the catalogue, and tap any reward you can afford. Confirm your redemption and your request will be sent to the admin team for fulfilment.',
      },
      {
        q: 'How long does reward fulfilment take?',
        a: 'Digital rewards (vouchers) are typically processed within 1–2 business days. Physical items may take 3–5 business days depending on stock and delivery.',
      },
      {
        q: 'My points balance looks incorrect — what do I do?',
        a: 'Pull down on the home feed to refresh your data. If the issue persists, contact your HR administrator who can review your points ledger.',
      },
    ],
  },
  {
    category: 'Status & Tiers',
    icon: 'trophy-outline' as const,
    items: [
      {
        q: 'What is my Status?',
        a: 'Your Status reflects how many recognitions you have received from colleagues in the current week. It resets every week and encourages consistent engagement.',
      },
      {
        q: 'How do I reach Bronze, Silver, or Gold?',
        a: 'Bronze: receive 5 or more recognitions in a week.\nSilver: receive 20 or more recognitions in a week.\nGold: receive 50 or more recognitions in a week.\n\nYour trophy icon and colour update automatically as your status changes.',
      },
      {
        q: 'Does my status reset?',
        a: 'Yes — status is based on your weekly recognition count and resets at the start of each new week. Keep engaging with your team to maintain or improve your tier!',
      },
    ],
  },
  {
    category: 'Profile & Account',
    icon: 'person-outline' as const,
    items: [
      {
        q: 'How do I update my profile photo?',
        a: 'Go to your Profile tab and tap your avatar. You can take a new photo or choose one from your gallery. It will update immediately across the app.',
      },
      {
        q: 'How do I sign out?',
        a: 'Go to your Profile tab, tap the menu icon (☰) in the top right corner, and select "Sign Out".',
      },
      {
        q: 'Who can see my recognition feed activity?',
        a: 'All employees at your hotel can see recognitions on the home feed. Your profile stats are visible to colleagues. Personal details like your employee code are private.',
      },
    ],
  },
];

// ─── Accordion Item ───────────────────────────────────────────────────────────

function AccordionItem({ q, a }: { q: string; a: string }) {
  const [open, setOpen] = useState(false);

  const toggle = () => {
    LayoutAnimation.configureNext(LayoutAnimation.Presets.easeInEaseOut);
    setOpen((o) => !o);
  };

  return (
    <Pressable onPress={toggle} style={s.accordionItem}>
      <View style={s.accordionHeader}>
        <Text style={s.question}>{q}</Text>
        <Ionicons
          name={open ? 'chevron-up' : 'chevron-down'}
          size={16}
          color={PURPLE}
        />
      </View>
      {open && <Text style={s.answer}>{a}</Text>}
    </Pressable>
  );
}

// ─── Category Section ─────────────────────────────────────────────────────────

function CategorySection({
  category,
  icon,
  items,
}: {
  category: string;
  icon: React.ComponentProps<typeof Ionicons>['name'];
  items: { q: string; a: string }[];
}) {
  return (
    <View style={s.section}>
      <View style={s.categoryHeader}>
        <View style={s.categoryIcon}>
          <Ionicons name={icon} size={16} color={PURPLE} />
        </View>
        <Text style={s.categoryTitle}>{category}</Text>
      </View>
      <View style={s.card}>
        {items.map((item, i) => (
          <View key={i}>
            <AccordionItem q={item.q} a={item.a} />
            {i < items.length - 1 && <View style={s.divider} />}
          </View>
        ))}
      </View>
    </View>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function FaqScreen() {
  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      {/* Header */}
      <View style={s.header}>
        <Pressable onPress={() => router.back()} style={s.backBtn}>
          <Ionicons name="chevron-back" size={22} color="#fff" />
        </Pressable>
        <Text style={s.headerTitle}>FAQs</Text>
        <View style={{ width: 36 }} />
      </View>

      {/* Hero strip */}
      <View style={s.hero}>
        <View style={s.heroIcon}>
          <Ionicons name="help-circle" size={28} color={PURPLE} />
        </View>
        <View style={{ flex: 1 }}>
          <Text style={s.heroTitle}>How can we help?</Text>
          <Text style={s.heroSub}>Browse answers to common questions below.</Text>
        </View>
      </View>

      <ScrollView
        style={s.scroll}
        contentContainerStyle={s.scrollContent}
        showsVerticalScrollIndicator={false}
      >
        {FAQS.map((section) => (
          <CategorySection
            key={section.category}
            category={section.category}
            icon={section.icon}
            items={section.items}
          />
        ))}

        {/* Footer */}
        <View style={s.footer}>
          <Ionicons name="mail-outline" size={16} color="#94a3b8" />
          <Text style={s.footerText}>
            Still need help? Contact your HR administrator.
          </Text>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const s = StyleSheet.create({
  safe: { flex: 1, backgroundColor: PURPLE },

  header: {
    flexDirection:  'row',
    alignItems:     'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical:   12,
    backgroundColor:   PURPLE,
  },
  backBtn: {
    width: 36, height: 36,
    borderRadius: 18,
    backgroundColor: 'rgba(255,255,255,0.15)',
    alignItems: 'center', justifyContent: 'center',
  },
  headerTitle: {
    fontSize: 18, fontWeight: '700', color: '#fff',
  },

  hero: {
    flexDirection:     'row',
    alignItems:        'center',
    gap:               12,
    marginHorizontal:  16,
    marginBottom:      16,
    backgroundColor:   'rgba(255,255,255,0.12)',
    borderRadius:      16,
    padding:           16,
  },
  heroIcon: {
    width: 48, height: 48,
    borderRadius: 24,
    backgroundColor: '#fff',
    alignItems: 'center', justifyContent: 'center',
  },
  heroTitle: { fontSize: 16, fontWeight: '700', color: '#fff' },
  heroSub:   { fontSize: 13, color: 'rgba(255,255,255,0.7)', marginTop: 2 },

  scroll:        { flex: 1, backgroundColor: '#f8f7ff' },
  scrollContent: { padding: 16, paddingBottom: 48 },

  section:  { marginBottom: 20 },

  categoryHeader: {
    flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 8,
  },
  categoryIcon: {
    width: 28, height: 28, borderRadius: 8,
    backgroundColor: PURPLE_SOFT,
    alignItems: 'center', justifyContent: 'center',
  },
  categoryTitle: {
    fontSize: 13, fontWeight: '700', color: PURPLE, textTransform: 'uppercase', letterSpacing: 0.5,
  },

  card: {
    backgroundColor: '#fff',
    borderRadius:    16,
    overflow:        'hidden',
    shadowColor:     '#7B1FA2',
    shadowOffset:    { width: 0, height: 2 },
    shadowOpacity:   0.06,
    shadowRadius:    8,
    elevation:       2,
  },

  accordionItem:   { padding: 16 },
  accordionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  question:        { flex: 1, fontSize: 14, fontWeight: '600', color: '#1e1b4b', lineHeight: 20 },
  answer:          { fontSize: 14, color: '#64748b', lineHeight: 22, marginTop: 10 },

  divider: { height: 1, backgroundColor: '#f1f5f9', marginHorizontal: 16 },

  footer: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    gap: 6, marginTop: 8,
  },
  footerText: { fontSize: 12, color: '#94a3b8', textAlign: 'center' },
});
