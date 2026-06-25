import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  Pressable,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { useLocalSearchParams, router, Stack } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { useRewardDetail, useEmployeePoints, useRedeemReward } from '@/hooks/use-rewards';
import { SkeletonCard } from '@/components/ui/Skeleton';
import { formatDate } from '@/utils/format';

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function RewardDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const insets  = useSafeAreaInsets();
  const [redeemed, setRedeemed] = useState(false);

  const { data: reward, isLoading } = useRewardDetail(id);
  const { data: points = 0 }        = useEmployeePoints();
  const redeemMutation               = useRedeemReward();

  if (isLoading || !reward) {
    return (
      <View className="flex-1 bg-white p-4">
        <SkeletonCard />
        <SkeletonCard />
      </View>
    );
  }

  const outOfStock = reward.stock <= 0;
  const canAfford  = points >= reward.points_required;

  const handleRedeem = () => {
    redeemMutation.mutate(reward.id, {
      onSuccess: (result) => {
        if (result.ok) {
          setRedeemed(true);
        } else {
          Alert.alert('Redemption Failed', result.error ?? 'Please try again.');
        }
      },
      onError: (err: Error) => {
        Alert.alert('Redemption Failed', err.message);
      },
    });
  };

  // ── Success overlay ───────────────────────────────────────────────────────

  if (redeemed) {
    return (
      <View
        className="flex-1 items-center justify-center bg-white px-10"
        style={{ paddingBottom: insets.bottom }}
      >
        <View
          className="mb-8 h-24 w-24 items-center justify-center rounded-full"
          style={{ backgroundColor: '#f3e8ff' }}
        >
          <Ionicons name="checkmark-circle" size={56} color="#7C3AED" />
        </View>

        <Text
          className="mb-4 text-center text-xl font-bold text-slate-900"
          style={{ lineHeight: 30 }}
        >
          Congratulations your reward has been redeemed.{'\n'}Check your email for the reward voucher.
        </Text>

        <Pressable
          onPress={() => router.replace('/(tabs)/profile' as any)}
          className="mt-4 w-full items-center rounded-2xl py-4 active:opacity-80"
          style={{
            backgroundColor: '#7C3AED',
            shadowColor: '#7C3AED',
            shadowOffset: { width: 0, height: 4 },
            shadowOpacity: 0.3,
            shadowRadius: 10,
            elevation: 5,
          }}
        >
          <Text className="text-base font-bold text-white">Home</Text>
        </Pressable>
      </View>
    );
  }

  return (
    <>
      <Stack.Screen options={{ title: 'REWARD' }} />
      <ScrollView
        className="flex-1 bg-white"
        contentContainerStyle={{ paddingBottom: insets.bottom + 110 }}
      >
        {/* Hero image */}
        {reward.image_url ? (
          <Image
            source={{ uri: reward.image_url }}
            className="h-64 w-full"
            contentFit="cover"
          />
        ) : (
          <View className="h-64 w-full items-center justify-center bg-slate-100">
            <Ionicons name="gift" size={72} color="#cbd5e1" />
          </View>
        )}

        <View className="px-5 pt-5">
          {/* Title */}
          <Text className="text-2xl font-bold text-slate-900">{reward.title}</Text>

          {/* Points cost */}
          <View className="mt-3 flex-row items-center">
            <View
              className="flex-row items-center rounded-xl px-3 py-1.5"
              style={{ backgroundColor: canAfford ? '#fef9c3' : '#fee2e2' }}
            >
              <Ionicons
                name="flash"
                size={18}
                color={canAfford ? '#ca8a04' : '#ef4444'}
              />
              <Text
                className="ml-1.5 text-lg font-bold"
                style={{ color: canAfford ? '#92400e' : '#ef4444' }}
              >
                {reward.points_required} points
              </Text>
            </View>
          </View>

          {/* Description */}
          {reward.description && (
            <Text className="mt-4 text-base leading-7 text-slate-600">
              {reward.description}
            </Text>
          )}

          {/* Stock status */}
          <View className="mt-4 flex-row items-center">
            <Ionicons
              name={outOfStock ? 'close-circle' : 'checkmark-circle'}
              size={18}
              color={outOfStock ? '#ef4444' : '#22c55e'}
            />
            <Text
              className="ml-1.5 text-sm font-medium"
              style={{ color: outOfStock ? '#ef4444' : '#16a34a' }}
            >
              {outOfStock ? 'Out of stock' : `${reward.stock} remaining`}
            </Text>
          </View>

          {/* Balance card */}
          <View className="mt-5 overflow-hidden rounded-2xl border border-slate-100 bg-slate-50">
            <View className="flex-row items-center justify-between px-4 py-3">
              <Text className="text-sm text-slate-500">Your balance</Text>
              <View className="flex-row items-center">
                <Ionicons name="flash" size={14} color="#f59e0b" />
                <Text className="ml-1 text-sm font-bold text-slate-800">{points} pts</Text>
              </View>
            </View>
            {!outOfStock && (
              <>
                <View className="h-px bg-slate-200" />
                <View className="flex-row items-center justify-between px-4 py-3">
                  <Text className="text-sm text-slate-500">After redemption</Text>
                  <Text
                    className="text-sm font-bold"
                    style={{ color: canAfford ? '#16a34a' : '#ef4444' }}
                  >
                    {points - reward.points_required} pts
                  </Text>
                </View>
              </>
            )}
          </View>

          {!canAfford && !outOfStock && (
            <View className="mt-3 flex-row items-center rounded-xl bg-amber-50 px-4 py-3">
              <Ionicons name="information-circle" size={16} color="#d97706" />
              <Text className="ml-2 flex-1 text-sm text-amber-700">
                You need {reward.points_required - points} more points.
                Keep collecting recognitions!
              </Text>
            </View>
          )}

          <Text className="mt-4 text-xs text-slate-400">
            Added {formatDate(reward.created_at)}
          </Text>
        </View>
      </ScrollView>

      {/* Redeem CTA — fixed bottom */}
      <View
        className="absolute bottom-0 left-0 right-0 border-t border-slate-100 bg-white px-5"
        style={{ paddingBottom: insets.bottom + 12, paddingTop: 12 }}
      >
        <Pressable
          onPress={handleRedeem}
          disabled={outOfStock || !canAfford || redeemMutation.isPending}
          className="items-center justify-center rounded-2xl py-4 active:opacity-80"
          style={{
            backgroundColor:
              outOfStock || !canAfford ? '#e2e8f0' : '#7C3AED',
            shadowColor: '#7C3AED',
            shadowOffset: { width: 0, height: 4 },
            shadowOpacity: outOfStock || !canAfford ? 0 : 0.35,
            shadowRadius: 10,
            elevation: outOfStock || !canAfford ? 0 : 5,
          }}
        >
          {redeemMutation.isPending ? (
            <ActivityIndicator color="#fff" size="small" />
          ) : (
            <Text
              className="text-base font-bold"
              style={{ color: outOfStock || !canAfford ? '#94a3b8' : '#fff' }}
            >
              {outOfStock
                ? 'Out of Stock'
                : !canAfford
                ? `Need ${reward.points_required - points} more pts`
                : 'Redeem Reward'}
            </Text>
          )}
        </Pressable>
      </View>

    </>
  );
}
