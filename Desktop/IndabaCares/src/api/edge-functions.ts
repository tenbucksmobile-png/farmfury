/**
 * Typed wrappers for Supabase Edge Functions.
 *
 * Auth model:
 *   All Edge Functions use withEmployeeAuth middleware (server-side), which
 *   validates the x-session-token header against employee_active_sessions.
 *
 *   The token is injected two ways (belt-and-suspenders):
 *     1. The supabase client's custom fetch adapter (hotelAwareFetch) injects
 *        x-session-token into every HTTP request automatically.
 *     2. The invoke() wrapper below explicitly reads the token via getSessionToken()
 *        and passes it in the headers object, ensuring it is present even if the
 *        fetch adapter path is bypassed in future refactors.
 */

import { supabase, getSessionToken, notifySessionExpired } from '@/lib/supabase';
import type {
  SendRecognitionRequest,
  SendRecognitionResponse,
  SubmitMoodRequest,
  SubmitMoodResponse,
  RedeemRewardRequest,
  RedeemRewardResponse,
  CancelRedemptionRequest,
  CancelRedemptionResponse,
  BoostRecognitionRequest,
  BoostRecognitionResponse,
  EdgeFunctionError,
} from '@/types/api';

class EdgeFunctionCallError extends Error {
  status: number;
  constructor(message: string, status: number) {
    super(message);
    this.name = 'EdgeFunctionCallError';
    this.status = status;
  }
}

async function invoke<T>(
  functionName: string,
  body?: unknown,
  method: 'GET' | 'POST' = 'POST'
): Promise<T> {
  const sessionToken = getSessionToken();

  if (!sessionToken) {
    notifySessionExpired();
    throw new EdgeFunctionCallError('No active session', 401);
  }

  const { data, error } = await supabase.functions.invoke(functionName, {
    body:    body ?? undefined,
    method,
    headers: { 'x-session-token': sessionToken },
  });

  if (error) {
    const context = (error as any).context;
    let message = error.message || 'Edge function call failed';
    const status = (error as { status?: number }).status || 500;

    if (context && typeof context.json === 'function') {
      try {
        const parsed = await context.json();
        if (parsed?.error) message = parsed.error;
      } catch {}
    }

    if (status === 401) notifySessionExpired();
    throw new EdgeFunctionCallError(message, status);
  }

  if (data && typeof data === 'object' && 'error' in data) {
    const errData = data as EdgeFunctionError;
    throw new EdgeFunctionCallError(errData.error, 400);
  }

  return data as T;
}

// ─── Recognition ─────────────────────────────────────────────────────────────

export async function sendRecognition(
  body: SendRecognitionRequest
): Promise<SendRecognitionResponse> {
  return invoke<SendRecognitionResponse>('send-recognition', body);
}

export async function boostRecognition(
  body: BoostRecognitionRequest
): Promise<BoostRecognitionResponse> {
  return invoke<BoostRecognitionResponse>('boost-recognition', body);
}

// ─── Mood ─────────────────────────────────────────────────────────────────────

export async function submitMood(body: SubmitMoodRequest): Promise<SubmitMoodResponse> {
  return invoke<SubmitMoodResponse>('submit-mood', body);
}

// ─── Rewards ──────────────────────────────────────────────────────────────────

export async function redeemReward(
  body: RedeemRewardRequest
): Promise<RedeemRewardResponse> {
  return invoke<RedeemRewardResponse>('redeem-reward', body);
}

export async function cancelRedemption(
  body: CancelRedemptionRequest
): Promise<CancelRedemptionResponse> {
  return invoke<CancelRedemptionResponse>('cancel-redemption', body);
}
