'use server';

import { revalidatePath } from 'next/cache';
import { createAdminClient } from '@/lib/supabase/admin';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface InitiativeRow {
  id:         string;
  hotel:      string;
  tab:        string;
  mascot_url: string | null;
  image_urls: string[];
  video_url:  string | null;
  sort_order: number;
  created_at: string;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Upload a single File to the initiative-media bucket, return public URL. */
async function uploadFile(file: File, path: string): Promise<string> {
  const db = createAdminClient();
  const arrayBuffer = await file.arrayBuffer();
  const buffer = Buffer.from(arrayBuffer);

  const { error } = await db.storage
    .from('initiative-media')
    .upload(path, buffer, {
      contentType: file.type,
      upsert: true,
    });

  if (error) throw new Error(`Upload failed: ${error.message}`);

  const { data } = db.storage.from('initiative-media').getPublicUrl(path);
  return data.publicUrl;
}

function storagePath(hotel: string, tab: string, filename: string) {
  // Sanitise for storage path: lowercase, spaces → dashes
  const h = hotel.toLowerCase().replace(/\s+/g, '-');
  const t = tab.toLowerCase().replace(/\s+/g, '-');
  return `${h}/${t}/${filename}`;
}

// ─── Actions ──────────────────────────────────────────────────────────────────

// ─── Storage browser ──────────────────────────────────────────────────────────

/**
 * Lists files and folders at a given prefix inside the initiative-media bucket.
 * Folders are items whose `metadata` field is null (Supabase storage convention).
 */
export async function listStorageFiles(prefix: string): Promise<
  { name: string; path: string; url: string; isFolder: boolean }[]
> {
  try {
    const db = createAdminClient();
    const { data, error } = await db.storage
      .from('initiative-media')
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
        : db.storage.from('initiative-media').getPublicUrl(path).data.publicUrl;
      return { name: item.name, path, url, isFolder };
    });
  } catch {
    return [];
  }
}

// ─── Actions ──────────────────────────────────────────────────────────────────

export async function createInitiative(formData: FormData): Promise<{ error?: string }> {
  try {
    const hotel      = formData.get('hotel')      as string;
    const tab        = formData.get('tab')        as string;
    const sortOrder  = parseInt(formData.get('sort_order') as string || '0', 10);
    const mascotFile = formData.get('mascot')     as File | null;
    const videoFile  = formData.get('video')      as File | null;
    const galleryFiles = formData.getAll('gallery') as File[];

    // Storage-picker selections (URLs already in the bucket)
    const mascotStorageUrl   = (formData.get('mascot_storage_url')    as string) || null;
    const galleryStorageJson = (formData.get('gallery_storage_urls')  as string) || '[]';
    const galleryStorageUrls: string[] = JSON.parse(galleryStorageJson);

    if (!hotel || !tab) return { error: 'Hotel and initiative name are required.' };

    const db = createAdminClient();
    const ts = Date.now();

    // Mascot: storage pick takes priority, then file upload
    let mascot_url: string | null = mascotStorageUrl;
    if (!mascot_url && mascotFile && mascotFile.size > 0) {
      mascot_url = await uploadFile(
        mascotFile,
        storagePath(hotel, tab, `mascot-${ts}.${mascotFile.name.split('.').pop()}`),
      );
    }

    // Video: file upload only
    let video_url: string | null = null;
    if (videoFile && videoFile.size > 0) {
      video_url = await uploadFile(
        videoFile,
        storagePath(hotel, tab, `video-${ts}.${videoFile.name.split('.').pop()}`),
      );
    }

    // Gallery: storage picks first, then any newly uploaded files appended
    const image_urls: string[] = [...galleryStorageUrls];
    for (let i = 0; i < galleryFiles.length; i++) {
      const f = galleryFiles[i];
      if (f && f.size > 0) {
        const url = await uploadFile(
          f,
          storagePath(hotel, tab, `gallery-${ts}-${i}.${f.name.split('.').pop()}`),
        );
        image_urls.push(url);
      }
    }

    const { error } = await db.from('initiatives').insert({
      hotel,
      tab,
      mascot_url,
      image_urls,
      video_url,
      sort_order: sortOrder,
    });

    if (error) return { error: error.message };

    revalidatePath('/initiatives');
    return {};
  } catch (err: any) {
    return { error: err.message ?? 'Create failed.' };
  }
}

