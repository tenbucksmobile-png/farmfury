'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { Megaphone, Send, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { HOTELS } from '@/lib/hotels';
import { sendAnnouncement } from '@/app/actions/notifications';

interface Announcement {
  id:         string;
  title:      string;
  message:    string | null;
  hotel:      string;
  created_at: string;
}

function timeAgo(iso: string) {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.floor(diff / 60_000);
  if (m < 1)  return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
}

export function NotificationsClient({
  announcements,
  hotelCounts,
}: {
  announcements: Announcement[];
  hotelCounts:   Record<string, number>;
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  const [hotel,   setHotel]   = useState('');
  const [title,   setTitle]   = useState('');
  const [message, setMessage] = useState('');
  const [sent,    setSent]    = useState<number | null>(null);

  const recipientCount = hotel ? (hotelCounts[hotel] ?? 0) : 0;

  function handleSend() {
    if (!hotel || !title.trim() || !message.trim()) {
      toast.error('Please fill in all fields — hotel, title, and message.');
      return;
    }

    startTransition(async () => {
      try {
        const count = await sendAnnouncement({ hotel, title, message });
        setSent(count);
        toast.success(`Announcement sent to ${count} employee${count !== 1 ? 's' : ''}`);
        setTitle('');
        setMessage('');
        router.refresh();
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  return (
    <div className="grid gap-6 lg:grid-cols-5">
      {/* Compose panel */}
      <div className="lg:col-span-2 space-y-5">
        <Card>
          <CardHeader className="border-b pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <Megaphone className="h-4 w-4 text-violet-600" />
              Compose Announcement
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4 pt-4">
            {/* Hotel */}
            <div className="space-y-1">
              <Label>Hotel *</Label>
              <Select value={hotel} onValueChange={setHotel}>
                <SelectTrigger>
                  <SelectValue placeholder="Select hotel…" />
                </SelectTrigger>
                <SelectContent>
                  {HOTELS.map((h) => (
                    <SelectItem key={h} value={h}>
                      <span>{h}</span>
                      {hotelCounts[h] != null && (
                        <span className="ml-2 text-muted-foreground">
                          ({hotelCounts[h]} active)
                        </span>
                      )}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Recipient preview */}
            {hotel && (
              <div className="flex items-center gap-2 rounded-lg bg-violet-50 px-3 py-2 text-sm text-violet-700">
                <Users className="h-4 w-4" />
                <span>
                  {recipientCount} active employee{recipientCount !== 1 ? 's' : ''} will receive this
                </span>
              </div>
            )}

            {/* Title */}
            <div className="space-y-1">
              <Label>Title *</Label>
              <Input
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Important update from management"
                maxLength={120}
              />
              <p className="text-right text-[10px] text-muted-foreground">
                {title.length}/120
              </p>
            </div>

            {/* Message */}
            <div className="space-y-1">
              <Label>Message *</Label>
              <Textarea
                value={message}
                onChange={(e) => setMessage(e.target.value)}
                placeholder="Enter your announcement here…"
                rows={5}
                maxLength={1000}
              />
              <p className="text-right text-[10px] text-muted-foreground">
                {message.length}/1000
              </p>
            </div>

            {/* Preview */}
            {(title || message) && (
              <div className="rounded-lg border bg-muted/40 p-3">
                <p className="mb-0.5 text-[10px] font-semibold uppercase text-muted-foreground">
                  Preview
                </p>
                <p className="text-sm font-semibold">{title || '—'}</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  {message || '—'}
                </p>
              </div>
            )}

            <Button
              className="w-full"
              onClick={handleSend}
              disabled={isPending || !hotel || !title.trim() || !message.trim()}
            >
              {isPending ? (
                'Sending…'
              ) : (
                <>
                  <Send className="mr-2 h-4 w-4" />
                  Send to {recipientCount || '?'} employee{recipientCount !== 1 ? 's' : ''}
                </>
              )}
            </Button>

            {sent !== null && (
              <p className="text-center text-sm font-medium text-emerald-600">
                ✓ Sent to {sent} employee{sent !== 1 ? 's' : ''}
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* History panel */}
      <div className="lg:col-span-3">
        <Card>
          <CardHeader className="border-b pb-3">
            <CardTitle className="text-base">Recent Announcements</CardTitle>
          </CardHeader>
          <CardContent className="pt-4">
            {announcements.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No announcements sent yet.
              </p>
            ) : (
              <ul className="space-y-3">
                {announcements.map((a) => (
                  <li
                    key={a.id}
                    className="rounded-lg border bg-card p-4 text-sm"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0 flex-1">
                        <p className="font-semibold">{a.title}</p>
                        {a.message && (
                          <p className="mt-0.5 text-muted-foreground line-clamp-2">
                            {a.message}
                          </p>
                        )}
                      </div>
                      <span className="shrink-0 text-xs text-muted-foreground">
                        {timeAgo(a.created_at)}
                      </span>
                    </div>
                    <div className="mt-2 flex items-center gap-2">
                      <span className="rounded-full bg-violet-100 px-2 py-0.5 text-[10px] font-semibold text-violet-700">
                        {a.hotel}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
