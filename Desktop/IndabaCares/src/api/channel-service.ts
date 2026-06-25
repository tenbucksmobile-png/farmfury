import { supabase } from '@/lib/supabase';

// ─── Types ────────────────────────────────────────────────────────────────────

export type PostType = 'photo' | 'video' | 'text';

export interface ChannelPost {
  id:            string;
  hotel:         string;
  post_type:     PostType;
  media_url:     string | null;
  thumbnail_url: string | null;
  caption:       string | null;
  created_at:    string;
}

// ─── Queries ──────────────────────────────────────────────────────────────────

const PAGE_SIZE = 15;

/**
 * Fetch published channel posts for a hotel, newest first.
 * cursor: ISO timestamp of the oldest item on the last page (for keyset pagination).
 */
export async function getChannelPosts(
  hotel:   string,
  cursor?: string,
): Promise<ChannelPost[]> {
  let q = supabase
    .from('channel_posts')
    .select('id, hotel, post_type, media_url, thumbnail_url, caption, created_at')
    .eq('hotel', hotel)
    .eq('is_published', true)
    .order('created_at', { ascending: false })
    .limit(PAGE_SIZE);

  if (cursor) {
    q = q.lt('created_at', cursor);
  }

  const { data, error } = await q;
  if (error) throw new Error(error.message);
  return (data ?? []) as ChannelPost[];
}
