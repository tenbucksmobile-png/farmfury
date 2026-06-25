/**
 * EmployeeContext
 *
 * Provides the authenticated employee record to the entire React tree.
 *
 * Session token lifecycle:
 *   Login  → setEmployee() receives token from auth RPC → saveSession()
 *   Boot   → loadSession() restores token from AsyncStorage + reactivates header
 *   Verify → validateSessionWithDB() confirms employee is still active
 *   Logout → clearSession() revokes server-side row + clears header
 *
 * The x-session-token header is injected into every Supabase request by the
 * custom fetch adapter in src/lib/supabase.ts.  PostgreSQL RLS policies read
 * it via current_employee_hotel() to enforce hotel-level tenant isolation.
 */

import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
} from 'react';
import {
  saveSession,
  loadSession,
  clearSession,
  validateSessionWithDB,
  type EmployeeSession,
} from '@/lib/EmployeeSessionManager';
import { registerSessionExpiredHandler, supabase } from '@/lib/supabase';
import { markWelcomeSeen as apiMarkWelcomeSeen } from '@/api/initiative-service';

// ─── Types ────────────────────────────────────────────────────────────────────

export type AuthenticatedEmployee = EmployeeSession;

interface EmployeeContextValue {
  /** The authenticated employee, or null when logged out. */
  employee: AuthenticatedEmployee | null;
  /**
   * True once the AsyncStorage read has completed.
   * AuthProvider waits for this before routing.
   */
  isLoaded: boolean;
  /**
   * Login: persists session and activates the x-session-token header.
   * Receives the full session including the token returned by the auth RPC.
   */
  setEmployee: (identity: AuthenticatedEmployee) => Promise<void>;
  /**
   * Logout: revokes the server-side session, clears AsyncStorage, and
   * removes the header.
   */
  clearEmployee: () => Promise<void>;
  /**
   * Marks the welcome screen as seen in Supabase and updates local state.
   * Called when the employee dismisses the first-time onboarding video.
   */
  markWelcomeSeen: () => Promise<void>;
  /**
   * Whether the current employee has already seen the welcome screen.
   * null = not yet loaded from DB; true/false = known state.
   */
  hasSeenWelcome: boolean | null;
}

// ─── Context default ──────────────────────────────────────────────────────────

const EmployeeContext = createContext<EmployeeContextValue>({
  employee:        null,
  isLoaded:        false,
  hasSeenWelcome:  null,
  setEmployee:     async () => {},
  clearEmployee:   async () => {},
  markWelcomeSeen: async () => {},
});

// ─── Provider ─────────────────────────────────────────────────────────────────

export function EmployeeProvider({ children }: { children: ReactNode }) {
  const [employee, setEmployeeState]     = useState<AuthenticatedEmployee | null>(null);
  const [isLoaded, setIsLoaded]          = useState(false);
  const [hasSeenWelcome, setHasSeenWelcome] = useState<boolean | null>(null);

  // ── Boot sequence ─────────────────────────────────────────────────────────
  useEffect(() => {
    let cancelled = false;

    async function boot() {
      const session = await loadSession();

      if (cancelled) return;

      if (session) {
        setEmployeeState(session);
        // Fetch has_seen_welcome in parallel with session validation
        fetchHasSeenWelcome(session.employee_id);
      }

      setIsLoaded(true);

      if (!session) return;

      const valid = await validateSessionWithDB(session);

      if (cancelled) return;

      if (!valid) {
        setEmployeeState(null);
        setHasSeenWelcome(null);
        await clearSession(session.session_token);
      }
    }

    boot().catch(() => {
      if (!cancelled) setIsLoaded(true);
    });

    return () => { cancelled = true; };
  }, []);

  // ── fetchHasSeenWelcome — reads the DB flag ───────────────────────────────

  async function fetchHasSeenWelcome(employeeId: string) {
    try {
      const { data } = await supabase
        .from('employees')
        .select('has_seen_welcome')
        .eq('id', employeeId)
        .maybeSingle();
      setHasSeenWelcome(data?.has_seen_welcome ?? false);
    } catch {
      // Fail-open: treat as already seen to avoid blocking login on network error
      setHasSeenWelcome(true);
    }
  }

  // ── setEmployee — called after successful authentication ──────────────────
  //
  // The token comes directly from the auth RPC response.
  // No separate create_employee_session call needed.

  const setEmployee = useCallback(async (identity: AuthenticatedEmployee) => {
    await saveSession(identity);
    setEmployeeState(identity);
    fetchHasSeenWelcome(identity.employee_id);
  }, []);

  // ── clearEmployee — logout ────────────────────────────────────────────────

  const clearEmployee = useCallback(async () => {
    const token = employee?.session_token;
    setEmployeeState(null);
    setHasSeenWelcome(null);
    await clearSession(token);
  }, [employee]);

  // ── markWelcomeSeen — called when onboarding video is dismissed ───────────

  const markWelcomeSeen = useCallback(async () => {
    if (!employee) return;
    setHasSeenWelcome(true); // optimistic
    await apiMarkWelcomeSeen(employee.employee_id);
  }, [employee]);

  // ── Register auto-logout on session expiry ────────────────────────────────

  useEffect(() => {
    registerSessionExpiredHandler(() => {
      clearEmployee();
    });
  }, [clearEmployee]);

  return (
    <EmployeeContext.Provider value={{ employee, isLoaded, hasSeenWelcome, setEmployee, clearEmployee, markWelcomeSeen }}>
      {children}
    </EmployeeContext.Provider>
  );
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

export function useEmployee() {
  return useContext(EmployeeContext);
}
