/**
 * Unit tests — EmployeeSessionManager.ts
 *
 * Covers:
 *   1.  saveSession — persists to AsyncStorage + activates token header
 *   2.  saveSession — token header receives the exact session_token value
 *   3.  loadSession — restores session + re-activates token
 *   4.  loadSession — returns null when storage is empty
 *   5.  loadSession — returns null and removes corrupt data
 *   6.  clearSession — revokes server session + wipes AsyncStorage + clears header
 *   7.  clearSession — still clears local state when revoke RPC fails
 *   8.  clearSession — skips RPC call when no token provided
 *   9.  validateSessionWithDB — returns true for active employee
 *  10.  validateSessionWithDB — returns false for inactive employee (session expiry / deactivation)
 *  11.  validateSessionWithDB — returns true on network error (fail-open / offline)
 *  12.  validateSessionWithDB — returns false when employee row not found
 *  13.  validateSessionWithDB — returns true on unexpected exception (fail-open)
 *  14.  Session expiry flow — validate returns false → clearSession wipes state
 *  15.  Boot flow — loadSession + validateSessionWithDB for active employee
 */

jest.mock('@react-native-async-storage/async-storage', () => ({
  __esModule: true,
  default: {
    setItem:    jest.fn(),
    getItem:    jest.fn(),
    removeItem: jest.fn(),
  },
}));

jest.mock('@/lib/supabase', () => {
  const single = jest.fn();
  const eq     = jest.fn().mockReturnThis();
  const select = jest.fn().mockReturnValue({ eq, single });
  eq.mockReturnValue({ eq, single });

  return {
    supabase: {
      rpc:  jest.fn(),
      from: jest.fn().mockReturnValue({ select }),
    },
    setSessionToken: jest.fn(),
  };
});

import AsyncStorage from '@react-native-async-storage/async-storage';
import { supabase, setSessionToken } from '@/lib/supabase';
import {
  saveSession,
  loadSession,
  clearSession,
  validateSessionWithDB,
  type EmployeeSession,
} from '@/lib/EmployeeSessionManager';

// Typed handles into the mocks
const mockSetItem    = AsyncStorage.setItem    as jest.Mock;
const mockGetItem    = AsyncStorage.getItem    as jest.Mock;
const mockRemoveItem = AsyncStorage.removeItem as jest.Mock;
const mockRpc        = supabase.rpc            as jest.Mock;
const mockSetSessionToken = setSessionToken    as jest.Mock;

// Drill into the .from().select().eq().single() chain
function getSingleMock(): jest.Mock {
  const fromResult   = (supabase.from as jest.Mock).mock.results[0]?.value;
  const selectResult = fromResult?.select?.mock?.results[0]?.value;
  return selectResult?.single as jest.Mock;
}

// ─── Test data ─────────────────────────────────────────────────────────────────

const SESSION: EmployeeSession = {
  employee_id:   'emp-uuid-001',
  full_name:     'Jane Smith',
  employee_code: 'JS001',
  hotel:         'sandton-indaba',
  department:    null,
  position:      null,
  session_token: 'tok-uuid-001',
};

const SESSION_KEY = '@indabacares/employee';

beforeEach(() => {
  jest.clearAllMocks();

  // AsyncStorage methods must return Promises (clearAllMocks resets to undefined)
  mockSetItem.mockResolvedValue(undefined);
  mockGetItem.mockResolvedValue(null);
  mockRemoveItem.mockResolvedValue(undefined);

  // Re-apply Supabase chain behaviour after clearAllMocks
  const single = jest.fn();
  const eq     = jest.fn();
  const select = jest.fn();
  eq.mockReturnThis();
  select.mockReturnValue({ eq, single });
  eq.mockReturnValue({ eq, single });
  (supabase.from as jest.Mock).mockReturnValue({ select });
});

// ─── saveSession ──────────────────────────────────────────────────────────────

describe('saveSession', () => {
  it('1. writes session JSON to AsyncStorage', async () => {
    await saveSession(SESSION);

    expect(mockSetItem).toHaveBeenCalledWith(SESSION_KEY, JSON.stringify(SESSION));
  });

  it('2. activates the correct session token header', async () => {
    await saveSession(SESSION);

    expect(mockSetSessionToken).toHaveBeenCalledWith('tok-uuid-001');
  });
});

// ─── loadSession ──────────────────────────────────────────────────────────────

describe('loadSession', () => {
  it('3. returns parsed session and re-activates token', async () => {
    mockGetItem.mockResolvedValueOnce(JSON.stringify(SESSION));

    const result = await loadSession();

    expect(result).toEqual(SESSION);
    expect(mockSetSessionToken).toHaveBeenCalledWith('tok-uuid-001');
  });

  it('4. returns null when AsyncStorage has no session', async () => {
    mockGetItem.mockResolvedValueOnce(null);

    const result = await loadSession();

    expect(result).toBeNull();
    expect(mockSetSessionToken).not.toHaveBeenCalled();
  });

  it('5. returns null and removes corrupt stored data', async () => {
    mockGetItem.mockResolvedValueOnce('{ not valid json >>>');

    const result = await loadSession();

    expect(result).toBeNull();
    expect(mockRemoveItem).toHaveBeenCalledWith(SESSION_KEY);
  });
});

// ─── clearSession ─────────────────────────────────────────────────────────────

