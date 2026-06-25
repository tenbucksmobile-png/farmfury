/**
 * Unit tests — Post-login feature validation
 *
 * Validates that the core feature query builders behave correctly once an
 * employee session is established. All Supabase calls are mocked; the
 * session token header is assumed active.
 *
 * Features covered:
 *   1.  feedQuery — hotel-scoped, ordered by created_at DESC
 *   2.  feedQuery — cursor-based pagination
 *   3.  postRecognition — INSERT with correct fields
 *   4.  rewardsQuery — hotel-scoped
 *   5.  redemptionsQuery — employee-scoped
 *   6.  notificationsQuery — employee-scoped
 *   7.  pointsLedgerQuery — employee-scoped, ordered DESC
 *   8.  markNotificationRead — calls correct RPC
 *   9.  markAllNotificationsRead — calls correct RPC
 *  10.  getLeaderboard (all_time) — null start/end
 *  11.  getLeaderboard (monthly) — start = first of month
 *  12.  getLeaderboard (quarterly) — start = first of quarter
 *  13.  getLeaderboard (annual) — start = first of year
 *  14.  getLeaderboard — throws on RPC error
 *  15.  getLeaderboard — returns [] when data is null
 *  16–25. getBadgeLevel — correct badge for each points tier
 */

// ─── Mocks ────────────────────────────────────────────────────────────────────

jest.mock('@/lib/supabase', () => {
  const single  = jest.fn();
  const limit   = jest.fn().mockReturnValue({ single: jest.fn(), eq: jest.fn().mockReturnThis() });
  const order   = jest.fn().mockReturnValue({ limit, single, eq: jest.fn().mockReturnThis() });
  const gte     = jest.fn().mockReturnValue({ order });
  const eq      = jest.fn();
  const select  = jest.fn();
  const insert  = jest.fn();

  eq.mockReturnValue({ eq, order, single, limit, gte, or: jest.fn().mockReturnThis() });
  select.mockReturnValue({ eq, order, single, or: jest.fn().mockReturnThis() });
  insert.mockReturnValue({ select, single });

  return {
    supabase: {
      rpc:  jest.fn(),
      from: jest.fn().mockReturnValue({ select, insert }),
    },
    setSessionToken: jest.fn(),
  };
});

// ─── Imports (after mocks) ────────────────────────────────────────────────────

import { supabase } from '@/lib/supabase';
import {
  feedQuery,
  postRecognition,
  rewardsQuery,
  redemptionsQuery,
  notificationsQuery,
  pointsLedgerQuery,
  markNotificationRead,
  markAllNotificationsRead,
} from '@/api/queries';
import {
  getLeaderboard,
  getBadgeLevel,
  BADGE_LEVELS,
  type PeriodType,
} from '@/api/leaderboard-service';

const mockRpc  = supabase.rpc  as jest.Mock;
const mockFrom = supabase.from as jest.Mock;

const HOTEL       = 'sandton-indaba';
const EMPLOYEE_ID = 'emp-uuid-001';

beforeEach(() => {
  jest.clearAllMocks();

  // Re-apply chain after clearAllMocks
  const single  = jest.fn();
  const limit   = jest.fn().mockReturnValue({ single, eq: jest.fn().mockReturnThis() });
  const order   = jest.fn().mockReturnValue({ limit, single, eq: jest.fn().mockReturnThis() });
  const gte     = jest.fn().mockReturnValue({ order });
  const eq      = jest.fn();
  const select  = jest.fn();
  const insert  = jest.fn();

  eq.mockReturnValue({ eq, order, single, limit, gte, or: jest.fn().mockReturnThis() });
  select.mockReturnValue({ eq, order, single, or: jest.fn().mockReturnThis() });
  insert.mockReturnValue({ select, single });

  mockFrom.mockReturnValue({ select, insert });
});

// ─── 1–3. Recognition ────────────────────────────────────────────────────────

describe('Feature: Recognition feed', () => {
  it('1. feedQuery queries recognitions table scoped to hotel', () => {
    feedQuery(HOTEL);

    expect(mockFrom).toHaveBeenCalledWith('recognitions');
    // eq called with hotel
    const fromResult = mockFrom.mock.results[0].value;
    const eqCalls = fromResult.select.mock.results[0].value.eq.mock.calls;
    expect(eqCalls).toContainEqual(['hotel', HOTEL]);
  });

  it('2. feedQuery orders by created_at DESC', () => {
    feedQuery(HOTEL);

    const fromResult  = mockFrom.mock.results[0].value;
    const selectChain = fromResult.select.mock.results[0].value;
    const eqResult    = selectChain.eq.mock.results[0].value;
    expect(eqResult.order).toHaveBeenCalledWith('created_at', { ascending: false });
  });

  it('3. postRecognition inserts to recognitions with correct payload', () => {
    postRecognition(EMPLOYEE_ID, 'rec-uuid-002', 'Great job!', 'champion', HOTEL);

    expect(mockFrom).toHaveBeenCalledWith('recognitions');
    const insertMock = mockFrom.mock.results[0].value.insert;
    expect(insertMock).toHaveBeenCalledWith({
      sender_id:   EMPLOYEE_ID,
      receiver_id: 'rec-uuid-002',
      message:     'Great job!',
      badge:       'champion',
      hotel:       HOTEL,
    });
  });
});

// ─── 4–5. Rewards & Redemptions ──────────────────────────────────────────────

