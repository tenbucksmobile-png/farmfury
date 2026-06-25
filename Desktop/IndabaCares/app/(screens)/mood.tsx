import React, { useState } from 'react';
import { View, Text, ScrollView, Pressable } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useSubmitMood, useMoodHistory } from '@/hooks/use-mood';
import { MOOD_MAP, type MoodValue } from '@/lib/constants';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/ui/TextInput';
import { Card } from '@/components/ui/Card';
import { formatDate } from '@/utils/format';

export default function MoodScreen() {
  const insets = useSafeAreaInsets();
  const [selectedMood, setSelectedMood] = useState<MoodValue | null>(null);
  const [note, setNote] = useState('');

  const submitMood = useSubmitMood();
  const { data: history = [] } = useMoodHistory();

  const today = new Date().toISOString().split('T')[0];
  const moodSubmittedToday = history.some((e: any) => e.entry_date === today);

  const handleSubmit = () => {
    if (!selectedMood) return;
    submitMood.mutate({ mood: selectedMood, note: note || undefined });
  };

  return (
    <ScrollView
      className="flex-1 bg-slate-50 px-4 pt-4"
      contentContainerStyle={{ paddingBottom: insets.bottom + 100 }}
    >
      {/* Mood selector */}
      {!moodSubmittedToday ? (
        <Card className="mb-6">
          <Text className="mb-4 text-center text-lg font-bold text-slate-900">
            How are you feeling?
          </Text>

          <View className="mb-4 flex-row justify-around">
            {(Object.entries(MOOD_MAP) as [MoodValue, (typeof MOOD_MAP)[MoodValue]][]).map(
              ([key, val]) => (
                <Pressable
                  key={key}
                  onPress={() => setSelectedMood(key)}
                  className={`items-center rounded-2xl px-3 py-3 ${
                    selectedMood === key ? 'bg-primary-50 border-2 border-primary-300' : ''
                  }`}
                >
                  <Text className="text-3xl">{val.emoji}</Text>
                  <Text className="mt-1 text-xs font-medium text-slate-600">{val.label}</Text>
                </Pressable>
              )
            )}
          </View>

          <TextInput
            placeholder="Add a note (optional, only visible to admins)"
            value={note}
            onChangeText={setNote}
            multiline
            maxLength={500}
          />

          <Button
            title="Submit Mood"
            onPress={handleSubmit}
            loading={submitMood.isPending}
            disabled={!selectedMood}
          />
        </Card>
      ) : (
        <Card className="mb-6 items-center">
          <Text className="text-4xl">✅</Text>
          <Text className="mt-2 text-base font-semibold text-slate-800">
            Mood logged for today!
          </Text>
          <Text className="mt-1 text-sm text-slate-500">Come back tomorrow</Text>
        </Card>
      )}

      {/* History */}
      <Text className="mb-3 text-lg font-bold text-slate-900">Recent Mood History</Text>
      {history.length === 0 ? (
        <Text className="text-center text-sm text-slate-400">No mood entries yet</Text>
      ) : (
        history.map((entry: any) => {
          const moodInfo = MOOD_MAP[entry.mood as MoodValue];
          return (
            <View
              key={entry.id}
              className="mb-2 flex-row items-center rounded-xl bg-white px-4 py-3"
            >
              <Text className="text-2xl">{moodInfo?.emoji}</Text>
              <View className="ml-3 flex-1">
                <Text className="text-sm font-medium text-slate-700">
                  {moodInfo?.label}
                </Text>
                <Text className="text-xs text-slate-400">
                  {formatDate(entry.entry_date)}
                </Text>
              </View>
              <View
                className="h-3 w-3 rounded-full"
                style={{ backgroundColor: moodInfo?.color }}
              />
            </View>
          );
        })
      )}
    </ScrollView>
  );
}
