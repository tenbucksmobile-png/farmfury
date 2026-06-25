import { supabase } from '@/lib/supabase';

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
  created_at:     string;
}

export async function getMonthlyLegends(hotel: string, year: number): Promise<MonthlyLegend[]> {
  const { data, error } = await supabase
    .from('monthly_legends')
    .select('id, hotel, employee_id, full_name, job_title, avatar_url, month, year, total_points, points_awarded, created_at')
    .eq('hotel', hotel)
    .eq('year', year)
    .order('month', { ascending: true });

  if (error) throw new Error(error.message);
  return (data ?? []) as MonthlyLegend[];
}
