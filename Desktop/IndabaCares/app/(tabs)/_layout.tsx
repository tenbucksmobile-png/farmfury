import React from 'react';
import { View, Text, Pressable } from 'react-native';
import { Tabs, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useCompanyTheme } from '@/hooks/use-company-theme';
import { useFeatureFlags } from '@/hooks/use-feature-flags';
import { useUnreadCount } from '@/hooks/use-notifications';

const PURPLE    = '#7B1FA2';
const ACCENT    = '#CE21FB';
const NAV_BG    = '#1a0533';
const NAV_INACT = '#c4b5fd';
const NAV_ACT   = '#ffffff';

function HeaderRight() {
  const unread = useUnreadCount();

  return (
    <View style={{ flexDirection: 'row', alignItems: 'center' }}>
      <Pressable
        onPress={() => router.push('/(screens)/notifications')}
        style={{ marginRight: 16, height: 40, width: 40, alignItems: 'center', justifyContent: 'center' }}
        hitSlop={8}
      >
        <Ionicons name="notifications-outline" size={24} color={PURPLE} />
        {unread > 0 && (
          <View className="absolute -right-0.5 -top-0.5 h-5 min-w-[20px] items-center justify-center rounded-full bg-danger-500 px-1">
            <Text className="text-[10px] font-bold text-white">
              {unread > 99 ? '99+' : unread}
            </Text>
          </View>
        )}
      </Pressable>
    </View>
  );
}

export default function TabLayout() {
  const { primaryColor } = useCompanyTheme();
  const flags = useFeatureFlags();

  return (
    <Tabs
      initialRouteName="profile"
      screenOptions={{
        tabBarActiveTintColor: NAV_ACT,
        tabBarInactiveTintColor: NAV_INACT,
        tabBarStyle: {
          height: 85,
          paddingBottom: 25,
          paddingTop: 8,
          borderTopWidth: 1,
          borderTopColor: 'rgba(255,255,255,0.08)',
          backgroundColor: NAV_BG,
        },
        tabBarLabelStyle: {
          fontSize: 11,
          fontWeight: '600',
        },
        headerRight: () => <HeaderRight />,
        headerShadowVisible: false,
        headerStyle: { backgroundColor: '#ffffff' },
        headerTitleStyle: { fontWeight: '700', fontSize: 18, color: PURPLE },
      }}
    >
      {/* 1 — Profile */}
      <Tabs.Screen
        name="profile"
        options={{
          title: 'Home',
          tabBarIcon: ({ color, size }) => (
            <Ionicons name="person-outline" size={size} color={color} />
          ),
          headerShown: false,
        }}
      />

      {/* 2 — Feed */}
      <Tabs.Screen
        name="index"
        options={{
          title: 'Feed',
          tabBarIcon: ({ color, size }) => (
            <Ionicons name="home-outline" size={size} color={color} />
          ),
          headerShown: false,
        }}
      />

      {/* 3 — Give (centre FAB) */}
      <Tabs.Screen
        name="give"
        options={{
          title: 'Give',
          tabBarIcon: ({ focused }) => (
            <View
              style={{
                marginTop: -16,
                height: 56,
                width: 56,
                alignItems: 'center',
                justifyContent: 'center',
                borderRadius: 28,
                backgroundColor: '#ffffff',
                borderWidth: 2.5,
                borderColor: focused ? NAV_ACT : NAV_INACT,
                shadowColor: PURPLE,
                shadowOffset: { width: 0, height: 4 },
                shadowOpacity: 0.2,
                shadowRadius: 8,
                elevation: 6,
              }}
            >
              <Ionicons name="add" size={28} color={focused ? NAV_ACT : NAV_INACT} />
            </View>
          ),
          tabBarLabel: () => null,
          headerShown: false,
        }}
      />

      {/* 4 — Leaders */}
      <Tabs.Screen
        name="leaderboard"
        options={{
          title: 'Leaders',
          tabBarIcon: ({ color, size }) => (
            <Ionicons name="trophy-outline" size={size} color={color} />
          ),
          href: flags.leaderboards_enabled ? '/(tabs)/leaderboard' : null,
          headerShown: false,
        }}
      />

      {/* 5 — Rewards */}
      <Tabs.Screen
        name="rewards"
        options={{
          title: 'Rewards',
          tabBarIcon: ({ color, size }) => (
            <Ionicons name="gift-outline" size={size} color={color} />
          ),
          href: flags.rewards_enabled ? '/(tabs)/rewards' : null,
          headerShown: false,
        }}
      />
    </Tabs>
  );
}
