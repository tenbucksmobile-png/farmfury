import React, { useState } from 'react';
import { View, Text, Pressable, FlatList } from 'react-native';
import { TextInput } from '@/components/ui/TextInput';
import { Avatar } from '@/components/ui/Avatar';
import { useSearchProfiles } from '@/hooks/use-profiles';
import { useEmployee } from '@/providers/EmployeeContext';
import { MAX_RECIPIENTS } from '@/lib/constants';

interface Employee {
  id: string;
  full_name: string;
  employee_code: string;
  hotel: string;
  position: string | null;
  department: string | null;
}

interface RecipientPickerProps {
  selected: Employee[];
  onSelect: (employee: Employee) => void;
  onRemove: (id: string) => void;
}

export function RecipientPicker({ selected, onSelect, onRemove }: RecipientPickerProps) {
  const [search, setSearch] = useState('');
  const { employee } = useEmployee();
  const { data: results = [], isLoading } = useSearchProfiles(search);

  const filtered = (results as Employee[]).filter(
    (e: Employee) => e.id !== employee?.employee_id && !selected.some((s) => s.id === e.id)
  );

  return (
    <View>
      {/* Selected chips */}
      {selected.length > 0 && (
        <View className="mb-2 flex-row flex-wrap">
          {selected.map((e) => (
            <Pressable
              key={e.id}
              onPress={() => onRemove(e.id)}
              className="mb-2 mr-2 flex-row items-center rounded-full bg-primary-50 px-3 py-1.5"
            >
              <Avatar uri={null} name={e.full_name} size="xs" />
              <Text className="ml-1.5 text-sm font-medium text-primary-700">
                {e.full_name}
              </Text>
              <Text className="ml-1.5 text-xs text-primary-400">✕</Text>
            </Pressable>
          ))}
        </View>
      )}

      {selected.length < MAX_RECIPIENTS && (
        <>
          <TextInput
            placeholder="Search colleagues..."
            value={search}
            onChangeText={setSearch}
          />

          {search.length >= 2 && (
            <View className="max-h-48 rounded-xl border border-slate-100 bg-white">
              {isLoading ? (
                <Text className="p-4 text-center text-sm text-slate-400">Searching...</Text>
              ) : filtered.length === 0 ? (
                <Text className="p-4 text-center text-sm text-slate-400">No results</Text>
              ) : (
                <FlatList
                  data={filtered}
                  keyExtractor={(item) => item.id}
                  renderItem={({ item }) => (
                    <Pressable
                      onPress={() => {
                        onSelect(item);
                        setSearch('');
                      }}
                      className="flex-row items-center px-4 py-3 active:bg-slate-50"
                    >
                      <Avatar uri={null} name={item.full_name} size="sm" />
                      <View className="ml-3">
                        <Text className="text-sm font-medium text-slate-800">
                          {item.full_name}
                        </Text>
                        {item.position && (
                          <Text className="text-xs text-slate-400">{item.position}</Text>
                        )}
                      </View>
                    </Pressable>
                  )}
                />
              )}
            </View>
          )}
        </>
      )}
    </View>
  );
}
