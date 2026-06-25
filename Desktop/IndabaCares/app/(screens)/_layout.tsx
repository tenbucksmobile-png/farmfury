import React from 'react';
import { Pressable } from 'react-native';
import { Stack, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';

function BackIcon() {
  const router = useRouter();
  return (
    <Pressable
      onPress={() => router.back()}
      hitSlop={12}
      className="ml-1 h-9 w-9 items-center justify-center rounded-full active:bg-slate-100"
    >
      <Ionicons name="chevron-back" size={26} color="#0f172a" />
    </Pressable>
  );
}

export default function ScreensLayout() {
  return (
    <Stack
      screenOptions={{
        headerShadowVisible: false,
        headerStyle: { backgroundColor: '#ffffff' },
        headerTitleStyle: { fontWeight: '700', fontSize: 18 },
        headerLeft: () => <BackIcon />,
        headerBackVisible: false,
        animation: 'slide_from_right',
      }}
    />
  );
}
