import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export const MONTHLY_RECOGNITION_LIMIT = 10;

export function useRecognitionBalance() {
  const { employee } = useEmployee();
  const now   = new Date();
  const start = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
  const end   = new Date(now.getFullYear(), now.getMonth() + 1, 1).toISOString();

  return useQuery({
    queryKey: QUERY_KEYS.recognitionBalance(employee?.employee_id ?? ''),
    queryFn:  async (): Promise<number> => {
      if (!employee) return MONTHLY_RECOGNITION_LIMIT;

      const { count, error } = await supabase
        .from('recognitions')
        .select('id', { count: 'exact', head: true })
        .eq('sender_id', employee.employee_id)
        .eq('card_type', 'recognition')
        .gte('created_at', start)
        .lt('created_at', end);

      if (error) throw error;
      return Math.max(0, MONTHLY_RECOGNITION_LIMIT - (count ?? 0));
    },
    enabled:   !!employee,
    staleTime: 30 * 1000,
  });
}
