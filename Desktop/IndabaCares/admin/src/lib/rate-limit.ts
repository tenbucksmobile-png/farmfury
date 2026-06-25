/**
 * In-process sliding-window rate limiter for Next.js API routes.
 *
 * NOTE: This works per-process. In a multi-instance production deployment
 * (e.g. Vercel serverless), each instance has its own counter.
 * For strict cross-instance rate limiting, use a shared store (Redis / Upstash).
 * For the admin portal this is acceptable — the primary auth guard is the
 * Supabase JWT check; the rate limiter is defense-in-depth.
 */

interface Entry {
  count:     number;
  resetAt:   number; // ms epoch
}

const store = new Map<string, Entry>();

// Purge stale entries every 10 minutes to avoid unbounded growth.
setInterval(() => {
  const now = Date.now();
  for (const [key, entry] of store) {
    if (entry.resetAt < now) store.delete(key);
  }
}, 10 * 60 * 1000).unref?.();

export interface RateLimitResult {
  allowed:     boolean;
  remaining:   number;
  resetAt:     number;   // ms epoch
}

/**
 * Check and increment the rate limit counter for a given key.
 *
 * @param key           Identifier (e.g. IP address, user id)
 * @param maxRequests   Maximum allowed requests per window
 * @param windowSeconds Window duration in seconds
 */
export function rateLimit(
  key:            string,
  maxRequests:    number,
  windowSeconds:  number,
): RateLimitResult {
  const now     = Date.now();
  const windowMs = windowSeconds * 1_000;

  let entry = store.get(key);

  if (!entry || entry.resetAt < now) {
    entry = { count: 0, resetAt: now + windowMs };
    store.set(key, entry);
  }

  entry.count++;

  return {
    allowed:   entry.count <= maxRequests,
    remaining: Math.max(0, maxRequests - entry.count),
    resetAt:   entry.resetAt,
  };
}

/**
 * Extract the best available IP from a Next.js request.
 * Prefers x-forwarded-for (set by reverse proxies / Vercel).
 */
export function getClientIp(request: Request): string {
  const forwarded = (request.headers as Headers).get('x-forwarded-for');
  if (forwarded) return forwarded.split(',')[0].trim();
  return 'unknown';
}
