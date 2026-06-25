import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 2,
      // 4-hour GC — long enough for a hotel shift, short enough to prevent
      // unbounded memory growth during all-day usage.
      gcTime: 1000 * 60 * 60 * 4,
      staleTime: 2 * 60 * 1000,
      refetchOnWindowFocus: false,
      // Prevent waterfall re-fetches when the user backgrounds + foregrounds
      // the app repeatedly (common for hotel floor staff).
      refetchOnReconnect: 'always',
    },
    mutations: {
      retry: 1,
    },
  },
});

export function QueryProvider({ children }: { children: React.ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}

export { queryClient };
