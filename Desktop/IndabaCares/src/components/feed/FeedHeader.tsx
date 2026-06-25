import React, { useState, useRef, useEffect } from 'react';
import { View, Text, TextInput, Pressable, ScrollView, StyleSheet, Modal } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useEmployee } from '@/providers/EmployeeContext';
import { useSubmitMood } from '@/hooks/use-mood';
import { RECOGNITION_BADGES, MOOD_MAP, type MoodValue } from '@/lib/constants';

const MOOD_STORAGE_KEY = 'daily_mood';

const PURPLE      = '#7B1FA2';
const PURPLE_SOFT = 'rgba(255,255,255,0.15)';
const PURPLE_TINT = '#ede9fe';

// ─── Filter types ─────────────────────────────────────────────────────────────

export type FilterCategory = 'latest' | 'badge' | 'department';

export interface FeedFilter {
  category: FilterCategory;
  value:    string;
}

const DEPARTMENTS = [
  'Front Office', 'Food & Beverage', 'Housekeeping', 'Reservations',
  'Spa & Wellness', 'Maintenance', 'Accounts', 'Main Kitchen',
  'Chiefs Boma', 'Banqueting & Conventions', 'Sales & Marketing',
  'Security', 'Landscaping', 'BoyangGape',
];

// ─── Props ────────────────────────────────────────────────────────────────────

interface FeedHeaderProps {
  searchTerm:     string;
  onSearchChange: (term: string) => void;
  activeFilter:   FeedFilter | null;
  onFilterChange: (filter: FeedFilter | null) => void;
}

// ─── Checkbox row ─────────────────────────────────────────────────────────────

function CheckRow({
  label, emoji, checked, onPress,
}: { label: string; emoji?: string; checked: boolean; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} style={cb.row}>
      <Ionicons
        name={checked ? 'checkbox' : 'square-outline'}
        size={18}
        color={checked ? PURPLE : '#94a3b8'}
      />
      {emoji && <Text style={cb.emoji}>{emoji}</Text>}
      <Text style={[cb.label, checked && cb.labelChecked]}>{label}</Text>
    </Pressable>
  );
}

const cb = StyleSheet.create({
  row:          { flexDirection: 'row', alignItems: 'center', paddingVertical: 7, gap: 10 },
  emoji:        { fontSize: 13 },
  label:        { fontSize: 13, color: '#475569', flex: 1 },
  labelChecked: { color: PURPLE, fontWeight: '700' },
});

// ─── Component ───────────────────────────────────────────────────────────────

