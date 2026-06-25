/**
 * Reaction analytics — typed wrappers around the four analytics RPCs
 * defined in migration 043.
 *
 * All functions accept an optional DateWindow so callers can request
 * all-time, monthly, or custom-range data with the same interface.
 */

import { supabase } from '@/lib/supabase';

// ─── Shared types ─────────────────────────────────────────────────────────────

export interface DateWindow {
  start?: string; // ISO-8601 timestamptz
  end?:   string;
}

// ─── Return types ─────────────────────────────────────────────────────────────

export interface TopReactorRow {
  rank:             number;
  employee_id:      string;
  full_name:        string;
  employee_code:    string;
  total_reactions:  number;
  hearts_given:     number;
  smiles_given:     number;
  thumbs_given:     number;
}

export interface TopRecognisedRow {
  rank:                     number;
  employee_id:              string;
  full_name:                string;
  employee_code:            string;
  total_reactions_received: number;
  hearts_received:          number;
  smiles_received:          number;
  thumbs_received:          number;
  reaction_points_received: number;
  recognition_count:        number;
}

export interface MostReactedRecognitionRow {
  rank:             number;
  recognition_id:   string;
  badge:            string;
  message_preview:  string;
  recognition_at:   string;
  sender_id:        string;
  sender_name:      string;
  receiver_id:      string;
  receiver_name:    string;
  total_reactions:  number;
  heart_count:      number;
  smile_count:      number;
  thumbs_count:     number;
  engagement_score: number;
}

export interface ReactionHotelSummary {
  total_reactions:         number;
  total_reaction_points:   number;
  heart_count:             number;
  smile_count:             number;
  thumbs_count:            number;
  unique_reactors:         number;
  unique_receivers:        number;
  most_used_type:          string | null;
  avg_reactions_per_recog: number | null;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function windowParams(w?: DateWindow) {
  return {
    p_start: w?.start ?? null,
    p_end:   w?.end   ?? null,
  };
}

// ─── Functions ────────────────────────────────────────────────────────────────

/** Employees ranked by reactions given. */
export async function getTopReactors(
  hotel:  string,
  limit   = 20,
  window?: DateWindow,
) {
  const { data, error } = await supabase.rpc('get_top_reactors', {
    p_hotel: hotel,
    p_limit: limit,
    ...windowParams(window),
  });
  if (error) throw error;
  return (data ?? []) as TopReactorRow[];
}

/** Employees ranked by reactions received (weighted by point value). */
export async function getTopRecognisedEmployees(
  hotel:  string,
  limit   = 20,
  window?: DateWindow,
) {
  const { data, error } = await supabase.rpc('get_top_recognised_employees', {
    p_hotel: hotel,
    p_limit: limit,
    ...windowParams(window),
  });
  if (error) throw error;
  return (data ?? []) as TopRecognisedRow[];
}

/** Recognition posts ranked by reaction count. */
export async function getMostReactedRecognitions(
  hotel:  string,
  limit   = 20,
  window?: DateWindow,
) {
  const { data, error } = await supabase.rpc('get_most_reacted_recognitions', {
    p_hotel: hotel,
    p_limit: limit,
    ...windowParams(window),
  });
  if (error) throw error;
  return (data ?? []) as MostReactedRecognitionRow[];
}

/** Single-row hotel-level reaction KPIs. */
export async function getReactionHotelSummary(
  hotel:   string,
  window?: DateWindow,
): Promise<ReactionHotelSummary | null> {
  const { data, error } = await supabase.rpc('get_reaction_hotel_summary', {
    p_hotel: hotel,
    ...windowParams(window),
  });
  if (error) throw error;
  return (data?.[0] ?? null) as ReactionHotelSummary | null;
}

// ─── Convenience window builders ──────────────────────────────────────────────

/** Returns a DateWindow covering the current calendar month. */
export function thisMonthWindow(): DateWindow {
  const now   = new Date();
  const start = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
  const end   = new Date(now.getFullYear(), now.getMonth() + 1, 1).toISOString();
  return { start, end };
}

/** Returns a DateWindow covering the last N complete days. */
export function lastDaysWindow(n: number): DateWindow {
  const end   = new Date();
  const start = new Date(end.getTime() - n * 24 * 60 * 60 * 1000);
  return { start: start.toISOString(), end: end.toISOString() };
}
