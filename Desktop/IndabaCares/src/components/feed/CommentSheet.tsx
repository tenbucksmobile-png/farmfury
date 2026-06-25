import React, { useState, useRef } from 'react';
import {
  Modal,
  View,
  Text,
  TextInput,
  Pressable,
  FlatList,
  KeyboardAvoidingView,
  Platform,
  ActivityIndicator,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import { formatRelativeTime } from '@/utils/format';
import {
  useRecognitionComments,
  useAddRecognitionComment,
  useDeleteRecognitionComment,
  type RecognitionComment,
} from '@/hooks/use-recognition-comments';
import { useEmployee } from '@/providers/EmployeeContext';

interface CommentSheetProps {
  recognitionId: string;
  visible: boolean;
  onClose: () => void;
}

export function CommentSheet({ recognitionId, visible, onClose }: CommentSheetProps) {
  const insets = useSafeAreaInsets();
  const { employee } = useEmployee();
  const [body, setBody] = useState('');
  const inputRef = useRef<TextInput>(null);

  const { data: comments = [], isLoading } = useRecognitionComments(recognitionId);
  const addComment = useAddRecognitionComment(recognitionId);
  const deleteComment = useDeleteRecognitionComment(recognitionId);

  const handleSubmit = () => {
    const trimmed = body.trim();
    if (!trimmed) return;
    setBody('');
    addComment.mutate(trimmed);
  };

  const renderComment = ({ item }: { item: RecognitionComment }) => {
    const isOwn = item.employee.id === employee?.employee_id;
    return (
      <View className="mb-3 flex-row px-4">
        <Avatar name={item.employee.full_name} size="sm" />
        <View className="ml-2.5 flex-1 rounded-xl bg-slate-50 px-3 py-2.5">
          <View className="flex-row items-center justify-between">
            <Text className="text-xs font-semibold text-slate-800">
              {item.employee.full_name}
            </Text>
            <Text className="text-[10px] text-slate-400">
              {formatRelativeTime(item.created_at)}
            </Text>
          </View>
          {item.employee.position && (
            <Text className="mb-0.5 text-[10px] text-slate-400">{item.employee.position}</Text>
          )}
          <Text className="mt-0.5 text-sm leading-5 text-slate-700">{item.body}</Text>
          {isOwn && (
            <Pressable
              onPress={() => deleteComment.mutate(item.id)}
              className="mt-1 self-end"
            >
              <Text className="text-[10px] text-slate-400">Delete</Text>
            </Pressable>
          )}
        </View>
      </View>
    );
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={onClose}
    >
      <Pressable
        className="flex-1 bg-black/40"
        onPress={onClose}
      />

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        className="absolute bottom-0 left-0 right-0"
      >
        <View
          className="rounded-t-3xl bg-white"
          style={{ paddingBottom: insets.bottom + 8 }}
        >
          {/* Handle */}
          <View className="items-center pt-3 pb-2">
            <View className="h-1 w-10 rounded-full bg-slate-200" />
          </View>

          {/* Header */}
          <View className="flex-row items-center justify-between px-4 pb-3">
            <Text className="text-base font-bold text-slate-800">Comments</Text>
            <Pressable onPress={onClose} className="p-1">
              <Ionicons name="close" size={22} color="#94a3b8" />
            </Pressable>
          </View>

          {/* Comments list */}
          {isLoading ? (
            <View className="items-center py-8">
              <ActivityIndicator color="#ED6813" />
            </View>
          ) : (
            <FlatList
              data={comments}
              keyExtractor={(item) => item.id}
              renderItem={renderComment}
              style={{ maxHeight: 360 }}
              contentContainerStyle={{ paddingTop: 4, paddingBottom: 8 }}
              ListEmptyComponent={
                <View className="items-center py-8">
                  <Text className="text-sm text-slate-400">
                    No comments yet. Be the first!
                  </Text>
                </View>
              }
            />
          )}

          {/* Input row */}
          <View className="flex-row items-end border-t border-slate-100 px-4 pt-3">
            <Avatar name={employee?.full_name ?? '?'} size="sm" />
            <View className="ml-2.5 flex-1 rounded-2xl border border-slate-200 bg-slate-50 px-3 py-2">
              <TextInput
                ref={inputRef}
                value={body}
                onChangeText={setBody}
                placeholder="Write a comment…"
                placeholderTextColor="#94a3b8"
                multiline
                maxLength={500}
                className="text-sm text-slate-800"
                style={{ maxHeight: 100 }}
              />
            </View>
            <Pressable
              onPress={handleSubmit}
              disabled={!body.trim() || addComment.isPending}
              className="ml-2 mb-1 h-9 w-9 items-center justify-center rounded-full"
              style={{
                backgroundColor: body.trim() ? '#ED6813' : '#e2e8f0',
              }}
            >
              {addComment.isPending ? (
                <ActivityIndicator size="small" color="#fff" />
              ) : (
                <Ionicons
                  name="send"
                  size={16}
                  color={body.trim() ? '#fff' : '#94a3b8'}
                />
              )}
            </Pressable>
          </View>
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );
}