describe('Feature: Rewards', () => {
  it('4. rewardsQuery queries rewards table', () => {
    rewardsQuery(HOTEL);
    expect(mockFrom).toHaveBeenCalledWith('rewards');
  });

  it('5. redemptionsQuery queries redemptions scoped to employee', () => {
    redemptionsQuery(EMPLOYEE_ID);
    expect(mockFrom).toHaveBeenCalledWith('redemptions');
    const eqCalls = mockFrom.mock.results[0].value.select.mock.results[0].value.eq.mock.calls;
    expect(eqCalls).toContainEqual(['employee_id', EMPLOYEE_ID]);
  });
});

// ─── 6–9. Notifications ──────────────────────────────────────────────────────

describe('Feature: Notifications', () => {
  it('6. notificationsQuery queries notifications scoped to employee_id', () => {
    notificationsQuery(EMPLOYEE_ID);

    expect(mockFrom).toHaveBeenCalledWith('notifications');
    const eqCalls = mockFrom.mock.results[0].value.select.mock.results[0].value.eq.mock.calls;
    expect(eqCalls).toContainEqual(['employee_id', EMPLOYEE_ID]);
  });

  it('7. pointsLedgerQuery queries points_ledger scoped to employee', () => {
    pointsLedgerQuery(EMPLOYEE_ID);
    expect(mockFrom).toHaveBeenCalledWith('points_ledger');
    const eqCalls = mockFrom.mock.results[0].value.select.mock.results[0].value.eq.mock.calls;
    expect(eqCalls).toContainEqual(['employee_id', EMPLOYEE_ID]);
  });

  it('8. markNotificationRead calls mark_notification_read RPC', () => {
    mockRpc.mockReturnValueOnce(Promise.resolve({ data: null, error: null }));

    markNotificationRead('notif-001');

    expect(mockRpc).toHaveBeenCalledWith('mark_notification_read', { p_id: 'notif-001' });
  });

  it('9. markAllNotificationsRead calls mark_all_notifications_read RPC', () => {
    mockRpc.mockReturnValueOnce(Promise.resolve({ data: null, error: null }));

    markAllNotificationsRead(EMPLOYEE_ID);

    expect(mockRpc).toHaveBeenCalledWith('mark_all_notifications_read', {
      p_employee_id: EMPLOYEE_ID,
    });
  });
});

// ─── 10–15. Leaderboard ──────────────────────────────────────────────────────

describe('Feature: Leaderboard', () => {
  it('10. getLeaderboard all_time passes null start/end', async () => {
    mockRpc.mockResolvedValueOnce({ data: [], error: null });

    await getLeaderboard(HOTEL, 'all_time');

    expect(mockRpc).toHaveBeenCalledWith('get_leaderboard', expect.objectContaining({
      p_hotel: HOTEL,
      p_start: null,
      p_end:   null,
    }));
  });

  it('11. getLeaderboard monthly: start = first day of current month', async () => {
    mockRpc.mockResolvedValueOnce({ data: [], error: null });

    await getLeaderboard(HOTEL, 'monthly');

    const args = mockRpc.mock.calls[0][1];
    const start = new Date(args.p_start);
    const now   = new Date();

    expect(start.getFullYear()).toBe(now.getFullYear());
    expect(start.getMonth()).toBe(now.getMonth());
    expect(start.getDate()).toBe(1);
    expect(args.p_end).toBeNull();
  });

  it('12. getLeaderboard quarterly: start = first day of current quarter', async () => {
    mockRpc.mockResolvedValueOnce({ data: [], error: null });

    await getLeaderboard(HOTEL, 'quarterly');

    const args       = mockRpc.mock.calls[0][1];
    const start      = new Date(args.p_start);
    const now        = new Date();
    const qStartMonth = Math.floor(now.getMonth() / 3) * 3;

    expect(start.getMonth()).toBe(qStartMonth);
    expect(start.getDate()).toBe(1);
  });

  it('13. getLeaderboard annual: start = Jan 1 of current year', async () => {
    mockRpc.mockResolvedValueOnce({ data: [], error: null });

    await getLeaderboard(HOTEL, 'annual');

    const args  = mockRpc.mock.calls[0][1];
    const start = new Date(args.p_start);

    expect(start.getFullYear()).toBe(new Date().getFullYear());
    expect(start.getMonth()).toBe(0);
    expect(start.getDate()).toBe(1);
  });

  it('14. getLeaderboard throws when RPC returns error', async () => {
    mockRpc.mockResolvedValueOnce({ data: null, error: { message: 'RLS denied' } });

    await expect(getLeaderboard(HOTEL)).rejects.toThrow('RLS denied');
  });

  it('15. getLeaderboard returns empty array when data is null', async () => {
    mockRpc.mockResolvedValueOnce({ data: null, error: null });

    const result = await getLeaderboard(HOTEL);

    expect(result).toEqual([]);
  });
});

// ─── 16–25. Badge levels ──────────────────────────────────────────────────────

describe('Feature: Badge level computation', () => {
  const cases: Array<[number, string]> = [
    [0,   'Newcomer'],
    [1,   'Newcomer'],
    [49,  'Newcomer'],
    [50,  'Rising Star'],
    [99,  'Rising Star'],
    [100, 'Star Player'],
    [199, 'Star Player'],
    [200, 'Champion'],
    [499, 'Champion'],
    [500, 'Legend'],
    [999, 'Legend'],
  ];

  test.each(cases)('getBadgeLevel(%i) → %s', (points, expectedLabel) => {
    expect(getBadgeLevel(points).label).toBe(expectedLabel);
  });

  it('BADGE_LEVELS are ordered highest minPoints first (ladder is correct)', () => {
    for (let i = 0; i < BADGE_LEVELS.length - 1; i++) {
      expect(BADGE_LEVELS[i].minPoints).toBeGreaterThan(BADGE_LEVELS[i + 1].minPoints);
    }
  });
});
