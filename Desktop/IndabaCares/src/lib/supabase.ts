/**
 * Supabase client — hotel-session-aware + network-secured.
 *
 * Request pipeline (every outbound call):
 *   secureFetch()        → domain allowlist + HTTPS check + timeout + redirect guard
 *   hotelAwareFetch()    → injects x-session-token header
 *   Supabase PostgREST   → RLS via current_employee_hotel()
 *
 * Call setSessionToken(token) after login and setSessionToken(null) on logout.
 */

import { createClient } from '@supabase/supabase-js';
import { secureFetch }  from '@/lib/secureApi';
import type { Database } from '@/types/database';

const supabaseUrl     = process.env.EXPO_PUBLIC_SUPABASE_URL!;
const supabaseAnonKey = process.env.EXPO_PUBLIC_SUPABASE_ANON_KEY!;

// ─── Session token store ──────────────────────────────────────────────────────

const _sessionHeaders: Record<string, string> = {};

export function setSessionToken(token: string | null): void {
  if (token) {
    _sessionHeaders['x-session-token'] = token;
  } else {
    delete _sessionHeaders['x-session-token'];
  }
}

export function getSessionToken(): string | null {
  return _sessionHeaders['x-session-token'] ?? null;
}

// ─── Session-expiry callback ──────────────────────────────────────────────────

let _onSessionExpired: (() => void) | null = null;

export function registerSessionExpiredHandler(fn: () => void): void {
  _onSessionExpired = fn;
}

export function notifySessionExpired(): void {
  _onSessionExpired?.();
}

// ─── Combined fetch adapter ───────────────────────────────────────────────────
//
// secureFetch validates domain, HTTPS, timeout, and redirect integrity.
// The wrapper on top injects the x-session-token header.

function hotelAwareFetch(
  input: RequestInfo | URL,
  init:  RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers ?? {});
  const token   = _sessionHeaders['x-session-token'];
  if (token) headers.set('x-session-token', token);

  return secureFetch(input, { ...init, headers });
}

// ─── Supabase client ──────────────────────────────────────────────────────────

export const supabase = createClient<Database>(supabaseUrl, supabaseAnonKey, {
  auth: {
    autoRefreshToken:   false,
    persistSession:     false,
    detectSessionInUrl: false,
  },
  global: {
    fetch: hotelAwareFetch,
  },
});
