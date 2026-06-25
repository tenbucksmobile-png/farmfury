import { useQuery } from '@tanstack/react-query';
import { getCampaignsForHotel } from '@/api/campaigns-service';

export function useCampaigns(hotel: string | undefined) {
  return useQuery({
    queryKey:  ['campaigns', hotel],
    queryFn:   () => getCampaignsForHotel(hotel!),
    enabled:   !!hotel,
    staleTime: 5 * 60 * 1000,
  });
}
