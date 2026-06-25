import React, { useState, useCallback } from 'react';
import {
  View,
  Text,
  TextInput,
  Pressable,
  TouchableOpacity,
  ScrollView,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  StyleSheet,
} from 'react-native';
import { router, Stack } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useQuery } from '@tanstack/react-query';
import { Avatar } from '@/components/ui/Avatar';
import { usePostRecognition } from '@/hooks/use-post-recognition';
import { searchEmployeesQuery } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

const SKILL_BADGES = [
  { value: 'Leadership',       emoji: '👑', color: '#F59E0B' },
  { value: 'Teamwork',         emoji: '🤝', color: '#3B82F6' },
  { value: 'Communication',    emoji: '💬', color: '#10B981' },
  { value: 'Problem Solving',  emoji: '🧩', color: '#8B5CF6' },
  { value: 'Customer Service', emoji: '🌟', color: '#EC4899' },
  { value: 'Creativity',       emoji: '💡', color: '#F97316' },
  { value: 'Reliability',      emoji: '⏰', color: '#06B6D4' },
  { value: 'Positivity',       emoji: '😊', color: '#84CC16' },
] as const;

type SkillBadge = typeof SKILL_BADGES[number]['value'];

const PURPLE      = '#7B1FA2';
const PURPLE_SOFT = '#ede9fe';
const PURPLE_TINT = '#ddd6fe';

interface EmployeeResult {
  id: string;
  full_name: string;
  employee_code: string;
  position: string | null;
  department: string | null;
}

function EmployeeSearch({ hotel, selected, onSelect, onClear }: {
  hotel: string;
  selected: EmployeeResult | null;
  onSelect: (e: EmployeeResult) => void;
  onClear: () => void;
}) {
  const [query, setQuery] = useState('');
  const [focused, setFocused] = useState(false);

  const { data: results = [], isFetching } = useQuery({
    queryKey: QUERY_KEYS.employees(query),
    queryFn: async () => {
      if (query.length < 2) return [];
      const { data, error } = await searchEmployeesQuery(hotel, query);
      if (error) throw error;
      return (data ?? []) as EmployeeResult[];
    },
    enabled: query.length >= 2,
    staleTime: 30 * 1000,
  });

  if (selected) {
    return (
      <View style={s.selectedRow}>
        <Avatar name={selected.full_name} size="sm" />
        <View style={{ flex: 1, marginLeft: 10 }}>
          <Text style={s.selectedName}>{selected.full_name}</Text>
          {selected.position && <Text style={s.selectedSub}>{selected.position}</Text>}
        </View>
        <Pressable onPress={onClear} hitSlop={8}>
          <Ionicons name="close-circle" size={22} color={PURPLE} />
        </Pressable>
      </View>
    );
  }

  return (
    <View>
      <View style={s.searchBox}>
        <Ionicons name="search" size={18} color="#94a3b8" />
        <TextInput
          value={query}
          onChangeText={setQuery}
          onFocus={() => setFocused(true)}
          onBlur={() => setTimeout(() => setFocused(false), 150)}
          placeholder="Search by name…"
          placeholderTextColor="#94a3b8"
          style={s.searchInput}
          autoCorrect={false}
        />
        {isFetching && <ActivityIndicator size="small" color={PURPLE} />}
      </View>
      {focused && results.length > 0 && (
        <View style={s.dropdown}>
          {results.slice(0, 6).map((emp) => (
            <Pressable
              key={emp.id}
              onPress={() => { onSelect(emp); setQuery(''); }}
              style={({ pressed }) => [s.dropdownRow, pressed && { backgroundColor: PURPLE_SOFT }]}
            >
              <Avatar name={emp.full_name} size="sm" />
              <View style={{ flex: 1, marginLeft: 10 }}>
                <Text style={s.dropdownName}>{emp.full_name}</Text>
                <Text style={s.dropdownSub}>
                  {[emp.position, emp.department].filter(Boolean).join(' · ')}
                </Text>
              </View>
            </Pressable>
          ))}
        </View>
      )}
    </View>
  );
}

