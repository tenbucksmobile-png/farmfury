import { useQuery } from '@tanstack/react-query';
import { getInitiatives, getInitiativeThumbnails, getInitiativeHotels } from '@/api/initiative-service';

/**
 * Returns distinct hotels that have CSR initiative content.
 * Used by the hotel-picker screen.
 */
export function useInitiativeHotels() {
  return useQuery({
    queryKey: ['initiative-hotels'],
    queryFn:  () => getInitiativeHotels(),
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Fetch one thumbnail per initiative tab for the given hotel.
 * `hotel` is the explicitly chosen hotel (from the picker), not the employee's own hotel.
 */
export function useInitiativeThumbnails(hotel: string) {
  return useQuery({
    queryKey: ['initiative-thumbnails', hotel],
    queryFn:  () => getInitiativeThumbnails(hotel),
    enabled:  !!hotel,
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Fetch full initiative content for a specific hotel + tab.
 * `hotel` is the explicitly chosen hotel (from the picker), not the employee's own hotel.
 */
export function useInitiatives(hotel: string, tab: string) {
  return useQuery({
    queryKey: ['initiatives', hotel, tab],
    queryFn:  () => getInitiatives(hotel, tab),
    enabled:  !!hotel && !!tab,
    staleTime: 5 * 60 * 1000,
  });
}
