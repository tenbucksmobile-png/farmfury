import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { useEmployee } from '@/providers/EmployeeContext';

export interface CelebrationFeedItem {
  _type:        'celebration';
  id:           string;
  type:         'birthday' | 'anniversary';
  milestone:    number | null;
  celebrated_on: string;
  created_at:   string;
  employee: {
    id:         string;
    full_name:  string;
    hotel:      string;
    department: string | null;
    position:   string | null;
    photo_url: string | null;
  };
}

export function useCelebrations(hotel: string) {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: ['celebrations', hotel],
    queryFn: async (): Promise<CelebrationFeedItem[]> => {
      if (!employee || !hotel) return [];

      const today = new Date().toISOString().split('T')[0];

      const { data, error } = await supabase
        .from('celebrations')
        .select(`
          id,
          type,
          milestone,
          celebrated_on,
          created_at,
          employee:employees!employee_id ( id, full_name, hotel, department, position, photo_url )
        `)
        .eq('celebrated_on', today)
        .eq('hotel', hotel)
        .order('created_at', { ascending: false });

      if (error) throw error;

      return (data ?? []).map((row: any) => ({
        _type:         'celebration' as const,
        id:            row.id,
        type:          row.type,
        milestone:     row.milestone,
        celebrated_on: row.celebrated_on,
        created_at:    row.created_at,
        employee: {
          id:         row.employee.id,
          full_name:  row.employee.full_name,
          hotel:      row.employee.hotel,
          department: row.employee.department ?? null,
          position:   row.employee.position   ?? null,
          photo_url: row.employee.photo_url ?? null,
        },
      }));
    },
    enabled:   !!employee,
    staleTime: 60 * 60 * 1000, // 1 hour — celebrations don't change during the day
  });
}
