/**
 * Edge Function request/response interfaces.
 * Must match the contracts in supabase/functions/
 */

import type { AppRole, MoodValue, Visibility } from './database';

// ─── auth-me ────────────────────────────────────────────────────────────────

export interface AuthMeResponse {
  user: {
    id: string;
    email: string;
    fullName: string;
    displayName: string | null;
    avatarUrl: string | null;
    role: AppRole;
    jobTitle: string | null;
    department: { id: string; name: string } | null;
    managerId: string | null;
    pointsBalance: number;
    starsBalance: number;
    givingBalance: number;
    loginStreak: number;
    badges: Array<{
      badge_id: string;
      earned_at: string;
      badges: { slug: string; name: string; icon: string };
    }>;
    createdAt: string;
  };
  company: {
    id: string;
    name: string;
    slug: string;
    logoUrl: string | null;
    primaryColor: string;
    features: Record<string, unknown>;
  };
  session: {
    unreadNotifications: number;
    moodSubmittedToday: boolean;
    streakResult: unknown;
  };
}

// ─── send-recognition ───────────────────────────────────────────────────────

export interface SendRecognitionRequest {
  recipientIds: string[];
  thumbsUpTypeId: string;
  message: string;
  visibility?: Visibility;
  imageUrl?: string;
  hashtags?: string[];
}

export interface SendRecognitionResponse {
  recognition: {
    id: string;
    message: string;
    visibility: Visibility;
    stars_per_recipient: number;
    image_url: string | null;
    hashtags: string[];
    created_at: string;
    sender: { id: string; full_name: string; avatar_url: string | null };
    thumbs_up_type: { id: string; name: string; icon: string; color: string };
    recipients: Array<{
      recipient: { id: string; full_name: string; avatar_url: string | null };
    }>;
  };
  message: string;
}

// ─── submit-mood ────────────────────────────────────────────────────────────

export interface SubmitMoodRequest {
  mood: MoodValue;
  note?: string;
}

export interface SubmitMoodResponse {
  moodEntryId: string;
  mood: MoodValue;
  pointsEarned: number;
  message: string;
}

// ─── redeem-reward ──────────────────────────────────────────────────────────

export interface RedeemRewardRequest {
  rewardId: string;
}

export interface RedeemRewardResponse {
  redemption: {
    id: string;
    star_cost: number;
    status: string;
    created_at: string;
    reward: { id: string; name: string; image_url: string | null; star_cost: number };
  };
  starsBalance: number;
  message: string;
}

// ─── cancel-redemption ─────────────────────────────────────────────────────

export interface CancelRedemptionRequest {
  redemptionId: string;
}

export interface CancelRedemptionResponse {
  redemptionId: string;
  status: string;
  starsRefunded: number;
  starsBalance: number;
  message: string;
}

// ─── boost-recognition ──────────────────────────────────────────────────────

export interface BoostRecognitionRequest {
  recognitionId: string;
}

export interface BoostRecognitionResponse {
  recognitionId: string;
  boostedBy: string;
  recipientsAwarded: number;
  bonusStarsEach: number;
  bonusPointsEach: number;
  message: string;
}

// ─── claim-employee-code ────────────────────────────────────────────────────

export interface ClaimEmployeeCodeRequest {
  employee_code: string;
}

export interface ClaimEmployeeCodeResponse {
  success: boolean;
  message: string;
  requiresRefresh: boolean;
  company: {
    id: string;
    name: string;
    slug: string;
    logoUrl: string | null;
    primaryColor: string;
  } | null;
}

// ─── Generic Edge Function error ────────────────────────────────────────────

export interface EdgeFunctionError {
  error: string;
}
