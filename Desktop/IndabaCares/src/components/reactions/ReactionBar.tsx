import React, { useState } from 'react';
import { View, Text, Pressable } from 'react-native';
import { useEmployee } from '@/providers/EmployeeContext';
import { ReactionPicker } from './ReactionPicker';

interface Reaction {
  id: string;
  emoji: string;
  user_id: string;
}

interface ReactionBarProps {
  reactions: Reaction[];
  onAdd: (emoji: string) => void;
  onRemove: (reactionId: string) => void;
}

export function ReactionBar({ reactions, onAdd, onRemove }: ReactionBarProps) {
  const { employee } = useEmployee();
  const userId = employee?.employee_id;
  const [showPicker, setShowPicker] = useState(false);

  // Group reactions by emoji
  const grouped = reactions.reduce<Record<string, { count: number; myId?: string }>>((acc, r) => {
    if (!acc[r.emoji]) acc[r.emoji] = { count: 0 };
    acc[r.emoji].count++;
    if (r.user_id === userId) acc[r.emoji].myId = r.id;
    return acc;
  }, {});

  const handleEmojiPress = (emoji: string) => {
    const group = grouped[emoji];
    if (group?.myId) {
      onRemove(group.myId);
    } else {
      onAdd(emoji);
    }
  };

  return (
    <View className="flex-row flex-wrap items-center">
      {Object.entries(grouped).map(([emoji, data]) => (
        <Pressable
          key={emoji}
          onPress={() => handleEmojiPress(emoji)}
          className={`mr-2 mb-1 flex-row items-center rounded-full border px-2.5 py-1 ${
            data.myId ? 'border-primary-300 bg-primary-50' : 'border-slate-200 bg-slate-50'
          }`}
        >
          <Text className="text-sm">{emoji}</Text>
          <Text className={`ml-1 text-xs font-semibold ${data.myId ? 'text-primary-600' : 'text-slate-500'}`}>
            {data.count}
          </Text>
        </Pressable>
      ))}

      <Pressable
        onPress={() => setShowPicker(!showPicker)}
        className="mb-1 h-8 w-8 items-center justify-center rounded-full border border-dashed border-slate-300"
      >
        <Text className="text-sm">+</Text>
      </Pressable>

      {showPicker && (
        <ReactionPicker
          onSelect={(emoji) => {
            onAdd(emoji);
            setShowPicker(false);
          }}
          onClose={() => setShowPicker(false)}
        />
      )}
    </View>
  );
}
