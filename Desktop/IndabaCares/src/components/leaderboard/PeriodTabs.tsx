import React from 'react';
import { View, Text, Pressable, StyleSheet } from 'react-native';
import { PERIOD_LABELS, PERIOD_TABS, type PeriodType } from '@/api/leaderboard-service';

const PURPLE     = '#7B1FA2';
const PURPLE_MID = '#8E24AA';

interface PeriodTabsProps {
  value: PeriodType;
  onChange: (value: PeriodType) => void;
}

export function PeriodTabs({ value, onChange }: PeriodTabsProps) {
  return (
    <View style={styles.pill}>
      {PERIOD_TABS.map((period) => {
        const active = value === period;
        return (
          <Pressable
            key={period}
            onPress={() => onChange(period)}
            style={[styles.tab, active && styles.tabActive]}
          >
            <Text style={[styles.tabText, active && styles.tabTextActive]}>
              {PERIOD_LABELS[period]}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  pill: {
    flexDirection: 'row',
    backgroundColor: PURPLE_MID,
    borderRadius: 20,
    padding: 4,
    marginHorizontal: 20,
    marginTop: 12,
    marginBottom: 8,
  },
  tab: {
    flex: 1,
    alignItems: 'center',
    paddingVertical: 10,
    borderRadius: 16,
  },
  tabActive: {
    backgroundColor: '#ffffff',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.12,
    shadowRadius: 4,
    elevation: 3,
  },
  tabText: {
    fontSize: 13,
    fontWeight: '600',
    color: 'rgba(255,255,255,0.75)',
    letterSpacing: 0.3,
  },
  tabTextActive: {
    color: PURPLE,
  },
});
