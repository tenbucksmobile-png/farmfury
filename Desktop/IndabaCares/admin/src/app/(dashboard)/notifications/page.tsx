import { createAdminClient } from '@/lib/supabase/admin';
import { PageHeader } from '@/components/layout/page-header';
import { NotificationsClient } from './notifications-client';

export const dynamic = 'force-dynamic';

async function getRecentAnnouncements() {
  const db = createAdminClient();
  const { data } = await db
    .from('notifications')
    .select('id, title, message, hotel, created_at')
    .eq('type', 'admin_announcement')
    .order('created_at', { ascending: false })
    .limit(20);
  return data ?? [];
}

async function getHotelEmployeeCounts() {
  const db = createAdminClient();
  const { data } = await db
    .from('employees')
    .select('hotel')
    .eq('status', 'active');

  const counts: Record<string, number> = {};
  for (const row of data ?? []) {
    counts[row.hotel] = (counts[row.hotel] ?? 0) + 1;
  }
  return counts;
}

export default async function NotificationsPage() {
  const [announcements, hotelCounts] = await Promise.all([
    getRecentAnnouncements(),
    getHotelEmployeeCounts(),
  ]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Notifications"
        description="Send announcements to employees at any hotel. Each active employee receives an in-app notification."
      />
      <NotificationsClient
        announcements={announcements}
        hotelCounts={hotelCounts}
      />
    </div>
  );
}
