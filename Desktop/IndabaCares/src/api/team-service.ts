import { supabase } from '@/lib/supabase';

export interface TeamMember {
  id:             string;
  full_name:      string;
  job_title:      string | null;  // legacy column
  position:       string | null;  // admin-portal field
  department:     string | null;
  photo_url:      string | null;
  employee_code:  string;
  points_balance: number;
}

/**
 * Fetch all active employees in a given department for a hotel.
 */
export async function getTeamByDepartment(hotel: string, department: string): Promise<TeamMember[]> {
  const { data, error } = await supabase
    .from('employees')
    .select('id, full_name, job_title, position, department, photo_url, employee_code, points_balance')
    .eq('hotel', hotel)
    .eq('department', department)
    .eq('status', 'active')
    .order('full_name', { ascending: true });

  if (error) throw new Error(error.message);
  return (data ?? []) as TeamMember[];
}

/**
 * Fetch distinct departments for a hotel (only departments with active employees).
 */
export async function getDepartments(hotel: string): Promise<string[]> {
  const { data, error } = await supabase
    .from('employees')
    .select('department')
    .eq('hotel', hotel)
    .eq('status', 'active')
    .not('department', 'is', null);

  if (error) throw new Error(error.message);

  const unique = Array.from(
    new Set((data ?? []).map((r: { department: string | null }) => r.department).filter(Boolean))
  ).sort() as string[];

  return unique;
}
