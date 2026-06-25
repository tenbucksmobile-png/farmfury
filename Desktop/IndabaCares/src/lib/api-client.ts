/**
 * api-client — PERF-06
 *
 * Central wrapper for all Supabase data requests. Adds:
 *   - Per-request AbortController timeout (default 10 s)
 *   - Exponential-backoff retry for transient network errors
 *   - Debounce helper for search / autocomplete inputs
 */

// ─── Timeout wrapper ──────────────────────────────────────────────────────────

const DEFAULT_TIMEOUT_MS = 10_000;

/**
 * Race a promise against a timeout.
 * Throws with message "Request timed out" when the deadline expires.
 */
export async function withTimeout<T>(
  promise: Promise<T>,
  ms = DEFAULT_TIMEOUT_MS,
): Promise<T> {
  let timer: ReturnType<typeof setTimeout>;

  const timeout = new Promise<never>((_, reject) => {
    timer = setTimeout(() => reject(new Error('Request timed out')), ms);
  });

  try {
    const result = await Promise.race([promise, timeout]);
    clearTimeout(timer!);
    return result;
  } catch (err) {
    clearTimeout(timer!);
    throw err;
  }
}

// ─── Retry with exponential backoff ──────────────────────────────────────────

const RETRYABLE_MESSAGES = [
  'network request failed',
  'failed to fetch',
  'request timed out',
  'etimedout',
  'econnreset',
];

function isRetryable(err: unknown): boolean {
  if (!(err instanceof Error)) return false;
  const msg = err.message.toLowerCase();
  return RETRYABLE_MESSAGES.some((s) => msg.includes(s));
}

/**
 * Retry a function up to `maxAttempts` times with exponential backoff.
 * Only retries on network / timeout errors — never on auth or validation errors.
 */
export async function withRetry<T>(
  fn: () => Promise<T>,
  maxAttempts = 3,
  baseDelayMs = 500,
): Promise<T> {
  let lastErr: unknown;

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      return await fn();
    } catch (err) {
      lastErr = err;
      if (attempt === maxAttempts || !isRetryable(err)) throw err;
      await delay(baseDelayMs * 2 ** (attempt - 1));
    }
  }

  throw lastErr;
}

function delay(ms: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, ms));
}

// ─── Debounce ─────────────────────────────────────────────────────────────────

/**
 * Returns a debounced version of `fn`.
 * The returned function type preserves the original signature.
 *
 * Usage:
 *   const debouncedSearch = debounce((q: string) => fetchResults(q), 300);
 */
export function debounce<Args extends unknown[]>(
  fn: (...args: Args) => void,
  waitMs: number,
): (...args: Args) => void {
  let timer: ReturnType<typeof setTimeout> | null = null;

  return (...args: Args) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      timer = null;
      fn(...args);
    }, waitMs);
  };
}

// ─── Convenience: fetch with timeout + retry ─────────────────────────────────

/**
 * Wraps any async data-fetch with both timeout and retry.
 * Use this at the service layer for RPC calls or REST requests.
 *
 * Example:
 *   const data = await fetchWithGuards(() => supabase.rpc('my_fn', args));
 */
export async function fetchWithGuards<T>(
  fn: () => Promise<T>,
  opts?: { timeoutMs?: number; maxAttempts?: number },
): Promise<T> {
  const { timeoutMs = DEFAULT_TIMEOUT_MS, maxAttempts = 3 } = opts ?? {};
  return withRetry(
    () => withTimeout(fn(), timeoutMs),
    maxAttempts,
  );
}
