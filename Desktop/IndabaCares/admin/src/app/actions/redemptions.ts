'use server';

import { revalidatePath } from 'next/cache';
import { Resend }         from 'resend';
import { createAdminClient }   from '@/lib/supabase/admin';
import { buildVoucherEmail }   from '@/lib/email/voucher-template';

export async function approveRedemption(id: string) {
  const db = createAdminClient();

  const { data, error } = await db.rpc('approve_redemption', { p_redemption_id: id });
  if (error) throw new Error(error.message);
  if (!(data as any)?.ok) throw new Error((data as any)?.error ?? 'Approval failed.');

  // ── Send voucher email for hotel rewards ──────────────────────────────────
  try {
    if (!process.env.RESEND_API_KEY) throw new Error('RESEND_API_KEY not configured');
    const resend = new Resend(process.env.RESEND_API_KEY);

    const { data: redemption } = await db
      .from('redemptions')
      .select(`
        id, points_used, approved_at, hotel,
        employee:employees!employee_id ( full_name, email ),
        reward:rewards!reward_id       ( title, image_url, category, terms )
      `)
      .eq('id', id)
      .single();

    const emp    = redemption?.employee as any;
    const reward = redemption?.reward   as any;

    if (reward?.category === 'hotel' && emp?.email) {
      const { subject, html } = buildVoucherEmail({
        employeeName:   emp.full_name,
        hotelName:      (redemption as any)?.hotel ?? '',
        rewardTitle:    reward.title,
        rewardImageUrl: reward.image_url ?? null,
        voucherCode:    id,
        pointsUsed:     redemption!.points_used,
        terms:          reward.terms ?? null,
        approvedAt:     redemption!.approved_at ?? new Date().toISOString(),
      });

      await resend.emails.send({
        from:    `Indaba Cares <vouchers@${process.env.RESEND_FROM_DOMAIN ?? 'indabacares.com'}>`,
        to:      emp.email,
        subject,
        html,
      });
    }
  } catch (emailErr) {
    // Email failure must not block the approval — log and continue
    console.error('Voucher email failed:', emailErr);
  }

  revalidatePath('/redemptions');
  revalidatePath('/rewards/redemptions');
}

export async function rejectRedemption(id: string, reason?: string) {
  const db = createAdminClient();
  const { data, error } = await db.rpc('reject_redemption', {
    p_redemption_id: id,
    p_reason:        reason ?? null,
  });
  if (error) throw new Error(error.message);
  if (!(data as any)?.ok) throw new Error((data as any)?.error ?? 'Rejection failed.');
  revalidatePath('/redemptions');
  revalidatePath('/rewards/redemptions');
}

export async function fulfillRedemption(id: string) {
  const db = createAdminClient();
  const { data, error } = await db.rpc('fulfill_redemption', { p_redemption_id: id });
  if (error) throw new Error(error.message);
  if (!(data as any)?.ok) throw new Error((data as any)?.error ?? 'Fulfillment failed.');
  revalidatePath('/redemptions');
  revalidatePath('/rewards/redemptions');
}
