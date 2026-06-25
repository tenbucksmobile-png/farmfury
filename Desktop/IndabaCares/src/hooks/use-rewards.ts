import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getRewards,
  getRewardDetail,
  getEmployeePoints,
  getWalletStats,
  convertToWallet,
  submitRedemption,
  type RedeemResult,
  type WalletStats,
} from '@/api/reward-service';
import { useEmployee } from '@/providers/EmployeeContext';

// ─── Local query key factory ──────────────────────────────────────────────────

const RK = {
  rewards:     (hotel: string)      => ['rewards', hotel]          as const,
  rewardDetail:(id: string)         => ['reward', id]              as const,
  points:      (employeeId: string) => ['points', employeeId]      as const,
  walletStats: (employeeId: string) => ['wallet-stats', employeeId] as const,
};

// ─── Hooks ────────────────────────────────────────────────────────────────────

export function useRewards() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: RK.rewards(employee?.hotel ?? ''),
    queryFn:  () => getRewards(employee!.hotel),
    enabled:  !!employee,
    staleTime: 2 * 60 * 1000,
  });
}

export function useRewardDetail(id: string) {
  return useQuery({
    queryKey: RK.rewardDetail(id),
    queryFn:  () => getRewardDetail(id),
    staleTime: 2 * 60 * 1000,
  });
}

/** Current points balance for the logged-in employee. */
export function useEmployeePoints() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: RK.points(employee?.employee_id ?? ''),
    queryFn:  () => getEmployeePoints(employee!.employee_id),
    enabled:  !!employee,
    staleTime: 30 * 1000,
  });
}

/**
 * Mutation to redeem a reward.
 * Returns the full RedeemResult — callers must check result.ok.
 */
export function useRedeemReward() {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: (rewardId: string): Promise<RedeemResult> => {
      if (!employee) throw new Error('Not authenticated');
      return submitRedemption(employee.employee_id, rewardId);
    },
    onSuccess: (result) => {
      if (!result.ok || !employee) return;
      // Refresh balance, wallet stats, catalogue (stock changed) and redemption history
      queryClient.invalidateQueries({ queryKey: RK.points(employee.employee_id) });
      queryClient.invalidateQueries({ queryKey: RK.walletStats(employee.employee_id) });
      queryClient.invalidateQueries({ queryKey: RK.rewards(employee.hotel) });
      queryClient.invalidateQueries({ queryKey: ['redemptions'] });
    },
  });
}

/** All wallet stats for the Reward tab (single RPC). */
export function useWalletStats() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: RK.walletStats(employee?.employee_id ?? ''),
    queryFn:  () => getWalletStats(employee!.employee_id),
    enabled:  !!employee,
    staleTime: 30 * 1000,
  });
}

/** Mutation to convert Recognition Points → Reward Wallet credits (5:1). */
export function useConvertPoints() {
  const queryClient = useQueryClient();
  const { employee } = useEmployee();

  return useMutation({
    mutationFn: async (amount: number) => {
      if (!employee) throw new Error('Not authenticated');
      const result = await convertToWallet(employee.employee_id, amount);
      if (!result.ok) throw new Error(result.error ?? 'Conversion failed.');
      return result;
    },
    onSuccess: () => {
      if (!employee) return;
      queryClient.invalidateQueries({ queryKey: RK.points(employee.employee_id) });
      queryClient.invalidateQueries({ queryKey: RK.walletStats(employee.employee_id) });
    },
  });
}

// Re-export query key helper so screens can invalidate
export { RK as REWARD_QUERY_KEYS };
