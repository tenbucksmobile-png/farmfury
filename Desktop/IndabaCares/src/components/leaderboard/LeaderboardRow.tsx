import React, { memo } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Avatar } from '@/components/ui/Avatar';
import { useEmployee } from '@/providers/EmployeeContext';
import type { LeaderboardEntry } from '@/api/leaderboard-service';

const PURPLE = '#7B1FA2';

interface LeaderboardRowProps {
  entry: LeaderboardEntry;
  isFirst?: boolean;
  isLast?: boolean;
}

export const LeaderboardRow = memo(function LeaderboardRow({ entry, isFirst, isLast }: LeaderboardRowProps) {
  const { employee } = useEmployee();
  const isMe   = entry.employee_id === employee?.employee_id;
  const delta  = entry.movement_delta ?? 0;
  const isUp   = delta > 0;
  const isDown = delta < 0;
  const moveColor = isUp ? '#22C55E' : isDown ? '#EF4444' : '#9CA3AF';

  const radiusStyle = {
    borderTopLeftRadius:     isFirst ? 16 : 0,
    borderTopRightRadius:    isFirst ? 16 : 0,
    borderBottomLeftRadius:  isLast  ? 16 : 0,
    borderBottomRightRadius: isLast  ? 16 : 0,
  };

  return (
    <View style={[styles.card, radiusStyle, isMe && styles.cardHighlight]}>

      {/* Movement indicator */}
      <View style={styles.movement}>
        <Text style={[styles.moveNumber, { color: moveColor }]}>
          {delta !== 0 ? Math.abs(delta) : entry.rank}
        </Text>
        <Text style={[styles.arrow, { color: moveColor }]}>
          {isUp ? '▲' : isDown ? '▼' : '—'}
        </Text>
      </View>

      {/* Avatar */}
      <Avatar uri={entry.avatar_url} name={entry.full_name} size="sm" />

      {/* Name + job title */}
      <View style={styles.info}>
        <Text style={[styles.name, isMe && styles.nameMe]} numberOfLines={1}>
          {entry.full_name}{isMe ? ' · You' : ''}
        </Text>
        {entry.job_title ? (
          <Text style={styles.jobTitle} numberOfLines={1}>{entry.job_title}</Text>
        ) : null}
      </View>

      {/* Points */}
      <View style={styles.points}>
        <Text style={styles.pointsStar}>⭐</Text>
        <Text style={styles.pointsValue}>{entry.total_points}</Text>
      </View>

    </View>
  );
});

const styles = StyleSheet.create({
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#ffffff',
    marginHorizontal: 16,
    paddingVertical: 12,
    paddingHorizontal: 14,
    gap: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 8,
    elevation: 3,
  },
  cardHighlight: {
    backgroundColor: '#F5F3FF',
  },
  movement: {
    width: 34,
    alignItems: 'center',
  },
  moveNumber: {
    fontSize: 13,
    fontWeight: '700',
    lineHeight: 16,
  },
  arrow: {
    fontSize: 9,
    lineHeight: 12,
  },
  info: {
    flex: 1,
  },
  name: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1F2937',
  },
  nameMe: {
    color: PURPLE,
  },
  jobTitle: {
    fontSize: 12,
    color: '#6B7280',
    marginTop: 1,
  },
  points: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  pointsStar: {
    fontSize: 14,
  },
  pointsValue: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1F2937',
  },
});
