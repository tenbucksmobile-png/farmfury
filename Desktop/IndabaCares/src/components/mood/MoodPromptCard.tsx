import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  Pressable,
  Modal,
  StyleSheet,
} from 'react-native';
import { useSubmitMood, useMoodHistory } from '@/hooks/use-mood';
import { MOOD_MAP, type MoodValue } from '@/lib/constants';

const PURPLE = '#7B1FA2';

interface MoodPromptCardProps {
  onSelect?: (emoji: string) => void;
}

export function MoodPromptCard({ onSelect }: MoodPromptCardProps) {
  const { data: history = [] } = useMoodHistory();
  const submitMood = useSubmitMood();

  const [visible, setVisible] = useState(false);

  const today             = new Date().toISOString().split('T')[0];
  const moodSubmittedToday = history.some((e: any) => e.entry_date === today);

  useEffect(() => {
    if (submitMood.isSuccess) setVisible(false);
  }, [submitMood.isSuccess]);

  if (moodSubmittedToday) return null;

  function handlePick(key: MoodValue) {
    const emoji = MOOD_MAP[key].emoji;
    submitMood.mutate({ mood: key });
    onSelect?.(emoji);
    setVisible(false);
  }

  return (
    <>
      {/* ── Prompt card ── */}
      <Pressable style={styles.card} onPress={() => setVisible(true)}>
        <View style={styles.cardText}>
          <View style={styles.titleRow}>
            <Text style={styles.cardTitle}>How are you feeling today?</Text>
            <View style={styles.ptsBadge}>
              <Text style={styles.ptsStar}>⭐</Text>
              <Text style={styles.ptsText}>5</Text>
            </View>
          </View>
        </View>
      </Pressable>

      {/* ── Picker modal ── */}
      <Modal
        visible={visible}
        transparent
        animationType="fade"
        onRequestClose={() => setVisible(false)}
      >
        <Pressable style={styles.backdrop} onPress={() => setVisible(false)}>
          <Pressable style={styles.pickerCard} onPress={(e) => e.stopPropagation()}>
            <Text style={styles.pickerTitle}>How are you feeling?</Text>
            <View style={styles.emojiRow}>
              {(Object.entries(MOOD_MAP) as [MoodValue, (typeof MOOD_MAP)[MoodValue]][]).map(
                ([key, val]) => (
                  <Pressable
                    key={key}
                    onPress={() => handlePick(key)}
                    style={({ pressed }) => [styles.emojiButton, pressed && { backgroundColor: val.color + '22' }]}
                  >
                    <Text style={styles.emojiText}>{val.emoji}</Text>
                    <Text style={[styles.emojiLabel, { color: val.color }]}>{val.label}</Text>
                  </Pressable>
                )
              )}
            </View>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#EDE7F6',
    borderRadius: 13,
    paddingHorizontal: 14,
    paddingVertical: 10,
    marginBottom: 0,
  },
  cardEmoji:     { fontSize: 18 },
  cardText:      { flex: 1, marginLeft: 8 },
  cardTitle:     { fontSize: 12, fontWeight: '700', color: PURPLE },
  titleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  ptsBadge: { flexDirection: 'row', alignItems: 'center', gap: 3 },
  ptsStar:  { fontSize: 11 },
  ptsText:  { fontSize: 11, fontWeight: '800', color: PURPLE },
  cardIcons:     { flexDirection: 'row', gap: 2 },
  cardIconEmoji: { fontSize: 16 },

  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  pickerCard: {
    backgroundColor: '#ffffff',
    borderRadius: 24,
    paddingHorizontal: 20,
    paddingTop: 22,
    paddingBottom: 20,
    width: '88%',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.18,
    shadowRadius: 20,
    elevation: 12,
  },
  pickerTitle: {
    fontSize: 17,
    fontWeight: '800',
    color: '#111827',
    textAlign: 'center',
    marginBottom: 18,
  },
  emojiRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  emojiButton: {
    alignItems: 'center',
    borderRadius: 12,
    paddingVertical: 10,
    paddingHorizontal: 8,
    flex: 1,
  },
  emojiText:  { fontSize: 34 },
  emojiLabel: { fontSize: 10, marginTop: 5, fontWeight: '600' },
});
