import { useQuery } from '@tanstack/react-query';
import { supabase } from '@/lib/supabase';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export interface PointsBreakdown {
  recognition_received:    number;
  skills_received:         number;
  responses:               number;  // recognition_response + skills_response
  mood_checkin:            number;
  birthday:                number;
  anniversary:             number;
  status_unlock:           number;
  badge_achieved:          number;
  legend_of_month:         number;
  campaign_points:         number;
  special_management_award: number;
  total:                   number;
}

const ZERO: PointsBreakdown = {
  recognition_received:    0,
  skills_received:         0,
  responses:               0,
  mood_checkin:            0,
  birthday:                0,
  anniversary:             0,
  status_unlock:           0,
  badge_achieved:          0,
  legend_of_month:         0,
  campaign_points:         0,
  special_management_award: 0,
  total:                   0,
};

export function usePointsBreakdown() {
  const { employee } = useEmployee();
  const now   = new Date();
  const start = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
  const end   = new Date(now.getFullYear(), now.getMonth() + 1, 1).toISOString();

  return useQuery({
    queryKey: ['points-breakdown', employee?.employee_id ?? '', start],
    queryFn:  async (): Promise<PointsBreakdown> => {
      if (!employee) return ZERO;

      const { data, error } = await supabase
        .from('points_ledger')
        .select('source, points')
        .eq('employee_id', employee.employee_id)
        .gte('created_at', start)
        .lt('created_at', end);

      if (error) throw error;
      if (!data?.length) return ZERO;

      const acc: Record<string, number> = {};
      let total = 0;
      for (const row of data) {
        if (row.points > 0) {
          acc[row.source] = (acc[row.source] ?? 0) + row.points;
          total += row.points;
        }
      }

      return {
        recognition_received: acc['recognition_received'] ?? 0,
        skills_received:      acc['skills_received']      ?? 0,
        responses:            (acc['recognition_response'] ?? 0) + (acc['skills_response'] ?? 0),
        mood_checkin:         acc['mood_checkin']          ?? 0,
        birthday:             acc['birthday']              ?? 0,
        anniversary:          acc['anniversary']           ?? 0,
        status_unlock:        acc['status_unlock']         ?? 0,
        badge_achieved:          acc['badge_achieved']           ?? 0,
        legend_of_month:         acc['legend_of_month']          ?? 0,
        campaign_points:         acc['campaign_points']          ?? 0,
        special_management_award: acc['special_management_award'] ?? 0,
        total,
      };
    },
    enabled:   !!employee,
    staleTime: 60 * 1000,
  });
}
