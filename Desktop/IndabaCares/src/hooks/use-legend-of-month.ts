import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { useEmployee } from '@/providers/EmployeeContext';

export interface MonthlyLegend {
  id:             string;
  hotel:          string;
  employee_id:    string;
  full_name:      string;
  job_title:      string | null;
  avatar_url:     string | null;
  month:          number;
  year:           number;
  total_points:   number;
  points_awarded: number;
  recognition_id: string | null;
}

export function useLegendOfMonth() {
  const { employee } = useEmployee();
  const now = new Date();

  return useQuery({
    queryKey: ['legend-of-month', employee?.hotel, now.getMonth(), now.getFullYear()],
    queryFn: async (): Promise<MonthlyLegend | null> => {
      if (!employee) return null;

      const { data, error } = await supabase
        .from('monthly_legends')
        .select('*')
        .eq('hotel', employee.hotel)
        .eq('month', now.getMonth() + 1)
        .eq('year', now.getFullYear())
        .maybeSingle();

      if (error) throw error;
      return data as MonthlyLegend | null;
    },
    enabled: !!employee,
    staleTime: 60 * 60 * 1000, // 1 hour
  });
}
