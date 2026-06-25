'use client';

import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { createClient } from '@/lib/supabase/client';
import { useAuthStore } from '@/stores/auth-store';
import { authMe } from '@/api/edge-functions';

export function useAuth() {
  const supabase = createClient();
  const { session, user, company, isLoading, setSession, setUserContext, setLoading, logout } =
    useAuthStore();

  // Bootstrap session
  useEffect(() => {
    supabase.auth.getSession().then(({ data: { session } }) => {
      setSession(session);
      setLoading(false);
    });

    const {
      data: { subscription },
    } = supabase.auth.onAuthStateChange((_event, session) => {
      setSession(session);
      if (!session) logout();
    });

    return () => subscription.unsubscribe();
  }, []);

  // Fetch user context from auth-me edge function
  const { isError } = useQuery({
    queryKey: ['admin-me'],
    queryFn: async () => {
      const data = await authMe();
      setUserContext(data.user, data.company);
      return data;
    },
    enabled: !!session,
    staleTime: 5 * 60 * 1000,
    retry: 1,
  });

  // Log auth-me failures but do not sign out — admin session is valid via Supabase Auth
  useEffect(() => {
    if (isError) {
      console.error('Failed to load admin context from auth-me');
    }
  }, [isError]);

  // Derived from Supabase Auth user_metadata (set manually per-user in the dashboard)
  const meta = (session?.user?.user_metadata ?? {}) as Record<string, unknown>;
  const isChannelOnly = !!meta.hotel && !meta.is_super_admin;

  return {
    session,
    user,
    company,
    isLoading,
    role: user?.role ?? null,
    isSuperAdmin: user?.role === 'super_admin' || !!meta.is_super_admin,
    isChannelOnly,
    adminHotel: (meta.hotel as string) ?? null,
    companyId: company?.id ?? null,
  };
}