export function FeedHeader({ searchTerm, onSearchChange, activeFilter, onFilterChange }: FeedHeaderProps) {
  const { employee } = useEmployee();
  const submitMood   = useSubmitMood();
  const [searchOpen,    setSearchOpen]    = useState(false);
  const [filterOpen,    setFilterOpen]    = useState(false);
  const [pickerVisible, setPickerVisible] = useState(false);
  const [moodEmoji,     setMoodEmoji]     = useState<string | null>(null);
  const inputRef = useRef<TextInput>(null);

  const today     = new Date().toISOString().split('T')[0];
  const firstName = employee?.full_name.split(' ')[0];
  const hour      = new Date().getHours();
  const greeting  =
    hour < 12 ? 'Good Morning' : hour < 17 ? 'Good Afternoon' : 'Good Evening';

  // Load stored mood on mount; clear if it's from a previous day
  useEffect(() => {
    AsyncStorage.getItem(MOOD_STORAGE_KEY).then((raw) => {
      if (!raw) return;
      try {
        const { date, emoji } = JSON.parse(raw);
        if (date === today) setMoodEmoji(emoji);
        else AsyncStorage.removeItem(MOOD_STORAGE_KEY);
      } catch {
        AsyncStorage.removeItem(MOOD_STORAGE_KEY);
      }
    });
  }, [today]);

  function handlePickMood(key: MoodValue) {
    const emoji = MOOD_MAP[key].emoji;
    setMoodEmoji(emoji);
    AsyncStorage.setItem(MOOD_STORAGE_KEY, JSON.stringify({ date: today, emoji }));
    setPickerVisible(false);
    submitMood.mutate({ mood: key });
  }

  function openSearch() {
    setSearchOpen(true);
    setTimeout(() => inputRef.current?.focus(), 50);
  }

  function closeSearch() {
    setSearchOpen(false);
    onSearchChange('');
    inputRef.current?.blur();
  }

  function handlePick(category: FilterCategory, value: string) {
    const same = activeFilter?.category === category && activeFilter?.value === value;
    onFilterChange(same ? null : { category, value });
  }

  function isChecked(category: FilterCategory, value: string) {
    return activeFilter?.category === category && activeFilter?.value === value;
  }

  const hasFilter = !!activeFilter;

  return (
    <View style={styles.container}>
      <View style={styles.header}>

        {/* Greeting row */}
        <View style={styles.greetingRow}>
          {firstName ? (
            <View style={{ flex: 1 }}>
              <Text style={styles.greeting}>{greeting}, {firstName}!</Text>
              <Text style={styles.subtext}>Let's make today great</Text>
            </View>
          ) : null}
          <Pressable
            onPress={() => !moodEmoji && setPickerVisible(true)}
            hitSlop={8}
            style={styles.moodBtn}
          >
            {moodEmoji
              ? <Text style={styles.moodBadge}>{moodEmoji}</Text>
              : <Ionicons name="help-circle-outline" size={36} color="rgba(255,255,255,0.7)" />
            }
          </Pressable>
        </View>

        {/* Emoji picker modal */}
        <Modal
          visible={pickerVisible}
          transparent
          animationType="fade"
          onRequestClose={() => setPickerVisible(false)}
        >
          <Pressable style={styles.backdrop} onPress={() => setPickerVisible(false)}>
            <Pressable style={styles.pickerCard} onPress={(e) => e.stopPropagation()}>
              <View style={styles.pickerTitleRow}>
                <Text style={styles.pickerTitle}>How are you feeling?</Text>
                <View style={styles.ptsBadge}>
                  <Text>⭐</Text>
                  <Text style={styles.ptsText}> 5 pts</Text>
                </View>
              </View>
              <View style={styles.emojiRow}>
                {(Object.entries(MOOD_MAP) as [MoodValue, typeof MOOD_MAP[MoodValue]][]).map(([key, val]) => (
                  <Pressable
                    key={key}
                    onPress={() => handlePickMood(key)}
                    style={({ pressed }) => [styles.emojiBtn, pressed && { backgroundColor: val.color + '22' }]}
                  >
                    <Text style={styles.emojiText}>{val.emoji}</Text>
                    <Text style={[styles.emojiLabel, { color: val.color }]}>{val.label}</Text>
                  </Pressable>
                ))}
              </View>
            </Pressable>
          </Pressable>
        </Modal>

      </View>

    {/* ── Search + filter row — below purple backdrop ───────────── */}
    <View style={styles.searchRow}>
      <Pressable onPress={searchOpen ? closeSearch : openSearch} style={styles.iconBtn} hitSlop={8}>
        <Ionicons name={searchOpen ? 'close' : 'search-outline'} size={20} color={PURPLE} />
      </Pressable>

      {searchOpen && (
        <View style={styles.searchBox}>
          <TextInput
            ref={inputRef}
            style={styles.searchInput}
            placeholder="Search by Name or Department"
            placeholderTextColor="#a78bca"
            value={searchTerm}
            onChangeText={onSearchChange}
            returnKeyType="search"
            autoCorrect={false}
            autoCapitalize="none"
          />
          {searchTerm.length > 0 && (
            <Pressable onPress={() => onSearchChange('')} hitSlop={6}>
              <Ionicons name="close-circle" size={16} color="#a78bca" />
            </Pressable>
          )}
        </View>
      )}

      {!searchOpen && <View style={{ flex: 1 }} />}

      <Pressable
        onPress={() => setFilterOpen((v) => !v)}
        style={[styles.iconBtn, hasFilter && styles.iconBtnActive]}
        hitSlop={8}
      >
        <Ionicons name="options-outline" size={20} color={hasFilter ? '#ffffff' : PURPLE} />
      </Pressable>
    </View>

    {/* ── Inline checkbox dropdown ──────────────────────────────── */}
    {filterOpen && (
      <View style={styles.dropdown}>
        <ScrollView showsVerticalScrollIndicator={false} nestedScrollEnabled>

          {/* Sort */}
          <Text style={styles.groupLabel}>Sort</Text>
          <CheckRow
            label="Latest"
            emoji="🕐"
            checked={isChecked('latest', 'latest')}
            onPress={() => handlePick('latest', 'latest')}
          />

          <View style={styles.divider} />

          {/* Badges */}
          <Text style={styles.groupLabel}>Badge</Text>
          {RECOGNITION_BADGES.map((b) => (
            <CheckRow
              key={b.value}
              label={b.value}
              emoji={b.emoji}
              checked={isChecked('badge', b.value)}
              onPress={() => handlePick('badge', b.value)}
            />
          ))}

          <View style={styles.divider} />

          {/* Departments */}
          <Text style={styles.groupLabel}>Department</Text>
          {DEPARTMENTS.map((dept) => (
            <CheckRow
              key={dept}
              label={dept}
              checked={isChecked('department', dept)}
              onPress={() => handlePick('department', dept)}
            />
          ))}

        </ScrollView>

        {/* Clear button */}
        {hasFilter && (
          <Pressable onPress={() => { onFilterChange(null); setFilterOpen(false); }} style={styles.clearBtn}>
            <Text style={styles.clearText}>Clear filter</Text>
          </Pressable>
        )}
      </View>
    )}

  </View>
  );
}

