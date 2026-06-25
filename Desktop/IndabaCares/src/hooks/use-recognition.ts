import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { recognitionDetailQuery } from '@/api/queries';
import { sendRecognition } from '@/api/edge-functions';
import { QUERY_KEYS } from '@/lib/constants';
import { useUIStore } from '@/stores/ui-store';
import type { SendRecognitionRequest } from '@/types/api';

export function useRecognitionDetail(id: string) {
  return useQuery({
    queryKey: QUERY_KEYS.recognition(id),
    queryFn: async () => {
      const { data, error } = await recognitionDetailQuery(id);
      if (error) throw error;
      return data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useSendRecognition() {
  const queryClient = useQueryClient();
  const showToast = useUIStore((s) => s.showToast);

  return useMutation({
    mutationFn: (body: SendRecognitionRequest) => sendRecognition(body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.feed });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.me });
      showToast({ type: 'success', message: 'Recognition sent!' });
    },
    onError: (error: Error) => {
      showToast({ type: 'error', message: error.message });
    },
  });
}
