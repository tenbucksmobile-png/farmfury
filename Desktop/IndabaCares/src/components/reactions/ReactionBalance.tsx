import React, { memo } from 'react';
import { View, Text, ActivityIndicator } from 'react-native';
import { useReactionBalance, REACTION_TOTALS } from '@/hooks/use-reaction-balance';
import type { ReactionType } from '@/hooks/use-recognition-reactions';
import type { ReactionBalance as ReactionBalanceData } from '@/hooks/use-reaction-balance';

// ─── Config ───────────────────────────────────────────────────────────────────

const ROWS: {
  type:       ReactionType;
  emoji:      string;
  label:      string;
  totalKey:   keyof typeof REACTION_TOTALS;
  balanceKey: keyof ReactionBalanceData;
}[] = [
  { type: 'heart',     emoji: '❤️', label: 'Hearts',    totalKey: 'heart',     balanceKey: 'hearts_remaining' },
  { type: 'smile',     emoji: '😊', label: 'Smiles',    totalKey: 'smile',     balanceKey: 'smiles_remaining' },
  { type: 'thumbs_up', emoji: '👍', label: 'Thumbs Up', totalKey: 'thumbs_up', balanceKey: 'thumbs_remaining' },
];

// ─── Component ────────────────────────────────────────────────────────────────

export const ReactionBalance = memo(function ReactionBalance() {
  const { data: balance, isLoading } = useReactionBalance();

  return (
    <View className="mx-4 mt-4 rounded-2xl bg-white p-4">
      {/* Header */}
      <Text className="mb-3 text-sm font-semibold text-slate-800">
        Monthly Reactions Remaining
      </Text>

      {isLoading || !balance ? (
        <ActivityIndicator size="small" color="#ED6813" />
      ) : (
        ROWS.map(({ type, emoji, label, totalKey, balanceKey }) => {
          const remaining = balance[balanceKey] as number;
          const total     = REACTION_TOTALS[totalKey];
          const pct       = Math.max(0, remaining / total);
          const exhausted = remaining === 0;

          return (
            <View key={type} className="mb-3 last:mb-0">
              <View className="mb-1 flex-row items-center justify-between">
                <Text className="text-sm text-slate-600">
                  {emoji}  {label}
                </Text>
                <Text
                  className="text-xs font-semibold"
                  style={{ color: exhausted ? '#ef4444' : '#ED6813' }}
                >
                  {remaining} / {total}
                </Text>
              </View>
              {/* Progress bar */}
              <View className="h-1.5 w-full overflow-hidden rounded-full bg-slate-100">
                <View
                  className="h-full rounded-full"
                  style={{
                    width: `${pct * 100}%`,
                    backgroundColor: exhausted ? '#ef4444' : '#ED6813',
                  }}
                />
              </View>
            </View>
          );
        })
      )}

      <Text className="mt-2 text-xs text-slate-400">
        Resets at the start of each month
      </Text>
    </View>
  );
});
