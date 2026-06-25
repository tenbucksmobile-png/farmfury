import React from 'react';
import { View, Text, Pressable } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import type { AppNotification } from '@/api/notification-service';

interface NotificationItemProps {
  notification: AppNotification;
  onPress:      () => void;
}

// ─── Icon config per type ─────────────────────────────────────────────────────

const TYPE_CONFIG: Record<
  string,
  { icon: keyof typeof Ionicons.glyphMap; color: string; bg: string }
> = {
  recognition_received: {
    icon:  'star',
    color: '#7B1FA2',
    bg:    '#ede9fe',
  },
  reward_approved: {
    icon:  'checkmark-circle',
    color: '#22c55e',
    bg:    '#dcfce7',
  },
  reward_rejected: {
    icon:  'close-circle',
    color: '#ef4444',
    bg:    '#fee2e2',
  },
  admin_announcement: {
    icon:  'megaphone',
    color: '#7B1FA2',
    bg:    '#ede9fe',
  },
};

const FALLBACK = { icon: 'notifications' as const, color: '#7B1FA2', bg: '#ede9fe' };

// ─── Relative time ────────────────────────────────────────────────────────────

function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.floor(diff / 60_000);
  if (m < 1)  return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24);
  if (d < 7)  return `${d}d ago`;
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
}

// ─── Component ────────────────────────────────────────────────────────────────

export function NotificationItem({ notification, onPress }: NotificationItemProps) {
  const cfg = TYPE_CONFIG[notification.type] ?? FALLBACK;
  const unread = !notification.read;

  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => ({
        backgroundColor: pressed ? '#f5f3ff' : unread ? '#faf8ff' : '#ffffff',
      })}
      className="flex-row px-4 py-3.5"
    >
      {/* Icon */}
      <View
        className="mr-3 h-10 w-10 items-center justify-center rounded-full"
        style={{ backgroundColor: cfg.bg }}
      >
        <Ionicons name={cfg.icon} size={20} color={cfg.color} />
      </View>

      {/* Content */}
      <View className="flex-1 justify-center">
        <Text
          className="text-sm leading-snug"
          style={{ color: '#0f172a', fontWeight: unread ? '700' : '400' }}
          numberOfLines={2}
        >
          {notification.title}
        </Text>
        {!!notification.message && (
          <Text
            className="mt-0.5 text-xs leading-snug text-slate-500"
            numberOfLines={2}
          >
            {notification.message}
          </Text>
        )}
        <Text className="mt-1 text-[10px] text-slate-400">
          {relativeTime(notification.created_at)}
        </Text>
      </View>

      {/* Unread dot */}
      {unread && (
        <View
          className="ml-3 mt-1 h-2 w-2 self-start rounded-full"
          style={{ backgroundColor: '#7B1FA2' }}
        />
      )}
    </Pressable>
  );
}
