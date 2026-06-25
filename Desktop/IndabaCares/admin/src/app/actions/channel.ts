'use server';

import { revalidatePath } from 'next/cache';
import { z } from 'zod';
import { createAdminClient } from '@/lib/supabase/admin';
import { createServerSupabaseClient } from '@/lib/supabase/server';
import { redirect } from 'next/navigation';

// ─── Auth context ─────────────────────────────────────────────────────────────

export async function getChannelAdminContext() {
  const sb = await createServerSupabaseClient();
  const { data: { user } } = await sb.auth.getUser();
  if (!user) redirect('/login');

  const meta = (user.user_metadata ?? {}) as Record<string, unknown>;
  return {
    userId:       user.id,
    isSuperAdmin: !!meta.is_super_admin,
    hotel:        (meta.hotel as string) ?? null,
  };
}

// ─── Create post ─────────────────────────────────────────────────────────────

const createSchema = z.object({
  hotel:         z.string().min(1),
  post_type:     z.enum(['photo', 'video', 'text']),
  media_url:     z.string().url().nullable().optional(),
  media_path:    z.string().nullable().optional(),
  thumbnail_url: z.string().url().nullable().optional(),
  caption:       z.string().max(2000).nullable().optional(),
});

export async function createChannelPost(raw: unknown): Promise<void> {
  const sb = await createServerSupabaseClient();
  const { data: { user } } = await sb.auth.getUser();
  if (!user) throw new Error('Unauthorized');

  const payload = createSchema.parse(raw);
  const db = createAdminClient();

  const { error } = await db.from('channel_posts').insert({
    hotel:         payload.hotel,
    post_type:     payload.post_type,
    media_url:     payload.media_url    ?? null,
    media_path:    payload.media_path   ?? null,
    thumbnail_url: payload.thumbnail_url ?? null,
    caption:       payload.caption      ?? null,
    created_by:    user.id,
  });

  if (error) throw new Error(error.message);
  revalidatePath('/channel');
}

// ─── Delete post ──────────────────────────────────────────────────────────────

export async function deleteChannelPost(id: string, mediaPath: string | null): Promise<void> {
  const sb = await createServerSupabaseClient();
  const { data: { user } } = await sb.auth.getUser();
  if (!user) throw new Error('Unauthorized');

  const db = createAdminClient();

  // Remove storage object first (non-fatal if missing)
  if (mediaPath) {
    await db.storage.from('channel-media').remove([mediaPath]);
  }

  const { error } = await db.from('channel_posts').delete().eq('id', id);
  if (error) throw new Error(error.message);
  revalidatePath('/channel');
}

// ─── Invite channel admin ─────────────────────────────────────────────────────

export async function inviteChannelAdmin(email: string, hotel: string): Promise<void> {
  const sb = await createServerSupabaseClient();
  const { data: { user: caller } } = await sb.auth.getUser();
  if (!caller) throw new Error('Unauthorized');

  const callerMeta = (caller.user_metadata ?? {}) as Record<string, unknown>;
  if (!callerMeta.is_super_admin) throw new Error('Only super admins can invite channel admins.');

  const db = createAdminClient();
  const { error } = await db.auth.admin.inviteUserByEmail(email, {
    data: { hotel },
    redirectTo: `${process.env.NEXT_PUBLIC_SITE_URL ?? ''}/auth/callback`,
  });

  if (error) throw new Error(error.message);
}

// ─── List channel admins ──────────────────────────────────────────────────────

export async function getChannelAdmins(): Promise<{ id: string; email: string; hotel: string }[]> {
  const db = createAdminClient();
  const { data, error } = await db.auth.admin.listUsers();
  if (error) throw new Error(error.message);

  return (data.users ?? [])
    .filter((u) => {
      const meta = (u.user_metadata ?? {}) as Record<string, unknown>;
      return !!meta.hotel && !meta.is_super_admin;
    })
    .map((u) => {
      const meta = u.user_metadata as Record<string, unknown>;
      return {
        id:    u.id,
        email: u.email ?? '',
        hotel: meta.hotel as string,
      };
    });
}

// ─── Remove channel admin hotel assignment ────────────────────────────────────

export async function removeChannelAdmin(userId: string): Promise<void> {
  const sb = await createServerSupabaseClient();
  const { data: { user: caller } } = await sb.auth.getUser();
  if (!caller) throw new Error('Unauthorized');

  const callerMeta = (caller.user_metadata ?? {}) as Record<string, unknown>;
  if (!callerMeta.is_super_admin) throw new Error('Only super admins can remove channel admins.');

  const db = createAdminClient();
  const { error } = await db.auth.admin.updateUserById(userId, {
    user_metadata: { hotel: null },
  });
  if (error) throw new Error(error.message);
}

// ─── Fetch posts ──────────────────────────────────────────────────────────────

export async function getPostsForHotel(hotel: string) {
  const db = createAdminClient();
  const { data, error } = await db
    .from('channel_posts')
    .select('id, hotel, post_type, media_url, media_path, thumbnail_url, caption, created_at, is_published')
    .eq('hotel', hotel)
    .order('created_at', { ascending: false })
    .limit(60);

  if (error) throw new Error(error.message);
  return data ?? [];
}
