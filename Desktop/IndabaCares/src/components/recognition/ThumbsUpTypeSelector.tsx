import React from 'react';
import { View, Text, Pressable, ScrollView, ActivityIndicator } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { thumbsUpTypesQuery } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

interface ThumbsUpType {
  id: string;
  name: string;
  icon: string;
  color: string;
  stars_awarded: number;
  description: string | null;
}

interface ThumbsUpTypeSelectorProps {
  selectedId: string | null;
  onSelect: (type: ThumbsUpType) => void;
}

export function ThumbsUpTypeSelector({ selectedId, onSelect }: ThumbsUpTypeSelectorProps) {
  const { employee } = useEmployee();

  const { data: types = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.thumbsUpTypes,
    queryFn: async () => {
      if (!employee) return [];
      const { data, error } = await thumbsUpTypesQuery(employee.hotel);
      if (error) throw error;
      return data ?? [];
    },
    enabled: !!employee,
    staleTime: Infinity,
  });

  if (isLoading) {
    return <ActivityIndicator className="py-4" color="#ED6813" />;
  }

  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} className="mb-4">
      {types.map((type: ThumbsUpType) => {
        const isSelected = type.id === selectedId;
        return (
          <Pressable
            key={type.id}
            onPress={() => onSelect(type)}
            className={`mr-3 items-center rounded-2xl border-2 px-4 py-3 ${
              isSelected ? 'border-primary-500 bg-primary-50' : 'border-slate-100 bg-white'
            }`}
            style={{ minWidth: 100 }}
          >
            <Text className="text-2xl">{type.icon}</Text>
            <Text
              className={`mt-1 text-xs font-semibold ${
                isSelected ? 'text-primary-700' : 'text-slate-600'
              }`}
            >
              {type.name}
            </Text>
            <Text className="mt-0.5 text-[10px] text-slate-400">
              {type.stars_awarded} star{type.stars_awarded !== 1 ? 's' : ''}
            </Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}
