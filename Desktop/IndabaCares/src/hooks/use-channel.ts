import { useInfiniteQuery } from '@tanstack/react-query';
import { getChannelPosts } from '@/api/channel-service';
import { QUERY_KEYS } from '@/lib/constants';

export function useChannelPosts(hotel: string) {
  return useInfiniteQuery({
    queryKey: QUERY_KEYS.channelPosts(hotel),
    queryFn: async ({ pageParam }) => {
      return getChannelPosts(hotel, pageParam as string | undefined);
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => {
      if (lastPage.length < 15) return undefined;
      return lastPage[lastPage.length - 1]?.created_at;
    },
    enabled: !!hotel,
    staleTime: 2 * 60 * 1000,
  });
}
