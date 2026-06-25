import React from 'react';
import { View } from 'react-native';

interface ProgressBarProps {
  progress: number; // 0-1
  color?: string;
  height?: number;
}

export function ProgressBar({ progress, color = '#ED6813', height = 6 }: ProgressBarProps) {
  const clampedProgress = Math.min(Math.max(progress, 0), 1);

  return (
    <View
      className="w-full overflow-hidden rounded-full bg-slate-100"
      style={{ height }}
    >
      <View
        className="h-full rounded-full"
        style={{
          width: `${clampedProgress * 100}%`,
          backgroundColor: color,
        }}
      />
    </View>
  );
}
