import React from 'react';
import { View, Text, FlatList, RefreshControl } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useQuery } from '@tanstack/react-query';
import { useEmployee } from '@/providers/EmployeeContext';
import { supabase } from '@/lib/supabase';
import { useStarTransactions, type StarTransaction } from '@/hooks/use-star-transactions';
import { Card } from '@/components/ui/Card';
import { EmptyState } from '@/components/ui/EmptyState';
import { SkeletonCard } from '@/components/ui/Skeleton';
import { formatRelativeTime } from '@/utils/format';

const TX_TYPE_CONFIG: Record<string, { icon: string; color: string; label: string }> = {
  recognition_received: { icon: 'arrow-down-circle', color: '#22c55e', label: 'Recognition' },
  admin_bonus:          { icon: 'gift',              color: '#8b5cf6', label: 'Bonus'       },
  campaign_reward:      { icon: 'rocket',            color: '#f59e0b', label: 'Campaign'    },
};

function TransactionRow({ tx }: { tx: StarTransaction }) {
  const config = TX_TYPE_CONFIG[tx.type] || { icon: 'ellipse', color: '#94a3b8', label: tx.type };

  return (
    <View className="flex-row items-center py-3">
      <View
        className="h-9 w-9 items-center justify-center rounded-full"
        style={{ backgroundColor: config.color + '15' }}
      >
        <Ionicons name={config.icon as any} size={18} color={config.color} />
      </View>
      <View className="ml-3 flex-1">
        <Text className="text-sm font-medium text-slate-800" numberOfLines={1}>
          {config.label}
        </Text>
        <Text className="text-[10px] text-slate-400">
          {formatRelativeTime(tx.created_at)}
        </Text>
      </View>
      <Text className="text-sm font-bold text-success-600">+{tx.amount}</Text>
    </View>
  );
}

export default function WalletScreen() {
  const { employee } = useEmployee();

  const { data: balanceData } = useQuery({
    queryKey: ['points-balance', employee?.employee_id],
    queryFn: async () => {
      if (!employee) return null;
      const { data, error } = await supabase
        .from('employees')
        .select('points_balance')
        .eq('id', employee.employee_id)
        .single();
      if (error) throw error;
      return (data as { points_balance: number }).points_balance;
    },
    enabled: !!employee,
    staleTime: 60 * 1000,
  });

  const { data: transactions = [], isLoading, refetch, isRefetching } = useStarTransactions();

  return (
    <FlatList
      data={transactions}
      keyExtractor={(item) => item.id}
      className="flex-1 bg-slate-50"
      contentContainerStyle={{ paddingBottom: 100 }}
      ListHeaderComponent={
        <View className="px-4 pt-4">
          <Card className="mb-4 items-center py-6">
            <Ionicons name="trophy" size={36} color="#ED6813" />
            <Text className="mt-2 text-3xl font-bold text-slate-900">
              {balanceData ?? 0}
            </Text>
            <Text className="text-sm text-slate-500">Points Balance</Text>
          </Card>

          <Text className="mb-2 px-1 text-xs font-semibold uppercase text-slate-400">
            Points History
          </Text>
        </View>
      }
      renderItem={({ item }) => (
        <View className="px-4">
          <TransactionRow tx={item} />
        </View>
      )}
      ItemSeparatorComponent={() => (
        <View className="mx-4 border-b border-slate-100" />
      )}
      ListEmptyComponent={
        !isLoading ? (
          <EmptyState
            icon="🏆"
            title="No points yet"
            description="Points appear here when you receive recognitions."
          />
        ) : (
          <View className="px-4">
            {[1, 2, 3].map((i) => <SkeletonCard key={i} />)}
          </View>
        )
      }
      refreshControl={
        <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor="#ED6813" />
      }
    />
  );
}
