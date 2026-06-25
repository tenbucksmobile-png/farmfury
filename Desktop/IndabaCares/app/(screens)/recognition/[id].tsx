import React from 'react';
import { View, Text, ScrollView, KeyboardAvoidingView, Platform } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useRecognitionDetail } from '@/hooks/use-recognition';
import { useReactions, useAddReaction, useRemoveReaction } from '@/hooks/use-reactions';
import { useComments, useAddComment, useDeleteComment } from '@/hooks/use-comments';
import { useRecognitionRealtime } from '@/hooks/use-realtime';
import { Avatar } from '@/components/ui/Avatar';
import { ReactionBar } from '@/components/reactions/ReactionBar';
import { CommentList } from '@/components/comments/CommentList';
import { CommentInput } from '@/components/comments/CommentInput';
import { TypingIndicator } from '@/components/comments/TypingIndicator';
import { SkeletonCard } from '@/components/ui/Skeleton';
import { formatRelativeTime } from '@/utils/format';
import { Image } from 'expo-image';

export default function RecognitionDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const insets = useSafeAreaInsets();

  const { data: recognition, isLoading } = useRecognitionDetail(id);
  const { data: reactions = [] } = useReactions(id);
  const { data: comments = [] } = useComments(id);
  const addReaction = useAddReaction(id);
  const removeReaction = useRemoveReaction(id);
  const addComment = useAddComment(id);
  const deleteComment = useDeleteComment(id);

  // Subscribe to realtime updates for this recognition (reactions, comments, typing)
  const { typingUsers, sendTyping } = useRecognitionRealtime(id);

  if (isLoading || !recognition) {
    return (
      <View className="flex-1 bg-white p-4">
        <SkeletonCard />
      </View>
    );
  }

  const sender = recognition.sender as any;
  const recipients = (recognition.recipients as any[]).map((r: any) => r.recipient);
  const thumbsUp = recognition.thumbs_up_type as any;

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      className="flex-1 bg-white"
      keyboardVerticalOffset={90}
    >
      <ScrollView
        contentContainerStyle={{ paddingBottom: 20 }}
        className="flex-1 px-4 pt-4"
      >
        {/* Boost badge */}
        {recognition.is_boosted && (
          <View className="mb-3 flex-row items-center rounded-lg bg-warning-50 px-3 py-2">
            <Ionicons name="rocket" size={16} color="#f59e0b" />
            <Text className="ml-2 text-sm font-semibold text-warning-600">
              Manager Boosted
            </Text>
          </View>
        )}

        {/* Sender */}
        <View className="mb-4 flex-row items-center">
          <Avatar uri={sender?.avatar_url} name={sender?.full_name || 'User'} size="lg" />
          <View className="ml-3 flex-1">
            <Text className="text-base font-bold text-slate-900">
              {sender?.display_name || sender?.full_name}
            </Text>
            <Text className="text-sm text-slate-400">
              {formatRelativeTime(recognition.created_at)}
            </Text>
          </View>
          <View
            className="items-center rounded-full px-3 py-1.5"
            style={{ backgroundColor: (thumbsUp?.color || '#ED6813') + '20' }}
          >
            <Text className="text-sm">
              {thumbsUp?.icon} {thumbsUp?.name}
            </Text>
          </View>
        </View>

        {/* Recipients */}
        <View className="mb-4">
          <Text className="mb-2 text-xs font-medium text-slate-400">RECOGNIZED</Text>
          {recipients.map((r: any) => (
            <View key={r.id} className="mb-2 flex-row items-center">
              <Avatar uri={r.avatar_url} name={r.full_name} size="sm" />
              <Text className="ml-2 text-sm font-medium text-slate-700">
                {r.display_name || r.full_name}
              </Text>
            </View>
          ))}
        </View>

        {/* Message */}
        <Text className="mb-4 text-base leading-7 text-slate-800">
          {recognition.message}
        </Text>

        {/* Image */}
        {recognition.image_url && (
          <Image
            source={{ uri: recognition.image_url }}
            className="mb-4 h-64 w-full rounded-2xl"
            contentFit="cover"
          />
        )}

        {/* Hashtags */}
        {recognition.hashtags?.length > 0 && (
          <View className="mb-4 flex-row flex-wrap">
            {recognition.hashtags.map((tag: string) => (
              <Text key={tag} className="mr-2 text-sm font-medium text-primary-500">
                #{tag}
              </Text>
            ))}
          </View>
        )}

        {/* Stars */}
        <View className="mb-4 flex-row items-center rounded-xl bg-warning-50 px-4 py-2">
          <Ionicons name="star" size={18} color="#f59e0b" />
          <Text className="ml-2 text-sm font-semibold text-warning-700">
            {recognition.stars_per_recipient} stars per recipient
          </Text>
        </View>

        {/* Reactions */}
        <View className="mb-4">
          <ReactionBar
            reactions={reactions}
            onAdd={(emoji) => addReaction.mutate(emoji)}
            onRemove={(reactionId) => removeReaction.mutate(reactionId)}
          />
        </View>

        {/* Comments */}
        <View className="mb-4">
          <Text className="mb-2 text-sm font-semibold text-slate-700">
            Comments ({comments.length})
          </Text>
          <CommentList
            comments={comments as any}
            onDelete={(commentId) => deleteComment.mutate(commentId)}
          />
          <TypingIndicator typingUsers={typingUsers} />
        </View>
      </ScrollView>

      {/* Comment input at bottom */}
      <View style={{ paddingBottom: insets.bottom }}>
        <CommentInput
          onSubmit={(body) => addComment.mutate(body)}
          onTyping={sendTyping}
          loading={addComment.isPending}
        />
      </View>
    </KeyboardAvoidingView>
  );
}
