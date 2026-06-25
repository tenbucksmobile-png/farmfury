import React from 'react';
import { View, Text, ScrollView } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useProfileDetail } from '@/hooks/use-profiles';
import { useUserBadges } from '@/hooks/use-badges';
import { Avatar } from '@/components/ui/Avatar';
import { Badge } from '@/components/ui/Badge';
import { BadgeShowcase } from '@/components/profile/BadgeShowcase';
import { SkeletonCard } from '@/components/ui/Skeleton';
import { Ionicons } from '@expo/vector-icons';

export default function UserProfileScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: profile, isLoading } = useProfileDetail(id);
  const { data: userBadges = [] } = useUserBadges(id);

  if (isLoading || !profile) {
    return (
      <View className="flex-1 bg-white p-4">
        <SkeletonCard />
      </View>
    );
  }

  return (
    <ScrollView className="flex-1 bg-slate-50">
      <View className="items-center bg-white px-6 pb-6 pt-8">
        <Avatar
          uri={(profile as any).avatar_url}
          name={(profile as any).full_name}
          size="xl"
        />
        <Text className="mt-3 text-xl font-bold text-slate-900">
          {(profile as any).display_name || (profile as any).full_name}
        </Text>
        {(profile as any).job_title && (
          <Text className="mt-0.5 text-sm text-slate-500">
            {(profile as any).job_title}
          </Text>
        )}
        <View className="mt-2 flex-row items-center">
          <Badge label={(profile as any).role?.replace('_', ' ')} />
          {(profile as any).departments && (
            <Badge label={(profile as any).departments.name} color="#22c55e" />
          )}
        </View>
      </View>

      {/* Points */}
      <View className="mx-4 mt-4 flex-row items-center rounded-2xl bg-white p-4">
        <Ionicons name="trophy" size={22} color="#ED6813" />
        <Text className="ml-3 text-sm text-slate-500">Points</Text>
        <Text className="ml-auto text-lg font-bold text-primary-600">
          {(profile as any).points_balance}
        </Text>
      </View>

      {/* Badges */}
      <View className="mx-4 mt-4">
        <BadgeShowcase badges={userBadges as any} />
      </View>
    </ScrollView>
  );
}
