import React from 'react';
import { View, Text } from 'react-native';

interface BadgeProps {
  label: string;
  color?: string;
  size?: 'sm' | 'md';
}

export function Badge({ label, color = '#ED6813', size = 'sm' }: BadgeProps) {
  return (
    <View
      className={`items-center justify-center rounded-full ${size === 'sm' ? 'px-2 py-0.5' : 'px-3 py-1'}`}
      style={{ backgroundColor: color + '20' }}
    >
      <Text
        className={`font-semibold ${size === 'sm' ? 'text-xs' : 'text-sm'}`}
        style={{ color }}
      >
        {label}
      </Text>
    </View>
  );
}
