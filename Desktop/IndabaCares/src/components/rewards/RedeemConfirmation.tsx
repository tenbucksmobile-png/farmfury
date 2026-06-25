import React from 'react';
import { View, Text, Pressable, ActivityIndicator } from 'react-native';
import { Image } from 'expo-image';
import { Ionicons } from '@expo/vector-icons';

interface RedeemConfirmationProps {
  reward: {
    title: string;
    image_url: string | null;
    points_required: number;
  };
  pointsBalance: number;
  onConfirm: () => void;
  onCancel: () => void;
  loading?: boolean;
}

export function RedeemConfirmation({
  reward,
  pointsBalance,
  onConfirm,
  onCancel,
  loading,
}: RedeemConfirmationProps) {
  const canAfford    = pointsBalance >= reward.points_required;
  const balanceAfter = pointsBalance - reward.points_required;

  return (
    <View className="p-6">
      <Text className="mb-5 text-center text-lg font-bold text-slate-900">
        Confirm Redemption
      </Text>

      {/* Reward image */}
      {reward.image_url ? (
        <Image
          source={{ uri: reward.image_url }}
          className="mb-4 h-36 w-full self-center rounded-2xl"
          contentFit="cover"
        />
      ) : (
        <View className="mb-4 h-36 w-full items-center justify-center rounded-2xl bg-slate-100">
          <Ionicons name="gift" size={48} color="#94a3b8" />
        </View>
      )}

      <Text className="mb-1 text-center text-base font-bold text-slate-800">
        {reward.title}
      </Text>

      {/* Points cost */}
      <View className="mb-5 flex-row items-center justify-center">
        <Ionicons name="flash" size={20} color="#f59e0b" />
        <Text className="ml-1 text-xl font-bold text-amber-600">
          {reward.points_required}
        </Text>
        <Text className="ml-1 text-sm text-slate-400">points</Text>
      </View>

      {/* Balance summary */}
      <View className="mb-5 overflow-hidden rounded-2xl border border-slate-100 bg-slate-50">
        <View className="flex-row items-center justify-between px-4 py-3">
          <Text className="text-sm text-slate-500">Your balance</Text>
          <Text className="text-sm font-bold text-slate-800">{pointsBalance} pts</Text>
        </View>
        <View className="h-px bg-slate-100" />
        <View className="flex-row items-center justify-between px-4 py-3">
          <Text className="text-sm text-slate-500">After redemption</Text>
          <Text
            className="text-sm font-bold"
            style={{ color: canAfford ? '#16a34a' : '#ef4444' }}
          >
            {balanceAfter} pts
          </Text>
        </View>
      </View>

      {!canAfford && (
        <View className="mb-4 flex-row items-center rounded-xl bg-red-50 px-4 py-3">
          <Ionicons name="alert-circle" size={16} color="#ef4444" />
          <Text className="ml-2 flex-1 text-sm text-red-600">
            You need {reward.points_required - pointsBalance} more points for this reward.
          </Text>
        </View>
      )}

      {/* Action buttons */}
      <View className="flex-row gap-3">
        <Pressable
          onPress={onCancel}
          className="flex-1 items-center justify-center rounded-2xl border border-slate-200 py-3.5 active:bg-slate-50"
        >
          <Text className="text-sm font-semibold text-slate-600">Cancel</Text>
        </Pressable>

        <Pressable
          onPress={onConfirm}
          disabled={!canAfford || loading}
          className="flex-1 items-center justify-center rounded-2xl py-3.5 active:opacity-80"
          style={{
            backgroundColor: canAfford ? '#ED6813' : '#e2e8f0',
            shadowColor: canAfford ? '#ED6813' : 'transparent',
            shadowOffset: { width: 0, height: 4 },
            shadowOpacity: 0.3,
            shadowRadius: 8,
            elevation: canAfford ? 4 : 0,
          }}
        >
          {loading ? (
            <ActivityIndicator color="#fff" size="small" />
          ) : (
            <Text
              className="text-sm font-bold"
              style={{ color: canAfford ? '#fff' : '#94a3b8' }}
            >
              Redeem
            </Text>
          )}
        </Pressable>
      </View>
    </View>
  );
}
