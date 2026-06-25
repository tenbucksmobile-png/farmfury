import { createServerClient } from '@supabase/ssr';
import { NextResponse, type NextRequest } from 'next/server';

const SUPER_ADMIN_ONLY = ['/audit-logs', '/settings'];

const SECURITY_HEADERS: Record<string, string> = {
  'X-Frame-Options':           'DENY',
  'X-Content-Type-Options':    'nosniff',
  'Referrer-Policy':           'strict-origin-when-cross-origin',
  'Permissions-Policy':        'camera=(), microphone=(), geolocation=(), payment=()',
  'Strict-Transport-Security': 'max-age=2592000; includeSubDomains',
  'Content-Security-Policy':   [
    "default-src 'self'",
    "script-src  'self' 'unsafe-inline' 'unsafe-eval'",
    "style-src   'self' 'unsafe-inline'",
    "img-src     'self' data: blob: https:",
    "font-src    'self'",
    "connect-src 'self' https://*.supabase.co wss://*.supabase.co",
    "frame-ancestors 'none'",
    "form-action 'self'",
    "base-uri    'self'",
  ].join('; '),
};

function addSecurityHeaders(response: NextResponse): NextResponse {
  Object.entries(SECURITY_HEADERS).forEach(([key, value]) => {
    response.headers.set(key, value);
  });
  return response;
}

const PUBLIC_PATHS = ['/login', '/forgot-password', '/reset-password', '/privacy'];

export default async function proxy(request: NextRequest) {
  let supabaseResponse = NextResponse.next({ request });

  const isPublic = PUBLIC_PATHS.some((p) =>
    request.nextUrl.pathname.startsWith(p)
  );

  let user = null;

  try {
    const supabase = createServerClient(
      process.env.NEXT_PUBLIC_SUPABASE_URL!,
      process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
      {
        cookies: {
          getAll() {
            return request.cookies.getAll();
          },
          setAll(cookiesToSet) {
            cookiesToSet.forEach(({ name, value }) =>
              request.cookies.set(name, value)
            );
            supabaseResponse = NextResponse.next({ request });
            cookiesToSet.forEach(({ name, value, options }) =>
              supabaseResponse.cookies.set(name, value, options)
            );
          },
        },
      }
    );

    const { data } = await supabase.auth.getUser();
    user = data.user;
  } catch {
    // If Supabase is unreachable, allow public paths and block protected routes
    if (!isPublic) {
      const url = request.nextUrl.clone();
      url.pathname = '/login';
      return addSecurityHeaders(NextResponse.redirect(url));
    }
    return addSecurityHeaders(supabaseResponse);
  }

  if (!user && !isPublic) {
    const url = request.nextUrl.clone();
    url.pathname = '/login';
    return addSecurityHeaders(NextResponse.redirect(url));
  }

  if (user && request.nextUrl.pathname === '/login') {
    const url = request.nextUrl.clone();
    url.pathname = '/';
    return addSecurityHeaders(NextResponse.redirect(url));
  }

  if (user) {
    const role = user.app_metadata?.role as string | undefined;

    if (role !== 'admin' && role !== 'super_admin') {
      const url = request.nextUrl.clone();
      url.pathname = '/login';
      url.searchParams.set('error', 'unauthorized');
      return addSecurityHeaders(NextResponse.redirect(url));
    }

    if (
      SUPER_ADMIN_ONLY.some((p) => request.nextUrl.pathname.startsWith(p)) &&
      role !== 'super_admin'
    ) {
      const url = request.nextUrl.clone();
      url.pathname = '/';
      return addSecurityHeaders(NextResponse.redirect(url));
    }
  }

  return addSecurityHeaders(supabaseResponse);
}

export const config = {
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)',
  ],
};
