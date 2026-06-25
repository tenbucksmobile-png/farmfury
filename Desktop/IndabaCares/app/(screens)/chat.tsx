/**
 * Hotel Chat Screen
 *
 * WhatsApp-style real-time group chat scoped to the employee's hotel.
 *
 * Layout:
 *   - Inverted FlatList so the newest message is at the bottom
 *   - Sender's own messages → right-aligned, fuchsia bubble
 *   - Others' messages      → left-aligned, white bubble with avatar
 *   - Sticky input bar above keyboard with send button
 */

import React, { useState, useRef, useCallback } from 'react';
import {
  View,
  Text,
  FlatList,
  TextInput,
  Pressable,
  KeyboardAvoidingView,
  Platform,
  ActivityIndicator,
  Alert,
  StyleSheet,
} from 'react-native';
import { Stack } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useChat } from '@/hooks/use-chat';
import { useEmployee } from '@/providers/EmployeeContext';
import type { ChatMessage } from '@/api/chat-service';

// ─── Avatar ────────────────────────────────────────────────────────────────────

function ChatAvatar({ name }: { name: string }) {
  const initials = name
    .split(' ')
    .slice(0, 2)
    .map((w) => w[0]?.toUpperCase() ?? '')
    .join('');

  // Generate a consistent pastel colour from the name
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
  const hue = Math.abs(hash) % 360;

  return (
    <View
      style={[
        styles.avatar,
        { backgroundColor: `hsl(${hue}, 55%, 75%)` },
      ]}
    >
      <Text style={styles.avatarText}>{initials}</Text>
    </View>
  );
}

// ─── Date separator ───────────────────────────────────────────────────────────

function DateSeparator({ date }: { date: string }) {
  const d = new Date(date);
  const today = new Date();
  const yesterday = new Date(today);
  yesterday.setDate(today.getDate() - 1);

  let label: string;
  if (d.toDateString() === today.toDateString()) {
    label = 'Today';
  } else if (d.toDateString() === yesterday.toDateString()) {
    label = 'Yesterday';
  } else {
    label = d.toLocaleDateString(undefined, { day: 'numeric', month: 'long', year: 'numeric' });
  }

  return (
    <View style={styles.dateSeparator}>
      <View style={styles.dateLine} />
      <Text style={styles.dateLabel}>{label}</Text>
      <View style={styles.dateLine} />
    </View>
  );
}

// ─── Message bubble ───────────────────────────────────────────────────────────

interface BubbleProps {
  msg:        ChatMessage;
  isMe:       boolean;
  showName:   boolean;
  showAvatar: boolean;
}

