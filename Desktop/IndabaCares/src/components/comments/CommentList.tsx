import React from 'react';
import { View, Text, Pressable } from 'react-native';
import { Avatar } from '@/components/ui/Avatar';
import { formatRelativeTime } from '@/utils/format';
import { useEmployee } from '@/providers/EmployeeContext';

interface Comment {
  id: string;
  body: string;
  created_at: string;
  user: {
    id: string;
    full_name: string;
    display_name: string | null;
    avatar_url: string | null;
  };
}

interface CommentListProps {
  comments: Comment[];
  onDelete?: (commentId: string) => void;
}

export function CommentList({ comments, onDelete }: CommentListProps) {
  const { employee } = useEmployee();
  const userId = employee?.employee_id;

  if (comments.length === 0) {
    return (
      <View className="py-4">
        <Text className="text-center text-sm text-slate-400">No comments yet</Text>
      </View>
    );
  }

  return (
    <View>
      {comments.map((comment) => {
        const isOwn = comment.user.id === userId;
        return (
          <View key={comment.id} className="mb-3 flex-row">
            <Avatar uri={comment.user.avatar_url} name={comment.user.full_name} size="sm" />
            <View className="ml-2.5 flex-1 rounded-xl bg-slate-50 px-3 py-2">
              <View className="flex-row items-center justify-between">
                <Text className="text-xs font-semibold text-slate-700">
                  {comment.user.display_name || comment.user.full_name}
                </Text>
                <Text className="text-[10px] text-slate-400">
                  {formatRelativeTime(comment.created_at)}
                </Text>
              </View>
              <Text className="mt-0.5 text-sm text-slate-600">{comment.body}</Text>
              {isOwn && onDelete && (
                <Pressable onPress={() => onDelete(comment.id)} className="mt-1 self-end">
                  <Text className="text-[10px] text-slate-400">Delete</Text>
                </Pressable>
              )}
            </View>
          </View>
        );
      })}
    </View>
  );
}