function SkillBadgeSelector({ selected, onSelect }: {
  selected: SkillBadge | null;
  onSelect: (b: SkillBadge) => void;
}) {
  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
      {SKILL_BADGES.map((b) => {
        const active = selected === b.value;
        return (
          <Pressable
            key={b.value}
            onPress={() => onSelect(b.value)}
            style={[s.badge, {
              backgroundColor: active ? b.color : b.color + '15',
              borderColor:     active ? b.color : b.color + '40',
            }]}
          >
            <Text style={{ fontSize: 14 }}>{b.emoji}</Text>
            <Text style={[s.badgeLabel, { color: active ? '#fff' : b.color }]}>{b.value}</Text>
          </Pressable>
        );
      })}
    </View>
  );
}

export default function SkillsHubScreen() {
  const { employee } = useEmployee();
  const postRecognition = usePostRecognition();

  const [receiver, setReceiver] = useState<EmployeeResult | null>(null);
  const [badge, setBadge]       = useState<SkillBadge | null>(null);
  const [message, setMessage]   = useState('');
  const [error, setError]       = useState('');

  const handleSend = useCallback(() => {
    setError('');
    if (!receiver) { setError('Please select a colleague to recognize.'); return; }
    if (!badge)    { setError('Please select a recognition badge.'); return; }
    if (message.trim().length < 10) { setError('Message must be at least 10 characters.'); return; }
    postRecognition.mutate(
      { receiverId: receiver.id, message: message.trim(), badge: badge as any },
      {
        onSuccess: () => { setReceiver(null); setBadge(null); setMessage(''); router.push('/(tabs)'); },
        onError:   (err: Error) => setError(err.message),
      },
    );
  }, [receiver, badge, message, postRecognition]);

  if (!employee) return null;

  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      {/* Purple header */}
      <View style={s.header}>
        <Text style={s.headerTitle}>Appreciate your Colleague</Text>
        <Text style={s.headerSub}>Recognise the skills that make your team great.</Text>
      </View>

      {/* Pill tab selector */}
      <View style={s.tabPill}>
        <TouchableOpacity
          style={s.tabBtn}
          onPress={() => router.push('/(tabs)/give' as any)}
          activeOpacity={0.8}
        >
          <Text style={s.tabTxt}>Recognition</Text>
        </TouchableOpacity>
        <TouchableOpacity style={[s.tabBtn, s.tabBtnActive]} activeOpacity={0.8}>
          <Text style={[s.tabTxt, s.tabTxtActive]}>Skills</Text>
        </TouchableOpacity>
      </View>

      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        keyboardVerticalOffset={0}
      >
        <ScrollView
          style={s.scroll}
          contentContainerStyle={s.content}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          <View style={s.handle} />

          <Text style={s.label}>Who are you Recognising?</Text>
          <EmployeeSearch
            hotel={employee.hotel}
            selected={receiver}
            onSelect={setReceiver}
            onClear={() => setReceiver(null)}
          />

          <Text style={[s.label, { marginTop: 20 }]}>Choose a Skill</Text>
          <SkillBadgeSelector selected={badge} onSelect={setBadge} />

          <Text style={[s.label, { marginTop: 20 }]}>Your Message</Text>
          <View style={s.messageBox}>
            <TextInput
              value={message}
              onChangeText={(v) => { setMessage(v); setError(''); }}
              placeholder="Share what they did and why it matters…"
              placeholderTextColor="#94a3b8"
              multiline
              numberOfLines={4}
              maxLength={500}
              textAlignVertical="top"
              style={s.messageInput}
            />
          </View>
          <Text style={s.charCount}>{message.length}/500</Text>

          {!!error && (
            <View style={s.errorBox}>
              <Ionicons name="alert-circle" size={16} color="#ef4444" />
              <Text style={s.errorText}>{error}</Text>
            </View>
          )}

          <TouchableOpacity onPress={handleSend} disabled={postRecognition.isPending} activeOpacity={0.8} style={{ marginTop: 16 }}>
            <View style={{ backgroundColor: '#7B1FA2', borderRadius: 16, paddingVertical: 16, flexDirection: 'row', alignItems: 'center', justifyContent: 'center' }}>
              {postRecognition.isPending
                ? <ActivityIndicator color="#fff" />
                : <>
                    <Ionicons name="sparkles" size={18} color="#fff" />
                    <Text style={{ marginLeft: 8, fontSize: 16, fontWeight: '800', color: '#fff' }}>Send Skill</Text>
                  </>
              }
            </View>
          </TouchableOpacity>
        </ScrollView>
      </KeyboardAvoidingView>

    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#f5f3ff' },

  header: {
    backgroundColor: PURPLE,
    borderBottomLeftRadius: 30,
    borderBottomRightRadius: 30,
    paddingHorizontal: 24,
    paddingTop: 12,
    paddingBottom: 30,
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.2,
    shadowRadius: 12,
    elevation: 10,
  },
  headerTitle: { fontSize: 22, fontWeight: '800', color: '#fff', marginBottom: 4 },
  headerSub:   { fontSize: 13, color: 'rgba(255,255,255,0.65)', lineHeight: 18 },

  tabPill:      { flexDirection: 'row', backgroundColor: '#8E24AA', borderRadius: 20, padding: 4, marginHorizontal: 20, marginTop: -22, zIndex: 10, elevation: 4 },
  tabBtn:       { flex: 1, alignItems: 'center', paddingVertical: 10, borderRadius: 16 },
  tabBtnActive: { backgroundColor: '#ffffff', shadowColor: '#000', shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.12, shadowRadius: 4, elevation: 3 },
  tabTxt:       { fontSize: 13, fontWeight: '700', color: 'rgba(255,255,255,0.75)' },
  tabTxtActive: { color: PURPLE },

  scroll: {
    flex: 1,
    backgroundColor: '#fff',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    marginTop: 8,
  },
  content: {
    paddingHorizontal: 20,
    paddingBottom: 120,
  },

  handle: {
    width: 36, height: 4, borderRadius: 2,
    backgroundColor: PURPLE_TINT,
    alignSelf: 'center',
    marginTop: 12, marginBottom: 20,
  },

  label: {
    fontSize: 11, fontWeight: '700', letterSpacing: 0.8,
    textTransform: 'uppercase', color: PURPLE, marginBottom: 10,
  },

  searchBox: {
    flexDirection: 'row', alignItems: 'center',
    borderWidth: 1.5, borderColor: PURPLE_TINT, borderRadius: 14,
    backgroundColor: '#faf8ff', paddingHorizontal: 14, paddingVertical: 12,
  },
  searchInput: { flex: 1, marginLeft: 8, fontSize: 14, color: '#1e1b4b' },

  selectedRow: {
    flexDirection: 'row', alignItems: 'center',
    borderWidth: 1.5, borderColor: PURPLE_TINT, borderRadius: 14,
    backgroundColor: PURPLE_SOFT, paddingHorizontal: 14, paddingVertical: 12,
  },
  selectedName: { fontSize: 14, fontWeight: '700', color: '#1e1b4b' },
  selectedSub:  { fontSize: 12, color: '#94a3b8', marginTop: 2 },

  dropdown: {
    marginTop: 4, borderWidth: 1, borderColor: PURPLE_TINT,
    borderRadius: 14, backgroundColor: '#fff', overflow: 'hidden',
    elevation: 4,
  },
  dropdownRow:  { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 14, paddingVertical: 12 },
  dropdownName: { fontSize: 14, fontWeight: '600', color: '#1e1b4b' },
  dropdownSub:  { fontSize: 12, color: '#94a3b8', marginTop: 2 },

  badge: {
    flexDirection: 'row', alignItems: 'center',
    borderWidth: 1.5, borderRadius: 20, paddingHorizontal: 14, paddingVertical: 8,
  },
  badgeLabel: { marginLeft: 6, fontSize: 12, fontWeight: '700' },

  messageBox: {
    borderWidth: 1.5, borderColor: PURPLE_TINT, borderRadius: 14,
    backgroundColor: '#faf8ff', paddingHorizontal: 14, paddingVertical: 12,
  },
  messageInput: { fontSize: 14, color: '#1e1b4b', lineHeight: 22, minHeight: 90 },
  charCount:    { marginTop: 4, textAlign: 'right', fontSize: 11, color: '#94a3b8' },

  errorBox: {
    flexDirection: 'row', alignItems: 'center',
    backgroundColor: '#fef2f2', borderRadius: 12,
    paddingHorizontal: 14, paddingVertical: 12, marginTop: 12,
  },
  errorText: { flex: 1, marginLeft: 8, fontSize: 13, color: '#ef4444' },
});
