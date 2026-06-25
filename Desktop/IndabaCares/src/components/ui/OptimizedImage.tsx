/**
 * OptimizedImage — PERF-04
 *
 * Drop-in replacement for React Native's <Image> backed by expo-image.
 * Features:
 *   - Blur-up placeholder while loading (blurhash or solid colour)
 *   - Fade-in transition on load
 *   - Disk + memory caching via expo-image's built-in cache
 *   - contentFit prop mirrors RN's resizeMode naming for easy migration
 */

import React from 'react';
import { StyleProp, ImageStyle } from 'react-native';
import { Image, ImageContentFit } from 'expo-image';

// A neutral 1×1 blurhash used as a placeholder before the real image loads.
// Generated from #E2E8F0 (Tailwind slate-200).
const DEFAULT_BLURHASH = 'L4Q],i00IU~q00~q00xu00D%xu~q';

interface OptimizedImageProps {
  uri: string | null | undefined;
  style?: StyleProp<ImageStyle>;
  contentFit?: ImageContentFit;
  /** Override the blurhash placeholder. Pass `null` to disable. */
  placeholder?: string | null;
  /** Fade duration in ms (default 250). */
  transition?: number;
  /** Accessible label. */
  accessibilityLabel?: string;
}

export function OptimizedImage({
  uri,
  style,
  contentFit = 'cover',
  placeholder = DEFAULT_BLURHASH,
  transition = 250,
  accessibilityLabel,
}: OptimizedImageProps) {
  return (
    <Image
      source={uri ? { uri } : undefined}
      style={style}
      contentFit={contentFit}
      placeholder={placeholder ? { blurhash: placeholder } : undefined}
      transition={transition}
      cachePolicy="memory-disk"
      accessibilityLabel={accessibilityLabel}
      recyclingKey={uri ?? undefined}
    />
  );
}
