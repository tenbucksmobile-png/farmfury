import React from 'react';
import {
  Modal,
  View,
  Text,
  FlatList,
  Pressable,
  ActivityIndicator,
  StyleSheet,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import { useGivenBadgeHistory, type GivenBadgeEntry } from '@/hooks/use-given-badge-history';

const PURPLE = '#7B1FA2';

function formatShortDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-ZA', {
    day:   'numeric',
    month: 'short',
  });
}

interface BadgeGivenSheetProps {
  cardType: 'recognition' | 'skills';
  onClose:  () => void;
}

export function BadgeGivenSheet({ cardType, onClose }: BadgeGivenSheetProps) {
  const insets = useSafeAreaInsets();
  const { data = [], isLoading } = useGivenBadgeHistory(cardType);

  const isRecognition = cardType === 'recognition';
  const title     = isRecognition ? 'Recognition Badges Given' : 'Skills Badges Given';
  const emoji     = isRecognition ? '🏅' : '🎓';
  const accentColor = isRecognition ? PURPLE : '#1d4ed8';

  const monthLabel = new Date().toLocaleDateString('en-ZA', { month: 'long', year: 'numeric' });

  const renderRow = ({ item }: { item: GivenBadgeEntry }) => (
    <View style={styles.row}>
      <Avatar
        uri={item.receiver.photo_url}
        name={item.receiver.full_name}
        size="lg"
      />
      <View style={styles.rowInfo}>
        <Text style={styles.rowName}>{item.receiver.full_name}</Text>
        <Text style={styles.rowDate}>{formatShortDate(item.created_at)}</Text>
      </View>
    </View>
  );

  return (
    <Modal
      visible
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      {/* Backdrop */}
      <Pressable style={styles.backdrop} onPress={onClose} />

      <View style={[styles.sheet, { paddingBottom: insets.bottom + 16 }]}>
        {/* Handle bar */}
        <View style={styles.handle} />

        {/* Header */}
        <View style={styles.header}>
          <View style={[styles.headerIcon, { backgroundColor: isRecognition ? '#f3e8ff' : '#eff6ff' }]}>
            <Text style={styles.headerEmoji}>{emoji}</Text>
          </View>
          <View style={styles.headerText}>
            <Text style={[styles.title, { color: accentColor }]}>{title}</Text>
            <Text style={styles.subtitle}>{monthLabel}</Text>
          </View>
          <Pressable onPress={onClose} hitSlop={10} style={styles.closeBtn}>
            <Ionicons name="close" size={22} color="#64748b" />
          </Pressable>
        </View>

        {/* Divider */}
        <View style={styles.divider} />

        {/* Content */}
        {isLoading ? (
          <ActivityIndicator color={accentColor} style={styles.loader} />
        ) : data.length === 0 ? (
          <View style={styles.emptyWrap}>
            <Text style={styles.emptyEmoji}>{emoji}</Text>
            <Text style={styles.emptyTitle}>None given yet</Text>
            <Text style={styles.emptyBody}>
              {isRecognition
                ? 'You haven\'t given any recognition badges this month.'
                : 'You haven\'t given any skills badges this month.'}
            </Text>
          </View>
        ) : (
          <FlatList
            data={data}
            keyExtractor={(item) => item.id}
            renderItem={renderRow}
            ItemSeparatorComponent={() => <View style={styles.rowDivider} />}
            showsVerticalScrollIndicator={false}
            contentContainerStyle={styles.listContent}
          />
        )}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
  },

  sheet: {
    backgroundColor: '#ffffff',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    maxHeight: '70%',
    paddingHorizontal: 20,
    paddingTop: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: -4 },
    shadowOpacity: 0.12,
    shadowRadius: 12,
    elevation: 16,
  },

  handle: {
    width: 40,
    height: 4,
    borderRadius: 2,
    backgroundColor: '#e2e8f0',
    alignSelf: 'center',
    marginBottom: 16,
  },

  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    marginBottom: 12,
  },

  headerIcon: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: 'center',
    justifyContent: 'center',
  },

  headerEmoji: {
    fontSize: 22,
  },

  headerText: {
    flex: 1,
  },

  title: {
    fontSize: 16,
    fontWeight: '800',
  },

  subtitle: {
    fontSize: 12,
    color: '#94a3b8',
    marginTop: 1,
  },

  closeBtn: {
    padding: 4,
  },

  divider: {
    height: 1,
    backgroundColor: '#f1f5f9',
    marginBottom: 4,
  },

  loader: {
    marginTop: 40,
    marginBottom: 40,
  },

  emptyWrap: {
    alignItems: 'center',
    paddingVertical: 40,
    gap: 8,
  },

  emptyEmoji: {
    fontSize: 40,
    marginBottom: 4,
  },

  emptyTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#1e293b',
  },

  emptyBody: {
    fontSize: 13,
    color: '#94a3b8',
    textAlign: 'center',
    lineHeight: 18,
    maxWidth: 260,
  },

  listContent: {
    paddingTop: 8,
    paddingBottom: 8,
  },

  row: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 10,
    gap: 14,
  },

  rowInfo: {
    flex: 1,
  },

  rowName: {
    fontSize: 15,
    fontWeight: '600',
    color: '#1e293b',
  },

  rowDate: {
    fontSize: 12,
    color: '#94a3b8',
    marginTop: 2,
  },

  rowDivider: {
    height: 1,
    backgroundColor: '#f8fafc',
  },
});
