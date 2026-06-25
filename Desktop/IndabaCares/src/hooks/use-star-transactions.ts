import { useQuery } from '@tanstack/react-query';
import { pointsLedgerQuery } from '@/api/queries';
import { QUERY_KEYS } from '@/lib/constants';
import { useEmployee } from '@/providers/EmployeeContext';

export interface StarTransaction {
  id: string;
  type: string;     // maps to points_ledger.source
  amount: number;   // maps to points_ledger.points
  balance_after: number;
  description: string;
  reference_type: string | null;
  reference_id: string | null;
  created_at: string;
}

export function useStarTransactions() {
  const { employee } = useEmployee();

  return useQuery({
    queryKey: [...QUERY_KEYS.starTransactions, employee?.employee_id],
    queryFn: async () => {
      if (!employee) return [];
      const { data, error } = await pointsLedgerQuery(employee.employee_id);
      if (error) throw error;
      // Normalise points_ledger rows to StarTransaction shape
      return ((data ?? []) as any[]).map((row) => ({
        id:             row.id,
        type:           row.source,
        amount:         row.points,
        balance_after:  0,          // points_ledger does not store running total
        description:    row.source.replace(/_/g, ' '),
        reference_type: null,
        reference_id:   null,
        created_at:     row.created_at,
      })) as StarTransaction[];
    },
    enabled: !!employee,
    staleTime: 2 * 60 * 1000,
  });
}
