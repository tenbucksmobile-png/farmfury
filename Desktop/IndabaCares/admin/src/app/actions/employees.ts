'use server';

import { revalidatePath } from 'next/cache';
import { createAdminClient } from '@/lib/supabase/admin';

/** Toggle an employee's status between active and inactive. */
export async function toggleEmployeeStatus(id: string, currentStatus: string) {
  const db   = createAdminClient();
  const next = currentStatus === 'active' ? 'inactive' : 'active';

  const { error } = await db
    .from('employees')
    .update({ status: next })
    .eq('id', id);

  if (error) throw new Error(error.message);
  revalidatePath('/employees');
}

/** Create a single employee record. */
export async function createEmployee(fields: {
  full_name:     string;
  employee_code: string;
  hotel:         string;
  department:    string | null;
  position:      string | null;
  email:         string | null;
  date_of_birth: string | null;
  start_date:    string | null;
  is_manager:    boolean;
}) {
  const db = createAdminClient();

  const { error } = await db.from('employees').insert({
    full_name:     fields.full_name.trim(),
    employee_code: fields.employee_code.trim().toUpperCase(),
    hotel:         fields.hotel,
    department:    fields.department?.trim() || null,
    position:      fields.position?.trim()   || null,
    email:         fields.email?.trim().toLowerCase() || null,
    date_of_birth: fields.date_of_birth || null,
    start_date:    fields.start_date    || null,
    is_manager:    fields.is_manager,
    status:        'active',
  });

  if (error) {
    if (error.code === '23505') throw new Error('Employee code already exists for this hotel.');
    throw new Error(error.message);
  }
  revalidatePath('/employees');
}

/**
 * Delete an employee record.
 * Blocked if the employee has any recognitions or redemptions — deactivate instead.
 */
export async function deleteEmployee(id: string) {
  const db = createAdminClient();

  // Check for linked activity
  const [{ count: recCount }, { count: redCount }] = await Promise.all([
    db.from('recognitions').select('id', { count: 'exact', head: true }).or(`sender_id.eq.${id},recipient_id.eq.${id}`),
    db.from('redemptions').select('id', { count: 'exact', head: true }).eq('employee_id', id),
  ]);

  if ((recCount ?? 0) > 0 || (redCount ?? 0) > 0) {
    throw new Error(
      'This employee has existing activity (recognitions or redemptions) and cannot be deleted. Use Deactivate instead.',
    );
  }

  const { error } = await db.from('employees').delete().eq('id', id);
  if (error) throw new Error(error.message);
  revalidatePath('/employees');
}

/** Reset an employee's password (clears hash + revokes sessions). */
export async function resetEmployeePassword(id: string) {
  const db = createAdminClient();

  const { data, error } = await db.rpc('reset_employee_password', { p_id: id });

  if (error) throw new Error(error.message);
  if (!(data as any)?.ok) throw new Error((data as any)?.error ?? 'Reset failed.');
  revalidatePath('/employees');
}

/** Update editable fields on an employee record. */
export async function updateEmployee(
  id: string,
  fields: {
    full_name:      string;
    department:     string | null;
    position:       string | null;
    email:          string | null;
    date_of_birth:  string | null;
    start_date:     string | null;
    is_manager:            boolean;
    points_balance?:       number;
    reward_wallet_balance?: number;
  },
) {
  const db = createAdminClient();

  // Update non-guarded fields only — points_balance and reward_wallet_balance
  // are protected by guard triggers and must go through dedicated RPCs.
  const { error } = await db
    .from('employees')
    .update({
      full_name:     fields.full_name.trim(),
      department:    fields.department?.trim() || null,
      position:      fields.position?.trim()   || null,
      email:         fields.email?.trim().toLowerCase() || null,
      date_of_birth: fields.date_of_birth || null,
      start_date:    fields.start_date    || null,
      is_manager:    fields.is_manager,
    })
    .eq('id', id);

  if (error) throw new Error(error.message);

  // points_balance — guarded by trg_guard_points_balance (migration 025/026).
  if (fields.points_balance !== undefined) {
    const { error: pointsError } = await db.rpc('admin_set_points_balance', {
      p_employee_id: id,
      p_new_balance: Math.max(0, Math.round(fields.points_balance)),
    });
    if (pointsError) throw new Error(pointsError.message);
  }

  // reward_wallet_balance — guarded by trg_guard_wallet_balance (migration 079).
  if (fields.reward_wallet_balance !== undefined) {
    const { error: walletError } = await db.rpc('admin_set_wallet_balance', {
      p_employee_id: id,
      p_new_balance: Math.max(0, Math.round(fields.reward_wallet_balance)),
    });
    if (walletError) throw new Error(walletError.message);
  }

  revalidatePath('/employees');
}
