import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { useEmployee } from '@/providers/EmployeeContext';
import type { RecognitionFeedItem } from '@/api/queries';

const SEARCH_DEBOUNCE_MS = 300;

/** Debounces the query term so the RPC is not called on every keystroke. */
function useDebouncedValue(value: string, delayMs: number): string {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(id);
  }, [value, delayMs]);

  return debounced;
}

export function useFeedSearch(searchTerm: string, hotel: string) {
  const { employee } = useEmployee();
  const trimmed  = searchTerm.trim();
  const debounced = useDebouncedValue(trimmed, SEARCH_DEBOUNCE_MS);

  return useQuery<RecognitionFeedItem[]>({
    queryKey: ['feed-search', hotel, debounced],
    queryFn: async () => {
      if (!employee || !debounced || !hotel) return [];

      const { data, error } = await supabase.rpc('search_recognitions', {
        p_hotel:  hotel,
        p_search: debounced,
        p_limit:  100,
      });

      if (error) throw error;
      return (data as RecognitionFeedItem[]) ?? [];
    },
    enabled: !!employee && debounced.length > 0,
    staleTime: 30_000,
  });
}