function MessageBubble({ msg, isMe, showName, showAvatar }: BubbleProps) {
  const time = new Date(msg.created_at).toLocaleTimeString([], {
    hour:   '2-digit',
    minute: '2-digit',
  });

  const isOptimistic = msg.id.startsWith('optimistic-');

  if (isMe) {
    return (
      <View style={styles.rowRight}>
        <View style={[styles.bubbleRight, isOptimistic && styles.bubbleOptimistic]}>
          <Text style={styles.bubbleTextRight}>{msg.body}</Text>
          <View style={styles.timestampRow}>
            <Text style={styles.timeRight}>{time}</Text>
            {isOptimistic ? (
              <Ionicons name="time-outline" size={11} color="rgba(255,255,255,0.6)" style={{ marginLeft: 3 }} />
            ) : (
              <Ionicons name="checkmark-done" size={11} color="rgba(255,255,255,0.7)" style={{ marginLeft: 3 }} />
            )}
          </View>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.rowLeft}>
      {/* Avatar column — always reserve space to keep bubbles aligned */}
      <View style={styles.avatarSlot}>
        {showAvatar && <ChatAvatar name={msg.sender.full_name} />}
      </View>

      <View style={styles.bubbleLeft}>
        {showName && (
          <Text style={styles.senderName} numberOfLines={1}>
            {msg.sender.full_name}
          </Text>
        )}
        <Text style={styles.bubbleTextLeft}>{msg.body}</Text>
        <Text style={styles.timeLeft}>{time}</Text>
      </View>
    </View>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function ChatScreen() {
  const { employee } = useEmployee();
  const { messages, isLoading, isSending, isLoadingMore, hasMore, error, send, loadMore, myId } = useChat();
  const insets = useSafeAreaInsets();
  const hotelName = employee?.hotel ?? 'Hotel Chat';

  const [text, setText] = useState('');
  const inputRef = useRef<TextInput>(null);

  const handleSend = useCallback(async () => {
    const trimmed = text.trim();
    if (!trimmed || isSending) return;
    setText('');
    try {
      await send(trimmed);
    } catch (e: any) {
      Alert.alert('Failed to send', e.message ?? 'Please try again.');
    }
  }, [text, isSending, send]);

  // ── Render helpers ──────────────────────────────────────────────────────────

  const renderItem = useCallback(
    ({ item, index }: { item: ChatMessage; index: number }) => {
      const isMe = item.sender.id === myId;

      // The list is inverted, so index 0 is the newest (bottom of screen).
      // "next" in display terms is the message above = index + 1 in data array.
      const nextMsg  = messages[index + 1];
      const prevMsg  = messages[index - 1];

      const sameSenderAsNext = nextMsg?.sender.id === item.sender.id;
      const sameSenderAsPrev = prevMsg?.sender.id === item.sender.id;

      // Show sender name on the topmost bubble in a group (first occurrence
      // going upwards, which in the inverted list is the last in the array chunk)
      const showName   = !isMe && !sameSenderAsNext;
      const showAvatar = !isMe && !sameSenderAsPrev;

      // Date separator: show when the day changes compared to the message above
      // (which in the inverted list is index + 1)
      const showDate =
        !nextMsg ||
        new Date(item.created_at).toDateString() !==
          new Date(nextMsg.created_at).toDateString();

      return (
        <View>
          {showDate && <DateSeparator date={item.created_at} />}
          <MessageBubble
            msg={item}
            isMe={isMe}
            showName={showName}
            showAvatar={showAvatar}
          />
        </View>
      );
    },
    [messages, myId],
  );

  // ── Loading / Error ─────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#ED6813" />
      </View>
    );
  }

  if (error) {
    return (
      <View style={styles.center}>
        <Text style={styles.errorText}>Unable to load chat</Text>
        <Text style={styles.errorSub}>{error}</Text>
      </View>
    );
  }

  // ── Main render ─────────────────────────────────────────────────────────────

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 88 : 0}
    >
      <Stack.Screen
        options={{
          title: hotelName,
          headerTitleStyle: { fontWeight: '700', fontSize: 16 },
        }}
      />
      {/* Messages */}
      <FlatList
        data={messages}
        keyExtractor={(item) => item.id}
        renderItem={renderItem}
        inverted
        contentContainerStyle={[
          styles.listContent,
          { paddingBottom: 8 },
        ]}
        onEndReached={hasMore ? loadMore : undefined}
        onEndReachedThreshold={0.3}
        ListFooterComponent={
          hasMore ? (
            <View style={styles.loadMoreWrap}>
              {isLoadingMore
                ? <ActivityIndicator size="small" color="#94a3b8" />
                : <Pressable onPress={loadMore} style={styles.loadMoreBtn}>
                    <Text style={styles.loadMoreText}>Load earlier messages</Text>
                  </Pressable>
              }
            </View>
          ) : null
        }
        ListEmptyComponent={
          <View style={styles.emptyWrap}>
            <Text style={styles.emptyIcon}>💬</Text>
            <Text style={styles.emptyTitle}>No messages yet</Text>
            <Text style={styles.emptySub}>Be the first to say something!</Text>
          </View>
        }
        showsVerticalScrollIndicator={false}
      />

      {/* Input bar */}
      <View
        style={[
          styles.inputBar,
          { paddingBottom: Math.max(insets.bottom, 12) },
        ]}
      >
        <TextInput
          ref={inputRef}
          value={text}
          onChangeText={setText}
          placeholder="Message..."
          placeholderTextColor="#94a3b8"
          multiline
          style={styles.input}
          returnKeyType="default"
          blurOnSubmit={false}
        />
        <Pressable
          onPress={handleSend}
          disabled={!text.trim() || isSending}
          style={({ pressed }) => [
            styles.sendBtn,
            (!text.trim() || isSending) && styles.sendBtnDisabled,
            pressed && styles.sendBtnPressed,
          ]}
        >
          {isSending ? (
            <ActivityIndicator size="small" color="#fff" />
          ) : (
            <Ionicons name="send" size={18} color="#fff" />
          )}
        </Pressable>
      </View>
    </KeyboardAvoidingView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const BUBBLE_MAX_WIDTH = '78%';
const FUCHSIA = '#ED6813';

