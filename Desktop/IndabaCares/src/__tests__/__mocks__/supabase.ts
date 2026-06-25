/**
 * Manual mock for @/lib/supabase
 * Import this via jest.mock('@/lib/supabase') in test files.
 */

export const mockRpc = jest.fn();
export const mockFrom = jest.fn();
export const mockSetSessionToken = jest.fn();

// Chainable query builder returned by .from()
const queryBuilder = {
  select: jest.fn().mockReturnThis(),
  eq:     jest.fn().mockReturnThis(),
  single: jest.fn(),
  update: jest.fn().mockReturnThis(),
  insert: jest.fn().mockReturnThis(),
  delete: jest.fn().mockReturnThis(),
};

mockFrom.mockReturnValue(queryBuilder);

export const supabase = {
  rpc:  mockRpc,
  from: mockFrom,
};

export const setSessionToken = mockSetSessionToken;

/** Helper: reset all mocks between tests */
export function resetSupabaseMocks() {
  mockRpc.mockReset();
  mockFrom.mockReset();
  mockSetSessionToken.mockReset();
  mockFrom.mockReturnValue(queryBuilder);
  Object.values(queryBuilder).forEach((fn) => {
    if (typeof fn === 'function') (fn as jest.Mock).mockReset();
  });
  queryBuilder.select.mockReturnThis();
  queryBuilder.eq.mockReturnThis();
  queryBuilder.update.mockReturnThis();
  queryBuilder.insert.mockReturnThis();
  queryBuilder.delete.mockReturnThis();
}
