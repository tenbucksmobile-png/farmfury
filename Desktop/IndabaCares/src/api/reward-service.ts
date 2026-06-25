/**
 * Reward Service
 *
 * All reward and redemption data access for the Indaba Cares app.
 *
 * Points model
 * ────────────
 *   Earned  +10 per recognition received  (DB trigger on recognitions)
 *   Spent   -N  on redemption             (redeem_reward RPC, atomic)
 *   Refund  +N  on rejection              (reject_redemption RPC, atomic)
 *
 * Redemption lifecycle
 * ────────────────────
 *   Employee: submitRedemption()
 *       → status = 'pending'  (points deducted immediately)
 *
 *   Admin: approveRedemption()
 *       → status = 'approved'
 *
 *   Admin: fulfillRedemption()
 *       → status = 'fulfilled'
 *
 *   Admin: rejectRedemption(reason?)
 *       → status = 'rejected'  (points refunded, stock restored)
 */

import { supabase } from '@/lib/supabase';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface Reward {
  id: string;
  title: string;
  description: string | null;
  points_required: number;
  image_url: string | null;
  hotel: string;
  stock: number;
  category: 'retail' | 'hotel';
  terms: string | null;
  created_at: string;
}

export type RedemptionStatus = 'pending' | 'approved' | 'rejected' | 'fulfilled';

export interface Redemption {
  id: string;
  points_used: number;
  status: RedemptionStatus;
  hotel: string;
  created_at: string;
  approved_at:  string | null;
  rejected_at:  string | null;
  fulfilled_at: string | null;
  rejection_reason: string | null;
  reward: {
    id: string;
    title: string;
    image_url: string | null;
    points_required: number;
  };
}

// ─── RPC result types ─────────────────────────────────────────────────────────

export interface RedeemResult {
  ok: boolean;
  redemption_id?: string;
  points_used?:   number;
  new_balance?:   number;
  error?:         string;
  balance?:       number;
  required?:      number;
}

export interface AdminActionResult {
  ok: boolean;
  status?:          RedemptionStatus;
  points_refunded?: number;
  error?:           string;
}

// ─── Catalogue queries ────────────────────────────────────────────────────────

/**
 * Fetch all rewards for a hotel, newest first.
 * Includes out-of-stock items so employees see the full catalogue.
 */
export async function getRewards(_hotel: string): Promise<Reward[]> {
  const { data, error } = await supabase
    .from('rewards')
    .select('id, title, description, points_required, image_url, hotel, stock, category, terms, created_at')
    .order('created_at', { ascending: false });

  if (error) throw new Error(error.message);
  return (data ?? []) as Reward[];
}

/** Fetch a single reward by ID. */
export async function getRewardDetail(id: string): Promise<Reward> {
  const { data, error } = await supabase
    .from('rewards')
    .select('id, title, description, points_required, image_url, hotel, stock, category, terms, created_at')
    .eq('id', id)
    .single();

  if (error) throw new Error(error.message);
  return data as Reward;
}

// ─── Points / Wallet ─────────────────────────────────────────────────────────

/** Get an employee's current Reward Wallet balance (spendable credits). */
export async function getEmployeePoints(employeeId: string): Promise<number> {
  const { data, error } = await supabase
    .from('employees')
    .select('reward_wallet_balance')
    .eq('id', employeeId)
    .single();

  if (error) throw new Error(error.message);
  return (data as { reward_wallet_balance: number }).reward_wallet_balance;
}

// ─── Wallet types ─────────────────────────────────────────────────────────────

export interface WalletStats {
  wallet_balance:       number;
  converted_points:     number;
  points_balance:       number;
  available_to_convert: number;
  max_credits:          number;
  redeemed_total:       number;
  converted_this_month: number;
}

export interface ConvertResult {
  ok:                  boolean;
  rp_converted?:       number;
  credits_earned?:     number;
  new_wallet_balance?: number;
  error?:              string;
  available?:          number;
  requested?:          number;
}

/** Fetch all wallet stats for the Reward tab in one RPC call. */
export async function getWalletStats(employeeId: string): Promise<WalletStats> {
  const { data, error } = await supabase.rpc('get_wallet_stats', {
    p_employee_id: employeeId,
  });

  if (error) throw new Error(error.message);
  return data as WalletStats;
}