export async function updateInitiative(id: string, formData: FormData): Promise<{ error?: string }> {
  try {
    const hotel      = formData.get('hotel')      as string;
    const tab        = formData.get('tab')        as string;
    const sortOrder  = parseInt(formData.get('sort_order') as string || '0', 10);
    const mascotFile = formData.get('mascot')     as File | null;
    const videoFile  = formData.get('video')      as File | null;
    const galleryFiles = formData.getAll('gallery') as File[];

    // Storage-picker selections
    const mascotStorageUrl   = (formData.get('mascot_storage_url')   as string) || null;
    const galleryStorageJson = (formData.get('gallery_storage_urls') as string) || '[]';
    const galleryStorageUrls: string[] = JSON.parse(galleryStorageJson);

    if (!hotel || !tab) return { error: 'Hotel and initiative name are required.' };

    const db = createAdminClient();
    const ts = Date.now();

    // Fetch current row to preserve existing URLs when no new file provided
    const { data: current } = await db
      .from('initiatives')
      .select('mascot_url, video_url, image_urls')
      .eq('id', id)
      .single();

    // Mascot: storage pick > new upload > keep existing
    let mascot_url = mascotStorageUrl ?? current?.mascot_url ?? null;
    if (!mascotStorageUrl && mascotFile && mascotFile.size > 0) {
      mascot_url = await uploadFile(
        mascotFile,
        storagePath(hotel, tab, `mascot-${ts}.${mascotFile.name.split('.').pop()}`),
      );
    }

    let video_url = current?.video_url ?? null;
    if (videoFile && videoFile.size > 0) {
      video_url = await uploadFile(
        videoFile,
        storagePath(hotel, tab, `video-${ts}.${videoFile.name.split('.').pop()}`),
      );
    }

    // Gallery: existing + storage picks + new uploads
    const image_urls: string[] = [
      ...(current?.image_urls ?? []),
      ...galleryStorageUrls,
    ];
    for (let i = 0; i < galleryFiles.length; i++) {
      const f = galleryFiles[i];
      if (f && f.size > 0) {
        const url = await uploadFile(
          f,
          storagePath(hotel, tab, `gallery-${ts}-${i}.${f.name.split('.').pop()}`),
        );
        image_urls.push(url);
      }
    }

    const { error } = await db.from('initiatives').update({
      hotel,
      tab,
      mascot_url,
      image_urls,
      video_url,
      sort_order: sortOrder,
    }).eq('id', id);

    if (error) return { error: error.message };

    revalidatePath('/initiatives');
    return {};
  } catch (err: any) {
    return { error: err.message ?? 'Update failed.' };
  }
}

export async function deleteInitiative(id: string): Promise<{ error?: string }> {
  try {
    const db = createAdminClient();
    const { error } = await db.from('initiatives').delete().eq('id', id);
    if (error) return { error: error.message };
    revalidatePath('/initiatives');
    return {};
  } catch (err: any) {
    return { error: err.message ?? 'Delete failed.' };
  }
}

export async function removeGalleryImage(id: string, imageUrl: string): Promise<{ error?: string }> {
  try {
    const db = createAdminClient();
    const { data: row } = await db
      .from('initiatives')
      .select('image_urls')
      .eq('id', id)
      .single();

    const updated = (row?.image_urls ?? []).filter((u: string) => u !== imageUrl);

    const { error } = await db.from('initiatives').update({ image_urls: updated }).eq('id', id);
    if (error) return { error: error.message };

    revalidatePath('/initiatives');
    return {};
  } catch (err: any) {
    return { error: err.message ?? 'Remove failed.' };
  }
}
