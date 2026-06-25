import React from 'react';
import { View, Text, ScrollView } from 'react-native';
import { router } from 'expo-router';
import { useEmployee } from '@/providers/EmployeeContext';
import { Button } from '@/components/ui/Button';

export default function EditProfileScreen() {
  const { employee } = useEmployee();

  return (
    <ScrollView className="flex-1 bg-white px-6 pt-6" keyboardShouldPersistTaps="handled">
      {/* Initials avatar */}
      <View className="mb-6 items-center">
        <View className="h-20 w-20 items-center justify-center rounded-full bg-primary-100">
          <Text className="text-2xl font-bold text-primary-600">
            {employee?.full_name
              ? employee.full_name
                  .split(' ')
                  .map((n) => n[0])
                  .slice(0, 2)
                  .join('')
                  .toUpperCase()
              : '?'}
          </Text>
        </View>
      </View>

      <InfoRow label="Full Name"     value={employee?.full_name     || '—'} />
      <InfoRow label="Employee Code" value={employee?.employee_code || '—'} />
      <InfoRow label="Hotel"         value={employee?.hotel         || '—'} />
      <InfoRow label="Department"    value={employee?.department    || '—'} />
      <InfoRow label="Position"      value={employee?.position      || '—'} />

      <View className="mt-8">
        <Button title="Back" variant="outline" onPress={() => router.back()} />
      </View>
    </ScrollView>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <View className="mb-4 border-b border-slate-100 pb-4">
      <Text className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-400">
        {label}
      </Text>
      <Text className="text-base text-slate-800">{value}</Text>
    </View>
  );
}
