import React from 'react';
import { View, Text, ScrollView, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import Constants from 'expo-constants';
import { router } from 'expo-router';
import { useEmployee } from '@/providers/EmployeeContext';

const PURPLE = '#7C3AED';
const DANGER = '#ef4444';

export default function SettingsScreen() {
  const { employee, clearEmployee } = useEmployee();

  return (
    <ScrollView className="flex-1 bg-slate-50">
      {/* Account info */}
      <View className="mx-4 mt-4 rounded-2xl bg-white p-4">
        <Text className="mb-3 text-sm font-semibold text-slate-400">ACCOUNT</Text>
        <InfoRow label="Name"          value={employee?.full_name     || ''} />
        <InfoRow label="Employee Code" value={employee?.employee_code || ''} />
        <InfoRow label="Hotel"         value={employee?.hotel         || ''} />
      </View>

      {/* App info */}
      <View className="mx-4 mt-4 rounded-2xl bg-white p-4">
        <Text className="mb-3 text-sm font-semibold text-slate-400">APP</Text>
        <InfoRow label="Version" value={Constants.expoConfig?.version || '1.0.0'} />
        <InfoRow label="Build"   value={String(Constants.expoConfig?.extra?.buildNumber || '1')} />
      </View>

      {/* Legal */}
      <View className="mx-4 mt-4 rounded-2xl bg-white p-4">
        <Text className="mb-3 text-sm font-semibold text-slate-400">LEGAL</Text>
        <NavRow
          label="Privacy Policy"
          icon="shield-checkmark-outline"
          onPress={() => router.push('/(screens)/privacy-policy' as any)}
        />
        <View style={{ height: 1, backgroundColor: '#f1f5f9', marginVertical: 4 }} />
        <NavRow
          label="Terms of Service"
          icon="document-text-outline"
          onPress={() => router.push('/(screens)/terms-of-service' as any)}
        />
      </View>

      {/* Danger zone */}
      <View className="mx-4 mt-4 rounded-2xl bg-white p-4">
        <Text className="mb-3 text-sm font-semibold text-slate-400">ACCOUNT MANAGEMENT</Text>
        <NavRow
          label="Delete My Account"
          icon="trash-outline"
          color={DANGER}
          onPress={() => router.push('/(screens)/delete-account' as any)}
        />
      </View>

      {/* Sign Out */}
      <TouchableOpacity
        onPress={() => clearEmployee()}
        className="mx-4 mt-6 flex-row items-center justify-center rounded-xl border border-red-200 bg-red-50 py-4"
      >
        <Ionicons name="log-out-outline" size={20} color="#ef4444" />
        <Text className="ml-2 text-base font-semibold text-red-500">Sign Out</Text>
      </TouchableOpacity>

      <View className="mt-8 items-center pb-10">
        <Text className="text-xs text-slate-300">
          IndabaCares v{Constants.expoConfig?.version || '1.0.0'}
        </Text>
      </View>
    </ScrollView>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <View className="mb-3 flex-row justify-between">
      <Text className="text-sm text-slate-500">{label}</Text>
      <Text className="text-sm font-medium text-slate-800">{value}</Text>
    </View>
  );
}

function NavRow({
  label,
  icon,
  color = PURPLE,
  onPress,
}: {
  label: string;
  icon: React.ComponentProps<typeof Ionicons>['name'];
  color?: string;
  onPress: () => void;
}) {
  return (
    <TouchableOpacity
      onPress={onPress}
      style={{ flexDirection: 'row', alignItems: 'center', paddingVertical: 8 }}
    >
      <Ionicons name={icon} size={18} color={color} style={{ marginRight: 10 }} />
      <Text style={{ flex: 1, fontSize: 14, color }}>{label}</Text>
      <Ionicons name="chevron-forward" size={16} color="#94a3b8" />
    </TouchableOpacity>
  );
}
