/**
 * EmployeeSessionManager
 *
 * Manages two layers of session state:
 *
 *   1. expo-secure-store — persists the session JSON in the platform keychain
 *      (iOS Keychain / Android Keystore). Encrypted at rest.
 *
 *   2. x-session-token header — injected into every Supabase request so that
 *      current_employee_hotel() (server-side RLS) can identify the employee.
 *
 * Migration: existing sessions stored in AsyncStorage are silently migrated to
 * SecureStore on first load and then removed from AsyncStorage.
 *
 * Boot sequence (handled by EmployeeProvider):
 *   a. loadSession()               → restore employee + token from SecureStore
 *   b. setSessionToken(token)      → inject header into Supabase client
 *   c. validateSessionWithDB()     → confirm employee still active in DB
 *   d. If invalid → clearSession() → wipe both SecureStore and header
 */

import * as SecureStore from 'expo-secure-store';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { supabase, setSessionToken } from '@/lib/supabase';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface EmployeeSession {
  employee_id:   string;
  full_name:     string;
  employee_code: string;
  hotel:         string;
  department:    string | null;
  position:      string | null;
  session_token: string;   // UUID returned by the auth RPC
}

// ─── Keys ─────────────────────────────────────────────────────────────────────

const SECURE_KEY  = 'indabacares.employee.session';
const LEGACY_KEY  = '@indabacares/employee'; // AsyncStorage key from previous versions

// ─── saveSession ──────────────────────────────────────────────────────────────
//
// Persists the session to SecureStore and activates the token header.
// Called immediately after a successful auth RPC response.

export async function saveSession(session: EmployeeSession): Promise<void> {
  await SecureStore.setItemAsync(SECURE_KEY, JSON.stringify(session));
  setSessionToken(session.session_token);
}

// ─── loadSession ─────────────────────────────────────────────────────────────
//
// Restores a persisted session.
// Migration path: checks SecureStore first, then falls back to AsyncStorage.
// If found in AsyncStorage, migrates to SecureStore and deletes the legacy entry.
// Returns null if no session is stored or the stored value is corrupt.

export async function loadSession(): Promise<EmployeeSession | null> {
  // ── 1. Try SecureStore (current) ─────────────────────────────────────────
  try {
    const raw = await SecureStore.getItemAsync(SECURE_KEY);
    if (raw) {
      const session = JSON.parse(raw) as EmployeeSession;
      if (session.session_token) {
        setSessionToken(session.session_token);
      }
      return session;
    }
  } catch {
    // Corrupt SecureStore entry — remove it and fall through to legacy check
    await SecureStore.deleteItemAsync(SECURE_KEY).catch(() => null);
  }

  // ── 2. Legacy migration: check AsyncStorage ──────────────────────────────
  try {
    const legacyRaw = await AsyncStorage.getItem(LEGACY_KEY);
    if (!legacyRaw) return null;

    const session = JSON.parse(legacyRaw) as EmployeeSession;

    // Migrate to SecureStore
    await SecureStore.setItemAsync(SECURE_KEY, legacyRaw);
    // Remove from AsyncStorage — no longer needed
    await AsyncStorage.removeItem(LEGACY_KEY).catch(() => null);

    if (session.session_token) {
      setSessionToken(session.session_token);
    }

    return session;
  } catch {
    // Corrupt legacy entry — clean up both stores
    await AsyncStorage.removeItem(LEGACY_KEY).catch(() => null);
    return null;
  }
}

// ─── clearSession ─────────────────────────────────────────────────────────────
//
// Full logout:
//   1. Revokes the server-side session row (token immediately invalidated).
//   2. Removes the session from SecureStore (and legacy AsyncStorage if present).
//   3. Clears the token from the Supabase client.

export async function clearSession(token?: string): Promise<void> {
  if (token) {
    try {
      await supabase.rpc('revoke_employee_session', { p_token: token });
    } catch {
      // best-effort revocation — proceed with local logout regardless
    }
  }

  await SecureStore.deleteItemAsync(SECURE_KEY).catch(() => null);
  // Also clear legacy storage in case it was never migrated
  await AsyncStorage.removeItem(LEGACY_KEY).catch(() => null);
  setSessionToken(null);
}

// ─── validateSessionWithDB ────────────────────────────────────────────────────
//
// Background validation on every app launch.
// Confirms the employee is still active AND the session token is still valid.
//
// Fail-open on network error (offline-friendly): returns true so the cached
// session is kept when there is no connectivity.

export async function validateSessionWithDB(
  session: EmployeeSession,
): Promise<boolean> {
  try {
    const { data, error } = await supabase.rpc('validate_session', {
      p_session_token: session.session_token,
    });

    if (error?.message?.includes('fetch') || error?.message?.includes('network')) {
      return true; // fail-open when offline
    }

    if (error || !data) return false;

    return (data as { ok: boolean }).ok === true;
  } catch {
    return true; // fail-open on unexpected errors
  }
}