describe('clearSession', () => {
  it('6. revokes server session, removes from AsyncStorage, clears token', async () => {
    mockRpc.mockResolvedValueOnce({ data: null, error: null });

    await clearSession(SESSION.session_token);

    expect(mockRpc).toHaveBeenCalledWith('revoke_employee_session', {
      p_token: 'tok-uuid-001',
    });
    expect(mockRemoveItem).toHaveBeenCalledWith(SESSION_KEY);
    expect(mockSetSessionToken).toHaveBeenCalledWith(null);
  });

  it('7. still clears local state when revoke RPC throws', async () => {
    mockRpc.mockRejectedValueOnce(new Error('Network error'));

    await clearSession(SESSION.session_token);

    expect(mockRemoveItem).toHaveBeenCalledWith(SESSION_KEY);
    expect(mockSetSessionToken).toHaveBeenCalledWith(null);
  });

  it('8. skips RPC call when no token is provided', async () => {
    await clearSession();

    expect(mockRpc).not.toHaveBeenCalled();
    expect(mockRemoveItem).toHaveBeenCalledWith(SESSION_KEY);
    expect(mockSetSessionToken).toHaveBeenCalledWith(null);
  });
});

// ─── validateSessionWithDB ────────────────────────────────────────────────────

describe('validateSessionWithDB', () => {
  it('9. returns true for an active employee', async () => {
    // Build a fresh chain so .single() resolves correctly
    const single = jest.fn().mockResolvedValueOnce({
      data: { id: SESSION.employee_id, status: 'active' }, error: null,
    });
    const eq = jest.fn().mockReturnValue({ eq: jest.fn().mockReturnThis(), single });
    (supabase.from as jest.Mock).mockReturnValueOnce({
      select: jest.fn().mockReturnValue({ eq }),
    });

    expect(await validateSessionWithDB(SESSION)).toBe(true);
  });

  it('10. returns false for an inactive employee (session expiry / deactivation)', async () => {
    const single = jest.fn().mockResolvedValueOnce({
      data: { id: SESSION.employee_id, status: 'inactive' }, error: null,
    });
    const eq = jest.fn().mockReturnValue({ single });
    (supabase.from as jest.Mock).mockReturnValueOnce({
      select: jest.fn().mockReturnValue({ eq }),
    });

    expect(await validateSessionWithDB(SESSION)).toBe(false);
  });

  it('11. returns true (fail-open) on network/fetch error', async () => {
    const single = jest.fn().mockResolvedValueOnce({
      data: null, error: { message: 'Failed to fetch' },
    });
    const eq = jest.fn().mockReturnValue({ single });
    (supabase.from as jest.Mock).mockReturnValueOnce({
      select: jest.fn().mockReturnValue({ eq }),
    });

    expect(await validateSessionWithDB(SESSION)).toBe(true);
  });

  it('12. returns false when employee row is not found', async () => {
    const single = jest.fn().mockResolvedValueOnce({
      data: null, error: { message: 'PGRST116: The result contains 0 rows' },
    });
    const eq = jest.fn().mockReturnValue({ single });
    (supabase.from as jest.Mock).mockReturnValueOnce({
      select: jest.fn().mockReturnValue({ eq }),
    });

    expect(await validateSessionWithDB(SESSION)).toBe(false);
  });

  it('13. returns true (fail-open) on unexpected exception', async () => {
    (supabase.from as jest.Mock).mockImplementationOnce(() => {
      throw new Error('Unexpected crash');
    });

    expect(await validateSessionWithDB(SESSION)).toBe(true);
  });
});

// ─── End-to-end flows ─────────────────────────────────────────────────────────

describe('Session lifecycle flows', () => {
  it('14. session expiry flow: validate → false → clearSession wipes state', async () => {
    // validate returns false (expired/inactive)
    const single = jest.fn().mockResolvedValueOnce({
      data: { id: SESSION.employee_id, status: 'inactive' }, error: null,
    });
    const eq = jest.fn().mockReturnValue({ single });
    (supabase.from as jest.Mock).mockReturnValueOnce({
      select: jest.fn().mockReturnValue({ eq }),
    });

    const stillValid = await validateSessionWithDB(SESSION);
    expect(stillValid).toBe(false);

    // EmployeeProvider reacts by calling clearSession
    mockRpc.mockResolvedValueOnce({ data: null, error: null });
    await clearSession(SESSION.session_token);

    expect(mockSetSessionToken).toHaveBeenCalledWith(null);
    expect(mockRemoveItem).toHaveBeenCalledWith(SESSION_KEY);
  });

  it('15. boot flow: loadSession restores and validate confirms active session', async () => {
    // Step 1: restore from AsyncStorage
    mockGetItem.mockResolvedValueOnce(JSON.stringify(SESSION));
    const loaded = await loadSession();
    expect(loaded).toEqual(SESSION);
    expect(mockSetSessionToken).toHaveBeenCalledWith('tok-uuid-001');

    // Step 2: background DB validation
    const single = jest.fn().mockResolvedValueOnce({
      data: { id: SESSION.employee_id, status: 'active' }, error: null,
    });
    const eq = jest.fn().mockReturnValue({ single });
    (supabase.from as jest.Mock).mockReturnValueOnce({
      select: jest.fn().mockReturnValue({ eq }),
    });

    expect(await validateSessionWithDB(SESSION)).toBe(true);
  });
});
