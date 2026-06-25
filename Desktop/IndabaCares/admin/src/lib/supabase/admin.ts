/**
 * Server-only Supabase client using the service_role key.
 *
 * This bypasses ALL Row Level Security policies, including the
 * hotel-isolation policies added in migration 017.
 *
 * NEVER import this file in client components or expose it to the browser.
 * Only use it inside Server Components and Server Actions ('use server').
 */

import { createClient } from '@supabase/supabase-js';

export function createAdminClient() {
  const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const key = process.env.SUPABASE_SERVICE_ROLE_KEY;

  if (!url || !key) {
    throw new Error(
      'Missing NEXT_PUBLIC_SUPABASE_URL or SUPABASE_SERVICE_ROLE_KEY env vars'
    );
  }

  return createClient(url, key, {
    auth: {
      autoRefreshToken: false,
      persistSession:   false,
    },
  });
}