// ─── Styles ──────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { marginBottom: 0, zIndex: 50 },

  header: {
    backgroundColor: PURPLE,
    borderBottomLeftRadius: 30,
    borderBottomRightRadius: 30,
    paddingHorizontal: 20,
    paddingTop: 20,
    paddingBottom: 18,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.15,
    shadowRadius: 12,
    elevation: 8,
  },

  greetingRow: { flexDirection: 'row', alignItems: 'center', marginBottom: 12 },
  greeting:    { fontSize: 22, fontWeight: 'bold', color: '#ffffff' },
  subtext:     { fontSize: 13, color: 'rgba(255,255,255,0.65)', marginTop: 2 },
  moodBtn:     { marginLeft: 8 },
  moodBadge:   { fontSize: 36 },

  backdrop:       { flex: 1, backgroundColor: 'rgba(0,0,0,0.45)', justifyContent: 'center', alignItems: 'center' },
  pickerCard:     { backgroundColor: '#fff', borderRadius: 24, paddingHorizontal: 20, paddingTop: 22, paddingBottom: 20, width: '88%', shadowColor: '#000', shadowOffset: { width: 0, height: 8 }, shadowOpacity: 0.18, shadowRadius: 20, elevation: 12 },
  pickerTitleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, marginBottom: 18 },
  pickerTitle:    { fontSize: 17, fontWeight: '800', color: '#111827', textAlign: 'center' },
  ptsBadge:       { flexDirection: 'row', alignItems: 'center', backgroundColor: '#fef9c3', borderRadius: 10, paddingHorizontal: 8, paddingVertical: 3 },
  ptsText:        { fontSize: 12, fontWeight: '700', color: '#92400e' },
  emojiRow:       { flexDirection: 'row', justifyContent: 'space-between' },
  emojiBtn:       { alignItems: 'center', borderRadius: 12, paddingVertical: 10, paddingHorizontal: 8, flex: 1 },
  emojiText:      { fontSize: 34 },
  emojiLabel:     { fontSize: 10, marginTop: 5, fontWeight: '600' },

  searchRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 20,
    paddingTop: 12,
    paddingBottom: 4,
    gap: 10,
  },

  iconBtn: {
    width: 38, height: 38, borderRadius: 12,
    backgroundColor: '#ede9fe',
    alignItems: 'center', justifyContent: 'center',
  },
  iconBtnActive: { backgroundColor: PURPLE },

  searchBox: {
    flex: 1, flexDirection: 'row', alignItems: 'center',
    backgroundColor: '#ffffff', borderRadius: 14,
    paddingHorizontal: 12, paddingVertical: 9, gap: 8,
  },
  searchInput: { flex: 1, fontSize: 14, color: '#1e1b4b', padding: 0 },

  // ── Dropdown ───────────────────────────────────────────────────────────────
  dropdown: {
    position: 'absolute',
    top: 152,
    left: 20,
    right: 20,
    backgroundColor: '#fff',
    borderRadius: 16,
    paddingHorizontal: 14,
    paddingTop: 10,
    paddingBottom: 6,
    maxHeight: 320,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.15,
    shadowRadius: 12,
    elevation: 20,
    zIndex: 100,
  },

  groupLabel: {
    fontSize: 10,
    fontWeight: '700',
    color: '#94a3b8',
    letterSpacing: 0.8,
    textTransform: 'uppercase',
    marginBottom: 2,
    marginTop: 4,
  },

  divider: { height: 1, backgroundColor: '#f1f5f9', marginVertical: 8 },

  clearBtn: {
    borderTopWidth: 1,
    borderTopColor: '#f1f5f9',
    paddingVertical: 10,
    alignItems: 'center',
  },
  clearText: { fontSize: 13, fontWeight: '700', color: '#ef4444' },
});
