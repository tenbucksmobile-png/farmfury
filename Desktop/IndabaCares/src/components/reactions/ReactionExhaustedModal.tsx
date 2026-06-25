import React from 'react';
import { Modal, View, Text, Pressable } from 'react-native';
import type { ReactionType } from '@/hooks/use-recognition-reactions';
import type { ReactionBalance } from '@/hooks/use-reaction-balance';
import { REACTION_TOTALS } from '@/hooks/use-reaction-balance';

// ─── Config ───────────────────────────────────────────────────────────────────

const REACTION_META: Record<ReactionType, { emoji: string; label: string; totalKey: keyof typeof REACTION_TOTALS; balanceKey: keyof ReactionBalance }> = {
  heart:     { emoji: '❤️', label: 'Heart',     totalKey: 'heart',     balanceKey: 'hearts_remaining' },
  smile:     { emoji: '😊', label: 'Smile',     totalKey: 'smile',     balanceKey: 'smiles_remaining' },
  thumbs_up: { emoji: '👍', label: 'Thumbs Up', totalKey: 'thumbs_up', balanceKey: 'thumbs_remaining' },
};

const REACTION_TYPES: ReactionType[] = ['heart', 'smile', 'thumbs_up'];

// ─── Props ────────────────────────────────────────────────────────────────────

interface ReactionExhaustedModalProps {
  visible:      boolean;
  onClose:      () => void;
  exhaustedType: ReactionType;
  balance:      ReactionBalance;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ReactionExhaustedModal({
  visible,
  onClose,
  exhaustedType,
  balance,
}: ReactionExhaustedModalProps) {
  const meta = REACTION_META[exhaustedType];

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onClose}
    >
      <Pressable
        className="flex-1 items-center justify-center bg-black/40"
        onPress={onClose}
      >
        <Pressable
          className="mx-6 w-full max-w-sm rounded-3xl bg-white p-6 shadow-xl"
          onPress={() => {/* prevent bubble-up close */}}
        >
          {/* Icon + title */}
          <View className="items-center">
            <Text className="text-4xl">{meta.emoji}</Text>
            <Text className="mt-2 text-center text-base font-bold text-slate-800">
              No {meta.label} reactions left
            </Text>
            <Text className="mt-1 text-center text-sm text-slate-500">
              You have used all your {meta.emoji} reactions for this month.
            </Text>
          </View>

          {/* Balance grid */}
          <View className="mt-5 rounded-2xl bg-slate-50 p-4">
            <Text className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">
              Monthly Remaining
            </Text>
            {REACTION_TYPES.map((type) => {
              const m         = REACTION_META[type];
              const remaining = balance[m.balanceKey] as number;
              const total     = REACTION_TOTALS[m.totalKey];
              const pct       = Math.max(0, remaining / total);
              const isThis    = type === exhaustedType;

              return (
                <View key={type} className="mb-3 last:mb-0">
                  <View className="flex-row items-center justify-between mb-1">
                    <Text className="text-sm">
                      {m.emoji}{' '}
                      <Text className={isThis ? 'font-bold text-red-500' : 'text-slate-700'}>
                        {remaining} / {total}
                      </Text>
                    </Text>
                  </View>
                  {/* Progress bar */}
                  <View className="h-1.5 w-full overflow-hidden rounded-full bg-slate-200">
                    <View
                      className="h-full rounded-full"
                      style={{
                        width: `${pct * 100}%`,
                        backgroundColor: isThis ? '#ef4444' : '#ED6813',
                      }}
                    />
                  </View>
                </View>
              );
            })}
          </View>

          {/* Dismiss */}
          <Pressable
            onPress={onClose}
            className="mt-4 items-center rounded-2xl bg-slate-100 py-3 active:bg-slate-200"
          >
            <Text className="text-sm font-semibold text-slate-600">Got it</Text>
          </Pressable>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
