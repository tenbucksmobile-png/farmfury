import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { useEmployee } from '@/providers/EmployeeContext';

export interface GivenBadgeEntry {
  id: string;
  created_at: string;
  receiver: {
    full_name: string;
    photo_url: string | null;
  };
}

export function useGivenBadgeHistory(cardType: 'recognition' | 'skills') {
  const { employee } = useEmployee();
  const now   = new Date();
  const start = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
  const end   = new Date(now.getFullYear(), now.getMonth() + 1, 1).toISOString();

  return useQuery({
    queryKey: ['given-badge-history', employee?.employee_id ?? '', cardType],
    queryFn:  async (): Promise<GivenBadgeEntry[]> => {
      if (!employee) return [];

      const { data, error } = await supabase
        .from('recognitions')
        .select('id, created_at, receiver:employees!receiver_id ( full_name, photo_url )')
        .eq('sender_id', employee.employee_id)
        .eq('card_type', cardType)
        .gte('created_at', start)
        .lt('created_at', end)
        .order('created_at', { ascending: false });

      if (error) throw error;
      return (data ?? []) as unknown as GivenBadgeEntry[];
    },
    enabled:   !!employee,
    staleTime: 30_000,
  });
}
