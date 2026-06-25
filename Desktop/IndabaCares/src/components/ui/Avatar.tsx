import React from 'react';
import { View, Text } from 'react-native';
import { Image } from 'expo-image';
import { getInitials } from '@/utils/format';

interface AvatarProps {
  uri?: string | null;
  name: string;
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl';
  showOnline?: boolean;
}

const SIZES = {
  xs: { container: 'h-6 w-6', text: 'text-[10px]' },
  sm: { container: 'h-8 w-8', text: 'text-xs' },
  md: { container: 'h-10 w-10', text: 'text-sm' },
  lg: { container: 'h-14 w-14', text: 'text-lg' },
  xl: { container: 'h-20 w-20', text: 'text-2xl' },
} as const;

const IMAGE_SIZES = { xs: 24, sm: 32, md: 40, lg: 56, xl: 80 } as const;

const DOT_SIZES = {
  xs: 'h-2 w-2 -bottom-0 -right-0 border',
  sm: 'h-2.5 w-2.5 -bottom-0 -right-0 border',
  md: 'h-3 w-3 -bottom-0.5 -right-0.5 border-[1.5px]',
  lg: 'h-3.5 w-3.5 -bottom-0.5 -right-0.5 border-2',
  xl: 'h-4 w-4 -bottom-0.5 -right-0.5 border-2',
} as const;

export function Avatar({ uri, name, size = 'md', showOnline }: AvatarProps) {
  const s = SIZES[size];

  const avatar = uri ? (
    <Image
      source={{ uri }}
      className={`${s.container} rounded-full`}
      style={{ width: IMAGE_SIZES[size], height: IMAGE_SIZES[size] }}
      contentFit="cover"
      transition={200}
    />
  ) : (
    <View className={`${s.container} items-center justify-center rounded-full bg-primary-100`}>
      <Text className={`${s.text} font-bold text-primary-700`}>
        {getInitials(name)}
      </Text>
    </View>
  );

  if (showOnline === undefined) return avatar;

  return (
    <View>
      {avatar}
      {showOnline && (
        <View
          className={`absolute rounded-full border-white bg-green-500 ${DOT_SIZES[size]}`}
        />
      )}
    </View>
  );
}
