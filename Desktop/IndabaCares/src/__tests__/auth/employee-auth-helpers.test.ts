/**
 * Unit tests — employee-auth-helpers.ts
 *
 * Covers:
 *   1.  First login success — returns AuthSuccess with correct fields
 *   2.  First login — calls first_time_authenticate with normalised params
 *   3.  First login — RPC ok:false (employee not found)
 *   4.  First login — RPC ok:false (already authenticated)
 *   5.  First login — Supabase transport error → generic message
 *   6.  First login — employee_code normalised to UPPERCASE
 *   7.  Returning login success — returns AuthSuccess
 *   8.  Returning login — calls authenticate_employee with normalised params
 *   9.  Returning login — wrong password
 *  10.  Returning login — inactive employee
 *  11.  Returning login — rate-limited / locked out
 *  12.  Returning login — Supabase transport error → generic message
 *  13.  Returning login — ok:false with no error field uses fallback message
 */

// jest.mock MUST appear before imports of the mocked module.
// Factories must not reference variables defined outside them (hoisting).

jest.mock('@/lib/supabase', () => ({
  supabase:        { rpc: jest.fn() },
  setSessionToken: jest.fn(),
}));

import { firstAuthentication, returningLogin } from '@/lib/employee-auth-helpers';
import { supabase } from '@/lib/supabase';

const mockRpc = supabase.rpc as jest.Mock;

// ─── Test data ─────────────────────────────────────────────────────────────────

const RPC_SUCCESS = {
  ok:        true,
  id:        'emp-uuid-001',
  full_name: 'Jane Smith',
  hotel:     'sandton-indaba',
  token:     'session-token-uuid-001',
};

beforeEach(() => {
  mockRpc.mockReset();
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

function rpcOk(overrides = {}) {
  mockRpc.mockResolvedValueOnce({ data: { ...RPC_SUCCESS, ...overrides }, error: null });
}

function rpcFail(errorMsg: string) {
  mockRpc.mockResolvedValueOnce({ data: { ok: false, error: errorMsg }, error: null });
}

function rpcTransportError() {
  mockRpc.mockResolvedValueOnce({ data: null, error: { message: 'Connection refused' } });
}

// ─── firstAuthentication ──────────────────────────────────────────────────────

describe('firstAuthentication', () => {
  it('1. returns AuthSuccess on first login', async () => {
    rpcOk();

    const result = await firstAuthentication('Jane Smith', 'js001', 'sandton-indaba', 'Secret123!');

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.employee_id).toBe('emp-uuid-001');
    expect(result.full_name).toBe('Jane Smith');
    expect(result.hotel).toBe('sandton-indaba');
    expect(result.token).toBe('session-token-uuid-001');
  });

  it('2. calls first_time_authenticate RPC with normalised params', async () => {
    rpcOk();

    await firstAuthentication('  Jane Smith  ', ' js001 ', '  sandton-indaba  ', 'pass');

    expect(mockRpc).toHaveBeenCalledWith('first_time_authenticate', {
      p_employee_code: 'JS001',
      p_hotel:         'sandton-indaba',
      p_full_name:     'Jane Smith',
      p_new_password:  'pass',
    });
  });

  it('3. returns AuthFailure when employee not found', async () => {
    rpcFail('Employee not found');

    const result = await firstAuthentication('Jane', 'UNKNOWN', 'sandton-indaba', 'pass');

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toBe('Employee not found');
  });

  it('4. returns AuthFailure when employee has already authenticated', async () => {
    rpcFail('Employee has already authenticated');

    const result = await firstAuthentication('Jane', 'JS001', 'sandton-indaba', 'pass');

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/already authenticated/i);
  });

  it('5. returns generic AuthFailure on Supabase transport error', async () => {
    rpcTransportError();

    const result = await firstAuthentication('Jane', 'JS001', 'sandton-indaba', 'pass');

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toBe('Authentication failed. Please try again.');
  });

  it('6. normalises employee_code to UPPERCASE in returned value', async () => {
    rpcOk();

    const result = await firstAuthentication('Jane', 'js001', 'hotel', 'pass');

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.employee_code).toBe('JS001');
  });
});

// ─── returningLogin ───────────────────────────────────────────────────────────

describe('returningLogin', () => {
  it('7. returns AuthSuccess on correct credentials', async () => {
    rpcOk();

    const result = await returningLogin('JS001', 'sandton-indaba', 'Secret123!');

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.employee_id).toBe('emp-uuid-001');
    expect(result.token).toBe('session-token-uuid-001');
  });

  it('8. calls authenticate_employee RPC with normalised params', async () => {
    rpcOk();

    await returningLogin(' js001 ', '  sandton-indaba  ', 'Secret123!');

    expect(mockRpc).toHaveBeenCalledWith('authenticate_employee', {
      p_employee_code: 'JS001',
      p_hotel:         'sandton-indaba',
      p_password:      'Secret123!',
    });
  });

  it('9. returns AuthFailure on wrong password', async () => {
    rpcFail('Invalid employee code or password');

    const result = await returningLogin('JS001', 'sandton-indaba', 'WrongPass');

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/invalid employee code or password/i);
  });

  it('10. returns AuthFailure when employee is inactive', async () => {
    rpcFail('Account is inactive');

    const result = await returningLogin('JS001', 'sandton-indaba', 'pass');

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/inactive/i);
  });

  it('11. returns AuthFailure when rate-limited', async () => {
    rpcFail('Too many failed attempts. Try again later.');

    const result = await returningLogin('JS001', 'sandton-indaba', 'wrong');

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/too many/i);
  });

  it('12. returns generic AuthFailure on Supabase transport error', async () => {
    rpcTransportError();

    const result = await returningLogin('JS001', 'sandton-indaba', 'pass');

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toBe('Login failed. Please try again.');
  });

  it('13. uses fallback error message when ok:false has no error field', async () => {
    mockRpc.mockResolvedValueOnce({ data: { ok: false }, error: null });

    const result = await returningLogin('JS001', 'sandton-indaba', 'pass');

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toBe('Invalid employee code or password.');
  });
});
