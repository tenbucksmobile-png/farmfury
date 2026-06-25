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
import { router } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useQuery } from '@tanstack/react-query';
import { Avatar } from '@/components/ui/Avatar';
import { usePostRecognition } from '@/hooks/use-post-recognition';
import { searchEmployeesQuery } from '@/api/queries';
import { RECOGNITION_BADGES, QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';
import type { RecognitionBadge } from '@/lib/constants';

const PURPLE      = '#7B1FA2';
const PURPLE_MID  = '#9C27B0';
const PURPLE_SOFT = '#ede9fe';
const PURPLE_TINT = '#ddd6fe';

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

function BadgeSelector({ selected, onSelect }: {
  selected: RecognitionBadge | null;
  onSelect: (b: RecognitionBadge) => void;
}) {
  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
      {RECOGNITION_BADGES.map((b) => {
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

export default function GiveScreen() {
  const { employee } = useEmployee();
  const postRecognition = usePostRecognition();

  const [activeTab, setActiveTab] = useState<'recognition' | 'skills'>('recognition');

  // Recognition form state
  const [recReceiver, setRecReceiver] = useState<EmployeeResult | null>(null);
  const [recBadge, setRecBadge]       = useState<RecognitionBadge | null>(null);
  const [recMessage, setRecMessage]   = useState('');
  const [recError, setRecError]       = useState('');

  // Skills form state
  const [sklReceiver, setSklReceiver] = useState<EmployeeResult | null>(null);
  const [sklBadge, setSklBadge]       = useState<SkillBadge | null>(null);
  const [sklMessage, setSklMessage]   = useState('');
  const [sklError, setSklError]       = useState('');

  const handleSendRecognition = useCallback(() => {
    setRecError('');
    if (!recReceiver) { setRecError('Please select a colleague to recognize.'); return; }
    if (!recBadge)    { setRecError('Please select a recognition badge.'); return; }
    if (recMessage.trim().length < 10) { setRecError('Message must be at least 10 characters.'); return; }
    postRecognition.mutate(
      { receiverId: recReceiver.id, message: recMessage.trim(), badge: recBadge, cardType: 'recognition' },
      {
        onSuccess: () => { setRecReceiver(null); setRecBadge(null); setRecMessage(''); router.push('/(tabs)'); },
        onError:   (err: Error) => setRecError(err.message),
      },
    );
  }, [recReceiver, recBadge, recMessage, postRecognition]);

  const handleSendSkill = useCallback(() => {
    setSklError('');
    if (!sklReceiver) { setSklError('Please select a colleague to recognise.'); return; }
    if (!sklBadge)    { setSklError('Please select a skill.'); return; }
    if (sklMessage.trim().length < 10) { setSklError('Message must be at least 10 characters.'); return; }
    postRecognition.mutate(
      { receiverId: sklReceiver.id, message: sklMessage.trim(), badge: sklBadge, cardType: 'skills' },
      {
        onSuccess: () => { setSklReceiver(null); setSklBadge(null); setSklMessage(''); router.push('/(tabs)'); },
        onError:   (err: Error) => setSklError(err.message),
      },
    );
  }, [sklReceiver, sklBadge, sklMessage, postRecognition]);

  if (!employee) return null;

  return (
    <SafeAreaView style={s.safe} edges={['top']}>

      {/* Purple header */}
      <View style={s.header}>
        <Text style={s.headerTitle}>Appreciate your Colleague</Text>
        <Text style={s.headerSub}>Celebrate someone who made a difference today.</Text>
      </View>

      {/* Pill tab selector */}
      <View style={s.tabPill}>
        <TouchableOpacity
          style={[s.tabBtn, activeTab === 'recognition' && s.tabBtnActive]}
          onPress={() => setActiveTab('recognition')}
          activeOpacity={0.8}
        >
          <Text style={[s.tabTxt, activeTab === 'recognition' && s.tabTxtActive]}>Recognition</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[s.tabBtn, activeTab === 'skills' && s.tabBtnActive]}
          onPress={() => setActiveTab('skills')}
          activeOpacity={0.8}
        >
          <Text style={[s.tabTxt, activeTab === 'skills' && s.tabTxtActive]}>Skills</Text>
        </TouchableOpacity>
      </View>

      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        keyboardVerticalOffset={0}
      >
        {activeTab === 'recognition' ? (
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
              selected={recReceiver}
              onSelect={setRecReceiver}
              onClear={() => setRecReceiver(null)}
            />

            <Text style={[s.label, { marginTop: 20 }]}>Choose a Badge</Text>
            <BadgeSelector selected={recBadge} onSelect={setRecBadge} />

            <Text style={[s.label, { marginTop: 20 }]}>Your Message</Text>
            <View style={s.messageBox}>
              <TextInput
                value={recMessage}
                onChangeText={(v) => { setRecMessage(v); setRecError(''); }}
                placeholder="Share what they did and why it matters…"
                placeholderTextColor="#94a3b8"
                multiline
                numberOfLines={4}
                maxLength={500}
                textAlignVertical="top"
                style={s.messageInput}
              />
            </View>
            <Text style={s.charCount}>{recMessage.length}/500</Text>

            {!!recError && (
              <View style={s.errorBox}>
                <Ionicons name="alert-circle" size={16} color="#ef4444" />
                <Text style={s.errorText}>{recError}</Text>
              </View>
            )}

            <TouchableOpacity onPress={handleSendRecognition} disabled={postRecognition.isPending} activeOpacity={0.8} style={{ marginTop: 16 }}>
              <View style={{ backgroundColor: '#7B1FA2', borderRadius: 16, paddingVertical: 16, flexDirection: 'row', alignItems: 'center', justifyContent: 'center' }}>
                {postRecognition.isPending
                  ? <ActivityIndicator color="#fff" />
                  : <>
                      <Ionicons name="sparkles" size={18} color="#fff" />
                      <Text style={{ marginLeft: 8, fontSize: 16, fontWeight: '800', color: '#fff' }}>Send Recognition</Text>
                    </>
                }
              </View>
            </TouchableOpacity>
          </ScrollView>
        ) : (
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
              selected={sklReceiver}
              onSelect={setSklReceiver}
              onClear={() => setSklReceiver(null)}
            />

            <Text style={[s.label, { marginTop: 20 }]}>Choose a Skill</Text>
            <SkillBadgeSelector selected={sklBadge} onSelect={setSklBadge} />

            <Text style={[s.label, { marginTop: 20 }]}>Your Message</Text>
            <View style={s.messageBox}>
              <TextInput
                value={sklMessage}
                onChangeText={(v) => { setSklMessage(v); setSklError(''); }}
                placeholder="Share what they did and why it matters…"
                placeholderTextColor="#94a3b8"
                multiline
                numberOfLines={4}
                maxLength={500}
                textAlignVertical="top"
                style={s.messageInput}
              />
            </View>
            <Text style={s.charCount}>{sklMessage.length}/500</Text>

            {!!sklError && (
              <View style={s.errorBox}>
                <Ionicons name="alert-circle" size={16} color="#ef4444" />
                <Text style={s.errorText}>{sklError}</Text>
              </View>
            )}

            <TouchableOpacity onPress={handleSendSkill} disabled={postRecognition.isPending} activeOpacity={0.8} style={{ marginTop: 16 }}>
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
        )}
      </KeyboardAvoidingView>

    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  safe:   { flex: 1, backgroundColor: '#f5f3ff' },

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

  btn: {
    marginTop: 20,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: PURPLE,
    borderRadius: 16,
    paddingVertical: 16,
    elevation: 6,
    shadowColor: PURPLE_MID,
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.35,
    shadowRadius: 12,
  },
  btnLabel: { marginLeft: 8, fontSize: 16, fontWeight: '800', color: '#fff' },

  errorBox: {
    flexDirection: 'row', alignItems: 'center',
    backgroundColor: '#fef2f2', borderRadius: 12,
    paddingHorizontal: 14, paddingVertical: 12, marginTop: 12,
  },
  errorText: { flex: 1, marginLeft: 8, fontSize: 13, color: '#ef4444' },
});
