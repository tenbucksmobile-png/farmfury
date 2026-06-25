import { redirect } from 'next/navigation';
import { createServerSupabaseClient } from '@/lib/supabase/server';
import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { ChannelClient } from './channel-client';
import { getChannelAdmins } from '@/app/actions/channel';

export const dynamic = 'force-dynamic';

export const CHANNEL_HOTELS = ['Indaba Hotel', 'Chobe Safari Lodge'] as const;
export type ChannelHotel = (typeof CHANNEL_HOTELS)[number];

async function getAdminContext() {
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

async function fetchPosts(hotel: string) {
  const db = createAdminClient();
  const { data } = await db
    .from('channel_posts')
    .select('id, hotel, post_type, media_url, media_path, thumbnail_url, caption, created_at, is_published')
    .eq('hotel', hotel)
    .order('created_at', { ascending: false })
    .limit(60);
  return data ?? [];
}

export default async function ChannelPage({
  searchParams,
}: {
  searchParams: Promise<{ hotel?: string }>;
}) {
  const [ctx, params] = await Promise.all([getAdminContext(), searchParams]);

  // Determine which hotel to display
  let activeHotel: string | null = null;
  if (ctx.isSuperAdmin) {
    const requested = params.hotel;
    activeHotel = CHANNEL_HOTELS.includes(requested as ChannelHotel)
      ? requested!
      : CHANNEL_HOTELS[0];
  } else if (ctx.hotel && CHANNEL_HOTELS.includes(ctx.hotel as ChannelHotel)) {
    activeHotel = ctx.hotel;
  }

  if (!activeHotel) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Channel"
          description="Manage your hotel's channel posts."
        />
        <p className="rounded-lg border bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Your account is not assigned to a channel hotel. Ask a super admin to set{' '}
          <code className="font-mono">hotel</code> in your user metadata.
        </p>
      </div>
    );
  }

  const [posts, channelAdmins] = await Promise.all([
    fetchPosts(activeHotel),
    ctx.isSuperAdmin ? getChannelAdmins() : Promise.resolve([]),
  ]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Channel"
        description="Manage photo, video and text posts for your hotel's channel feed."
      />
      <ChannelClient
        initialPosts={posts as any[]}
        activeHotel={activeHotel}
        isSuperAdmin={ctx.isSuperAdmin}
        channelHotels={[...CHANNEL_HOTELS]}
        channelAdmins={channelAdmins}
      />
    </div>
  );
}
