import { useInfiniteQuery } from '@tanstack/react-query';
import { feedQuery } from '@/api/queries';
import { QUERY_KEYS, PAGE_SIZE } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export function useFeed(hotel: string) {
  const { employee } = useEmployee();

  return useInfiniteQuery({
    queryKey: [...QUERY_KEYS.feed, hotel],
    queryFn: async ({ pageParam }) => {
      if (!employee || !hotel) return [];
      const { data, error } = await feedQuery(hotel, pageParam);
      if (error) throw error;
      return data ?? [];
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => {
      if (lastPage.length < PAGE_SIZE) return undefined;
      return lastPage[lastPage.length - 1]?.created_at;
    },
    enabled: !!employee && !!hotel,
    staleTime: 2 * 60 * 1000,
  });
}
