import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { EmployeesClient } from './employees-client';

export const dynamic = 'force-dynamic';

async function getEmployees(hotel?: string) {
  const db = createAdminClient();
  let q = db
    .from('employees')
    .select('id, employee_code, full_name, hotel, department, position, email, status, points_balance, reward_wallet_balance, is_manager, created_at')
    .order('hotel')
    .order('full_name');

  if (hotel) q = q.eq('hotel', hotel);

  const { data, error } = await q;
  if (error) throw new Error(error.message);
  return data ?? [];
}

export default async function EmployeesPage({
  searchParams,
}: {
  searchParams: Promise<{ hotel?: string }>;
}) {
  const { hotel } = await searchParams;
  const employees = await getEmployees(hotel);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Employees"
        description="Manage employee records. Upload a CSV to bulk-import employees."
      />
      <EmployeesClient employees={employees} selectedHotel={hotel} />
    </div>
  );
}
