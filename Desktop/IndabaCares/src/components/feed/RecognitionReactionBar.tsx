import React, { memo, useState, useRef, useEffect } from 'react';
import { View, Text, Pressable, Modal, Animated, StyleSheet, GestureResponderEvent } from 'react-native';
import { useEmployee } from '@/providers/EmployeeContext';
import {
  useRecognitionReactions,
  useSubmitReaction,
  type ReactionType,
} from '@/hooks/use-recognition-reactions';
import { useReactionBalance } from '@/hooks/use-reaction-balance';
import { ReactionExhaustedModal } from '@/components/reactions/ReactionExhaustedModal';

// ─── Config ───────────────────────────────────────────────────────────────────

const REACTIONS: { type: ReactionType; emoji: string }[] = [
  { type: 'heart',     emoji: '❤️' },
  { type: 'smile',     emoji: '😊' },
  { type: 'thumbs_up', emoji: '👍' },
];

// ─── Props ────────────────────────────────────────────────────────────────────

interface RecognitionReactionBarProps {
  recognitionId: string;
  pickerVisible: boolean;
  pickerY: number;
  onPickerClose: () => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

export const RecognitionReactionBar = memo(function RecognitionReactionBar({
  recognitionId,
  pickerVisible,
  pickerY,
  onPickerClose,
}: RecognitionReactionBarProps) {
  const { employee } = useEmployee();
  const { data: reactions = [] } = useRecognitionReactions(recognitionId);
  const { data: balance }        = useReactionBalance();
  const submit                   = useSubmitReaction(recognitionId);

  const [exhaustedType, setExhaustedType] = useState<ReactionType | null>(null);

  // Spring animation for picker entrance
  const scaleAnim = useRef(new Animated.Value(0)).current;
  const translateY = useRef(new Animated.Value(12)).current;

  useEffect(() => {
    if (pickerVisible) {
      Animated.parallel([
        Animated.spring(scaleAnim, { toValue: 1, friction: 6, tension: 120, useNativeDriver: true }),
        Animated.spring(translateY, { toValue: 0, friction: 6, tension: 120, useNativeDriver: true }),
      ]).start();
    } else {
      scaleAnim.setValue(0);
      translateY.setValue(12);
    }
  }, [pickerVisible]);

  const myReaction = reactions.find((r) => r.employee_id === employee?.employee_id) ?? null;

  const counts = reactions.reduce<Record<ReactionType, number>>(
    (acc, r) => { acc[r.reaction_type] = (acc[r.reaction_type] ?? 0) + 1; return acc; },
    { heart: 0, smile: 0, thumbs_up: 0 },
  );

  const handlePick = (type: ReactionType) => {
    if (submit.isPending) return;
    onPickerClose();
    const existingId = myReaction?.reaction_type === type ? myReaction.id : null;
    submit.mutate({ reactionType: type, existingId }, {
      onError: () => {
        if (!existingId) setExhaustedType(type);
      },
    });
  };

  const hasReactions = Object.values(counts).some((c) => c > 0);

  return (
    <>
      {/* ── Reaction count pills (display only) ── */}
      {hasReactions && (
        <View style={s.countsRow}>
          {REACTIONS.map(({ type, emoji }) => {
            const count = counts[type];
            if (!count) return null;
            const isActive = myReaction?.reaction_type === type;
            return (
              <View key={type} style={[s.countPill, isActive && s.countPillActive]}>
                <Text style={s.countEmoji}>{emoji}</Text>
                <Text style={[s.countText, isActive && s.countTextActive]}>{count}</Text>
              </View>
            );
          })}
        </View>
      )}

      {/* ── Floating picker (WhatsApp style) ── */}
      <Modal
        visible={pickerVisible}
        transparent
        animationType="none"
        onRequestClose={onPickerClose}
        statusBarTranslucent
      >
        <Pressable style={s.backdrop} onPress={onPickerClose}>
          <Animated.View
            style={[
              s.pickerPill,
              {
                top: Math.max(60, pickerY - 90),
                transform: [{ scale: scaleAnim }, { translateY }],
              },
            ]}
          >
            {REACTIONS.map(({ type, emoji }) => {
              const isActive = myReaction?.reaction_type === type;
              return (
                <Pressable
                  key={type}
                  onPress={() => handlePick(type)}
                  style={({ pressed }) => [
                    s.pickerBtn,
                    isActive && s.pickerBtnActive,
                    pressed && s.pickerBtnPressed,
                  ]}
                >
                  <Animated.Text style={s.pickerEmoji}>{emoji}</Animated.Text>
                  {isActive && <View style={s.activeDot} />}
                </Pressable>
              );
            })}
          </Animated.View>
        </Pressable>
      </Modal>

      {exhaustedType && balance && (
        <ReactionExhaustedModal
          visible
          onClose={() => setExhaustedType(null)}
          exhaustedType={exhaustedType}
          balance={balance}
        />
      )}
    </>
  );
});

// ─── Styles ───────────────────────────────────────────────────────────────────

const s = StyleSheet.create({
  // Counts row
  countsRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    marginTop: 10,
  },
  countPill: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f1f5f9',
    borderRadius: 20,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  countPillActive: {
    backgroundColor: '#fef3c7',
    borderWidth: 1,
    borderColor: '#fcd34d',
  },
  countEmoji: { fontSize: 13 },
  countText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#64748b',
    marginLeft: 3,
  },
  countTextActive: { color: '#ED6813' },

  // Picker modal
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.2)',
  },
  pickerPill: {
    position: 'absolute',
    left: 24,
    right: 24,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    gap: 4,
    backgroundColor: '#fff',
    borderRadius: 50,
    paddingVertical: 8,
    paddingHorizontal: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.18,
    shadowRadius: 16,
    elevation: 14,
  },
  pickerBtn: {
    width: 56,
    height: 56,
    borderRadius: 28,
    alignItems: 'center',
    justifyContent: 'center',
  },
  pickerBtnActive: {
    backgroundColor: '#fef9c3',
  },
  pickerBtnPressed: {
    backgroundColor: '#f1f5f9',
    transform: [{ scale: 1.25 }],
  },
  pickerEmoji: { fontSize: 32 },
  activeDot: {
    position: 'absolute',
    bottom: 4,
    width: 5,
    height: 5,
    borderRadius: 3,
    backgroundColor: '#ED6813',
  },
});