const styles = StyleSheet.create({
  flex:           { flex: 1, backgroundColor: '#f8fafc' },
  center:         { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#f8fafc' },
  listContent:    { paddingHorizontal: 12, paddingTop: 8 },

  // ─ Rows ────────────────────────────────────────────────────────────────────
  rowRight: {
    flexDirection:  'row',
    justifyContent: 'flex-end',
    marginBottom:   2,
    paddingLeft:    48,
  },
  rowLeft: {
    flexDirection:  'row',
    alignItems:     'flex-end',
    marginBottom:   2,
    paddingRight:   48,
  },

  // ─ Avatar ──────────────────────────────────────────────────────────────────
  avatarSlot:   { width: 32, marginRight: 6 },
  avatar:       { width: 32, height: 32, borderRadius: 16, alignItems: 'center', justifyContent: 'center' },
  avatarText:   { fontSize: 12, fontWeight: '700', color: '#fff' },

  // ─ Bubbles ─────────────────────────────────────────────────────────────────
  bubbleRight: {
    maxWidth:      BUBBLE_MAX_WIDTH,
    backgroundColor: FUCHSIA,
    borderRadius:  18,
    borderBottomRightRadius: 4,
    paddingHorizontal: 14,
    paddingTop:    9,
    paddingBottom: 6,
    shadowColor:   FUCHSIA,
    shadowOffset:  { width: 0, height: 2 },
    shadowOpacity: 0.25,
    shadowRadius:  6,
    elevation:     3,
  },
  bubbleOptimistic: { opacity: 0.75 },

  bubbleLeft: {
    maxWidth:      BUBBLE_MAX_WIDTH,
    backgroundColor: '#ffffff',
    borderRadius:  18,
    borderBottomLeftRadius: 4,
    paddingHorizontal: 14,
    paddingTop:    9,
    paddingBottom: 6,
    shadowColor:   '#000',
    shadowOffset:  { width: 0, height: 1 },
    shadowOpacity: 0.06,
    shadowRadius:  4,
    elevation:     1,
  },

  // ─ Text ────────────────────────────────────────────────────────────────────
  bubbleTextRight: { color: '#fff',    fontSize: 15, lineHeight: 21 },
  bubbleTextLeft:  { color: '#0f172a', fontSize: 15, lineHeight: 21 },
  senderName:      { color: FUCHSIA,   fontSize: 11, fontWeight: '700', marginBottom: 2 },

  timestampRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'flex-end', marginTop: 3 },
  timeRight:    { color: 'rgba(255,255,255,0.65)', fontSize: 10 },
  timeLeft:     { color: '#94a3b8', fontSize: 10, textAlign: 'right', marginTop: 3 },

  // ─ Date separator ──────────────────────────────────────────────────────────
  dateSeparator: { flexDirection: 'row', alignItems: 'center', marginVertical: 12, paddingHorizontal: 4 },
  dateLine:      { flex: 1, height: 1, backgroundColor: '#e2e8f0' },
  dateLabel:     { marginHorizontal: 10, fontSize: 11, fontWeight: '600', color: '#94a3b8' },

  // ─ Empty state ─────────────────────────────────────────────────────────────
  emptyWrap:  { flex: 1, alignItems: 'center', justifyContent: 'center', paddingTop: 80 },
  emptyIcon:  { fontSize: 48, marginBottom: 12 },
  emptyTitle: { fontSize: 18, fontWeight: '700', color: '#334155', marginBottom: 6 },
  emptySub:   { fontSize: 14, color: '#94a3b8', textAlign: 'center' },

  // ─ Error ───────────────────────────────────────────────────────────────────
  errorText: { fontSize: 16, fontWeight: '700', color: '#ef4444', marginBottom: 4 },
  errorSub:  { fontSize: 13, color: '#64748b', textAlign: 'center', paddingHorizontal: 32 },

  // ─ Input bar ───────────────────────────────────────────────────────────────
  inputBar: {
    flexDirection:   'row',
    alignItems:      'flex-end',
    paddingHorizontal: 12,
    paddingTop:      10,
    backgroundColor: '#ffffff',
    borderTopWidth:  1,
    borderTopColor:  '#f1f5f9',
    gap: 8,
  },
  input: {
    flex:            1,
    maxHeight:       120,
    backgroundColor: '#f1f5f9',
    borderRadius:    22,
    paddingHorizontal: 16,
    paddingTop:      10,
    paddingBottom:   10,
    fontSize:        15,
    color:           '#0f172a',
    lineHeight:      20,
  },
  sendBtn: {
    width:           44,
    height:          44,
    borderRadius:    22,
    backgroundColor: FUCHSIA,
    alignItems:      'center',
    justifyContent:  'center',
    shadowColor:     FUCHSIA,
    shadowOffset:    { width: 0, height: 3 },
    shadowOpacity:   0.35,
    shadowRadius:    8,
    elevation:       4,
  },
  sendBtnDisabled: { opacity: 0.4, shadowOpacity: 0 },
  sendBtnPressed:  { opacity: 0.8 },

  // ─ Load more ───────────────────────────────────────────────────────────────
  loadMoreWrap: { alignItems: 'center', paddingVertical: 14 },
  loadMoreBtn:  { paddingHorizontal: 20, paddingVertical: 8, borderRadius: 20, backgroundColor: '#f1f5f9' },
  loadMoreText: { fontSize: 12, fontWeight: '600', color: '#64748b' },
});