/**
 * Convert Recognition Points to Reward Wallet credits at 5:1.
 * p_amount must be a positive multiple of 5.
 * Use walletStats.max_credits * 5 to get the maximum convertible amount.
 */
export async function convertToWallet(
  employeeId: string,
  amount:     number,
): Promise<ConvertResult> {
  const { data, error } = await supabase.rpc('convert_points_to_wallet', {
    p_employee_id: employeeId,
    p_amount:      amount,
  });

  if (error) throw new Error(error.message);
  return data as ConvertResult;
}

// ─── Redemption queries ───────────────────────────────────────────────────────

/** Fetch an employee's full redemption history, newest first. */
export async function getRedemptions(employeeId: string): Promise<Redemption[]> {
  const { data, error } = await supabase
    .from('redemptions')
    .select(
      `id, points_used, status, hotel, created_at,
       approved_at, rejected_at, fulfilled_at, rejection_reason,
       reward:rewards ( id, title, image_url, points_required )`,
    )
    .eq('employee_id', employeeId)
    .order('created_at', { ascending: false });

  if (error) throw new Error(error.message);
  return (data ?? []) as unknown as Redemption[];
}

// ─── Employee RPC functions ───────────────────────────────────────────────────

/**
 * Submit a redemption request.
 *
 * Calls redeem_reward() which atomically:
 *   1. Verifies stock > 0
 *   2. Verifies employee balance >= points_required
 *   3. Deducts points from employee
 *   4. Decrements reward stock
 *   5. Creates redemption record with status = 'pending'
 *
 * Returns RedeemResult — always check result.ok before proceeding.
 */
export async function submitRedemption(
  employeeId: string,
  rewardId:   string,
): Promise<RedeemResult> {
  const { data, error } = await supabase.rpc('redeem_reward', {
    p_employee_id: employeeId,
    p_reward_id:   rewardId,
  });

  if (error) throw new Error(error.message);
  return data as RedeemResult;
}

// ─── Admin RPC functions ──────────────────────────────────────────────────────
//
// These RPCs are SECURITY DEFINER — they run under the postgres role and
// bypass RLS.  Call them from the admin dashboard (service_role context)
// or from any authenticated session (the RPCs perform their own validation).

/**
 * Approve a pending redemption.
 * Moves status: pending → approved.
 * No points change — points were already deducted at submission time.
 */
export async function approveRedemption(redemptionId: string): Promise<AdminActionResult> {
  const { data, error } = await supabase.rpc('approve_redemption', {
    p_redemption_id: redemptionId,
  });

  if (error) throw new Error(error.message);
  return data as AdminActionResult;
}

/**
 * Reject a pending or approved redemption.
 * Moves status: pending|approved → rejected.
 * Refunds points_used to the employee and restores reward stock.
 *
 * @param reason  Optional message shown to the employee.
 */
export async function rejectRedemption(
  redemptionId: string,
  reason?:      string,
): Promise<AdminActionResult> {
  const { data, error } = await supabase.rpc('reject_redemption', {
    p_redemption_id: redemptionId,
    p_reason:        reason ?? null,
  });

  if (error) throw new Error(error.message);
  return data as AdminActionResult;
}

/**
 * Mark an approved redemption as fulfilled.
 * Moves status: approved → fulfilled.
 * Called once the physical or digital reward has been delivered.
 */
export async function fulfillRedemption(redemptionId: string): Promise<AdminActionResult> {
  const { data, error } = await supabase.rpc('fulfill_redemption', {
    p_redemption_id: redemptionId,
  });

  if (error) throw new Error(error.message);
  return data as AdminActionResult;
}

// ─── Legacy alias ─────────────────────────────────────────────────────────────
/** @deprecated Use submitRedemption() */
export const redeemReward = submitRedemption;

/** @deprecated Use rejectRedemption() */
export async function cancelRedemption(
  redemptionId: string,
  _employeeId:  string,
): Promise<{ ok: boolean; points_refunded?: number; error?: string }> {
  return rejectRedemption(redemptionId);
}
