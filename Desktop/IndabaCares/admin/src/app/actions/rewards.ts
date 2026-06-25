'use server';

import { revalidatePath } from 'next/cache';
import { z } from 'zod';
import { createAdminClient } from '@/lib/supabase/admin';
import { rewardSchema, employeeIdSchema, formatValidationError } from '@/lib/validation';

export async function createReward(raw: unknown) {
  const payload = rewardSchema.parse(raw);

  const db = createAdminClient();
  const { error } = await db.from('rewards').insert({
    title:           payload.title,
    description:     payload.description || null,
    points_required: payload.points_required,
    hotel:           payload.hotels[0],
    hotels:          payload.hotels,
    stock:           payload.stock ?? null,
    image_url:       payload.image_url || null,
    category:        payload.category ?? 'hotel',
    wicode:          payload.wicode   || null,
  });
  if (error) throw new Error(error.message);
  revalidatePath('/rewards');
}

export async function updateReward(id: string, raw: unknown) {
  employeeIdSchema.parse({ id }); // validate UUID
  const payload = rewardSchema.parse(raw);

  const db = createAdminClient();
  const { error } = await db.from('rewards').update({
    title:           payload.title,
    description:     payload.description || null,
    points_required: payload.points_required,
    hotel:           payload.hotels[0],
    hotels:          payload.hotels,
    stock:           payload.stock ?? null,
    image_url:       payload.image_url || null,
    category:        payload.category ?? 'hotel',
    wicode:          payload.wicode   || null,
  }).eq('id', id);
  if (error) throw new Error(error.message);
  revalidatePath('/rewards');
}

export async function deleteReward(id: string): Promise<{ error?: string }> {
  try {
    employeeIdSchema.parse({ id });

    const db = createAdminClient();

    // Block deletion if redemptions reference this reward (FK constraint).
    const { count } = await db
      .from('redemptions')
      .select('id', { count: 'exact', head: true })
      .eq('reward_id', id);

    if ((count ?? 0) > 0) {
      return {
        error: 'This reward has existing redemptions and cannot be deleted. Set stock to 0 to hide it instead.',
      };
    }

    const { error } = await db.from('rewards').delete().eq('id', id);
    if (error) return { error: error.message };

    revalidatePath('/rewards');
    return {};
  } catch (err: any) {
    return { error: err.message ?? 'Delete failed.' };
  }
}
