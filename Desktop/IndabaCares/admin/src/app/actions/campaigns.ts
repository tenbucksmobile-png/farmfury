'use server';

import { revalidatePath } from 'next/cache';
import { createAdminClient } from '@/lib/supabase/admin';
import { employeeIdSchema } from '@/lib/validation';

// ─── Banner upload helper ─────────────────────────────────────────────────────

async function uploadBanner(file: File): Promise<string> {
  const db = createAdminClient();
  const ext = file.name.split('.').pop() ?? 'jpg';
  const path = `banners/${Date.now()}.${ext}`;

  const arrayBuffer = await file.arrayBuffer();
  const { error } = await db.storage
    .from('campaign-media')
    .upload(path, Buffer.from(arrayBuffer), {
      contentType: file.type,
      upsert: true,
    });

  if (error) throw new Error(`Banner upload failed: ${error.message}`);
  return db.storage.from('campaign-media').getPublicUrl(path).data.publicUrl;
}

// ─── Storage browser ──────────────────────────────────────────────────────────

export async function listCampaignMediaFiles(prefix: string): Promise<
  { name: string; path: string; url: string; isFolder: boolean }[]
> {
  try {
    const db = createAdminClient();
    const { data, error } = await db.storage
      .from('campaign-media')
      .list(prefix || undefined, {
        limit: 300,
        sortBy: { column: 'name', order: 'asc' },
      });

    if (error || !data) return [];

    return data.map((item) => {
      const isFolder = item.metadata === null;
      const path     = prefix ? `${prefix}/${item.name}` : item.name;
      const url      = isFolder
        ? ''
        : db.storage.from('campaign-media').getPublicUrl(path).data.publicUrl;
      return { name: item.name, path, url, isFolder };
    });
  } catch {
    return [];
  }
}

// ─── Actions ──────────────────────────────────────────────────────────────────

export async function createCampaign(formData: FormData): Promise<{ error?: string }> {
  try {
    const db = createAdminClient();

    const type               = (formData.get('type')               as string) || 'recognition';
    const title              = ((formData.get('title')              as string) ?? '').trim();
    const description        = ((formData.get('description')        as string) ?? '').trim() || null;
    const points_multiplier  = parseInt(formData.get('points_multiplier') as string || '1', 10);
    const hotel              = (formData.get('hotel')               as string) ?? '';
    const start_date         = (formData.get('start_date')          as string) ?? '';
    const end_date           = (formData.get('end_date')            as string) ?? '';
    const sponsor_name       = ((formData.get('sponsor_name')       as string) ?? '').trim() || null;
    const banner_link_url    = ((formData.get('banner_link_url')    as string) ?? '').trim() || null;
    const voucher_description = ((formData.get('voucher_description') as string) ?? '').trim() || null;

    if (!title || title.length < 3) return { error: 'Title must be at least 3 characters.' };
    if (!hotel)      return { error: 'Hotel is required.' };
    if (!start_date) return { error: 'Start date is required.' };
    if (!end_date)   return { error: 'End date is required.' };
    if (end_date < start_date) return { error: 'End date must be on or after start date.' };

    // Banner: storage-picker URL takes priority, then file upload
    let banner_url: string | null = ((formData.get('banner_storage_url') as string) ?? '').trim() || null;
    const bannerFile = formData.get('banner_file') as File | null;
    if (!banner_url && bannerFile && bannerFile.size > 0) {
      banner_url = await uploadBanner(bannerFile);
    }

    const { error } = await db.from('campaigns').insert({
      type,
      title,
      description,
      points_multiplier,
      hotel,
      start_date,
      end_date,
      sponsor_name,
      banner_url,
      banner_link_url,
      voucher_description,
    });

    if (error) return { error: error.message };
    revalidatePath('/campaigns');
    return {};
  } catch (err: any) {
    return { error: err.message ?? 'Create failed.' };
  }
}

export async function updateCampaign(id: string, formData: FormData): Promise<{ error?: string }> {
  try {
    employeeIdSchema.parse({ id });

    const db = createAdminClient();

    const type               = (formData.get('type')               as string) || 'recognition';
    const title              = ((formData.get('title')              as string) ?? '').trim();
    const description        = ((formData.get('description')        as string) ?? '').trim() || null;
    const points_multiplier  = parseInt(formData.get('points_multiplier') as string || '1', 10);
    const hotel              = (formData.get('hotel')               as string) ?? '';
    const start_date         = (formData.get('start_date')          as string) ?? '';
    const end_date           = (formData.get('end_date')            as string) ?? '';
    const sponsor_name       = ((formData.get('sponsor_name')       as string) ?? '').trim() || null;
    const banner_link_url    = ((formData.get('banner_link_url')    as string) ?? '').trim() || null;
    const voucher_description = ((formData.get('voucher_description') as string) ?? '').trim() || null;

    if (!title || title.length < 3) return { error: 'Title must be at least 3 characters.' };
    if (!hotel)      return { error: 'Hotel is required.' };
    if (!start_date) return { error: 'Start date is required.' };
    if (!end_date)   return { error: 'End date is required.' };
    if (end_date < start_date) return { error: 'End date must be on or after start date.' };

    // Fetch existing to preserve banner if none provided
    const { data: current } = await db
      .from('campaigns')
      .select('banner_url')
      .eq('id', id)
      .single();

    let banner_url: string | null = ((formData.get('banner_storage_url') as string) ?? '').trim() || null;
    const bannerFile = formData.get('banner_file') as File | null;
    if (!banner_url && bannerFile && bannerFile.size > 0) {
      banner_url = await uploadBanner(bannerFile);
    }
    if (!banner_url) {
      banner_url = current?.banner_url ?? null;
    }

    const { error } = await db
      .from('campaigns')
      .update({
        type,
        title,
        description,
        points_multiplier,
        hotel,
        start_date,
        end_date,
        sponsor_name,
        banner_url,
        banner_link_url,
        voucher_description,
      })
      .eq('id', id);

    if (error) return { error: error.message };
    revalidatePath('/campaigns');
    return {};
  } catch (err: any) {
    return { error: err.message ?? 'Update failed.' };
  }
}

export async function deleteCampaign(id: string): Promise<{ error?: string }> {
  try {
    employeeIdSchema.parse({ id });
    const db = createAdminClient();
    const { error } = await db.from('campaigns').delete().eq('id', id);
    if (error) return { error: error.message };
    revalidatePath('/campaigns');
    return {};
  } catch (err: any) {
    return { error: err.message ?? 'Delete failed.' };
  }
}
