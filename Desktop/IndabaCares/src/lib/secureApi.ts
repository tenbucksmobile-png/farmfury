/**
 * secureApi.ts — Network security layer
 *
 * Expo managed workflow does not expose native networking hooks, so full
 * certificate pinning via TrustKit / okhttp-pinning is not available without
 * ejecting to bare workflow. This module implements the practical mitigations
 * available within the managed runtime:
 *
 *   1. Domain allowlist — every outbound URL is validated against a strict
 *      list before the request is dispatched. Blocks accidental calls to
 *      typo-squatted or redirected endpoints.
 *
 *   2. HTTPS enforcement — rejects any URL that does not use https:// so
 *      plain-HTTP downgrade (e.g. hostile captive portals on hotel WiFi)
 *      is caught before credentials leave the device.
 *
 *   3. Request timeout — guards against slow-response attacks and hanging
 *      requests that block the UI indefinitely.
 *
 *   4. Response Content-Type validation — JSON endpoints must return
 *      application/json. Unexpected content types (e.g. redirect to an HTML
 *      login page from a captive portal) are rejected as errors.
 *
 *   5. Redirect guard — follows at most 3 redirects. Bails on any redirect
 *      that would leave the allowlisted domain.
 *
 * The supabase client is initialised with this fetch adapter (src/lib/supabase.ts
 * injects session headers on top of it).
 *
 * NOTE ON CERTIFICATE PINNING:
 *   For full certificate pinning in a future bare/EAS build, add
 *   react-native-ssl-pinning (Android + iOS) or configure okhttp-pinning
 *   via a custom Expo config plugin. Supabase's TLS certificate is issued
 *   by Let's Encrypt; pin the ISRG Root X1 / X2 intermediates.
 */

const ALLOWED_HOSTNAMES = new Set([
  'supabase.co',              // Supabase API root
  'typfhdrmtusmffxfclfq.supabase.co', // This project's PostgREST / Storage / Auth
  'api.expo.dev',             // Expo push notifications
  'exp.host',                 // Expo push delivery endpoint
]);

const REQUEST_TIMEOUT_MS  = 15_000; // 15 seconds
const MAX_REDIRECT_HOPS   = 3;

// ─── Domain validator ─────────────────────────────────────────────────────────

function isAllowedUrl(urlString: string): boolean {
  try {
    const url = new URL(urlString);

    // Reject anything that is not HTTPS
    if (url.protocol !== 'https:') return false;

    const host = url.hostname.toLowerCase();

    // Direct match
    if (ALLOWED_HOSTNAMES.has(host)) return true;

    // Subdomain of an allowed domain (e.g. *.supabase.co)
    for (const allowed of ALLOWED_HOSTNAMES) {
      if (host.endsWith(`.${allowed}`)) return true;
    }

    return false;
  } catch {
    return false;
  }
}

// ─── Secure fetch ─────────────────────────────────────────────────────────────

export async function secureFetch(
  input:   RequestInfo | URL,
  init:    RequestInit = {},
  hops     = 0,
): Promise<Response> {
  const urlString = typeof input === 'string'
    ? input
    : input instanceof URL
      ? input.toString()
      : (input as Request).url;

  // ── 1. Domain + HTTPS check ─────────────────────────────────────────────────
  if (!isAllowedUrl(urlString)) {
    throw new SecurityError(
      `Blocked request to disallowed or non-HTTPS endpoint: ${sanitizeUrl(urlString)}`
    );
  }

  // ── 2. Timeout via AbortController ─────────────────────────────────────────
  const controller  = new AbortController();
  const timeoutId   = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

  let response: Response;

  try {
    response = await fetch(input, {
      ...init,
      signal: controller.signal,
      // Prevent automatic browser-level redirect following so we can
      // validate each hop ourselves
      redirect: 'manual',
    });
  } finally {
    clearTimeout(timeoutId);
  }

  // ── 3. Redirect guard ───────────────────────────────────────────────────────
  if (response.status >= 300 && response.status < 400) {
    const location = response.headers.get('Location');
    if (!location) {
      throw new SecurityError('Redirect with no Location header');
    }
    if (hops >= MAX_REDIRECT_HOPS) {
      throw new SecurityError(`Too many redirects (max ${MAX_REDIRECT_HOPS})`);
    }
    if (!isAllowedUrl(location)) {
      throw new SecurityError(
        `Redirect to disallowed domain blocked: ${sanitizeUrl(location)}`
      );
    }
    // Safe redirect — follow manually.
    // 307 Temporary Redirect / 308 Permanent Redirect require the original
    // method and body to be preserved (RFC 7231 §6.4.7 / RFC 7538 §3).
    // 301 / 302 / 303 conventionally downgrade to GET.
    const preserveMethod = response.status === 307 || response.status === 308;
    return secureFetch(
      location,
      preserveMethod ? init : { ...init, method: 'GET', body: undefined },
      hops + 1,
    );
  }

  // ── 4. Content-Type validation for JSON-returning endpoints ────────────────
  // Only validate non-empty responses to JSON-specific paths.
  // Storage (binary) and auth endpoints are excluded.
  const contentType = response.headers.get('Content-Type') ?? '';
  const url = new URL(urlString);
  const isJsonPath =
    url.pathname.startsWith('/rest/') ||
    url.pathname.startsWith('/functions/');

  if (
    isJsonPath &&
    response.status < 300 &&
    response.headers.has('Content-Type') &&
    !contentType.includes('application/json') &&
    !contentType.includes('application/vnd.pgrst')
  ) {
    throw new SecurityError(
      `Unexpected Content-Type "${contentType.split(';')[0]}" from JSON endpoint. ` +
      'This may indicate a captive portal or MITM interception.'
    );
  }

  return response;
}

// ─── Error type ───────────────────────────────────────────────────────────────

export class SecurityError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'SecurityError';
  }
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Strip auth tokens from a URL before logging — never log credentials */
function sanitizeUrl(urlString: string): string {
  try {
    const url    = new URL(urlString);
    url.search   = '';   // drop all query params (may contain apikey)
    url.hash     = '';
    return url.origin + url.pathname;
  } catch {
    return '[invalid URL]';
  }
}
