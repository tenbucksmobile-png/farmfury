'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { CheckCircle, XCircle, Gift, Filter } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { HOTELS } from '@/lib/hotels';
import {
  approveRedemption,
  rejectRedemption,
  fulfillRedemption,
} from '@/app/actions/redemptions';

interface Redemption {
  id:               string;
  points_used:      number;
  status:           string;
  hotel:            string;
  created_at:       string;
  approved_at:      string | null;
  rejected_at:      string | null;
  fulfilled_at:     string | null;
  rejection_reason: string | null;
  employee: { id: string; full_name: string; employee_code: string; points_balance: number } | null;
  reward:   { id: string; title: string; points_required: number } | null;
}

const STATUS_BADGE: Record<string, string> = {
  pending:   'bg-amber-100 text-amber-700 border-amber-200',
  approved:  'bg-blue-100 text-blue-700 border-blue-200',
  fulfilled: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  rejected:  'bg-red-100 text-red-700 border-red-200',
};

const STATUSES = ['all', 'pending', 'approved', 'fulfilled', 'rejected'];

export function RedemptionsClient({
  redemptions,
  counts,
  selectedHotel,
  selectedStatus,
}: {
  redemptions:    Redemption[];
  counts:         Record<string, number>;
  selectedHotel?: string;
  selectedStatus?: string;
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [rejectId, setRejectId]   = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState('');

  function applyFilter(hotel?: string, status?: string) {
    const url = new URLSearchParams();
    if (hotel  && hotel  !== 'all') url.set('hotel',  hotel);
    if (status && status !== 'all') url.set('status', status);
    router.push(`/redemptions${url.toString() ? '?' + url.toString() : ''}`);
  }

  function doApprove(id: string) {
    startTransition(async () => {
      try {
        await approveRedemption(id);
        toast.success('Redemption approved');
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  function doFulfill(id: string) {
    startTransition(async () => {
      try {
        await fulfillRedemption(id);
        toast.success('Marked as fulfilled');
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  function doReject() {
    if (!rejectId) return;
    startTransition(async () => {
      try {
        await rejectRedemption(rejectId, rejectReason || undefined);
        toast.success('Redemption rejected — points refunded');
        setRejectId(null);
        setRejectReason('');
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  return (
    <>
      {/* Summary cards */}
      <div className="grid gap-3 sm:grid-cols-4">
        {(['pending', 'approved', 'fulfilled', 'rejected'] as const).map((s) => (
          <Card
            key={s}
            className={`cursor-pointer transition-shadow hover:shadow-md ${
              selectedStatus === s ? 'ring-2 ring-violet-500' : ''
            }`}
            onClick={() => applyFilter(selectedHotel, s)}
          >
            <CardContent className="flex items-center justify-between p-4">
              <span className="text-sm font-medium capitalize text-muted-foreground">{s}</span>
              <span className="text-2xl font-bold">{counts[s] ?? 0}</span>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <Select
          value={selectedHotel ?? 'all'}
          onValueChange={(v) => applyFilter(v, selectedStatus)}
        >
          <SelectTrigger className="w-52">
            <Filter className="mr-2 h-4 w-4" />
            <SelectValue placeholder="All hotels" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Hotels</SelectItem>
            {HOTELS.map((h) => <SelectItem key={h} value={h}>{h}</SelectItem>)}
          </SelectContent>
        </Select>

        <Select
          value={selectedStatus ?? 'all'}
          onValueChange={(v) => applyFilter(selectedHotel, v)}
        >
          <SelectTrigger className="w-40">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s} className="capitalize">{s}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Table */}
      <div className="rounded-lg border bg-card">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Employee</TableHead>
              <TableHead>Reward</TableHead>
              <TableHead>Hotel</TableHead>
              <TableHead className="text-right">Points</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Requested</TableHead>
              <TableHead>Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {redemptions.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} className="py-10 text-center text-muted-foreground">
                  No redemptions match the current filters.
                </TableCell>
              </TableRow>
            )}
            {redemptions.map((r) => (
              <TableRow key={r.id}>
                <TableCell>
                  <div className="font-medium">{r.employee?.full_name ?? '—'}</div>
                  <div className="text-xs text-muted-foreground font-mono">
                    {r.employee?.employee_code}
                  </div>
                </TableCell>
                <TableCell className="font-medium">{r.reward?.title ?? '—'}</TableCell>
                <TableCell className="text-sm text-muted-foreground">{r.hotel}</TableCell>
                <TableCell className="text-right font-semibold">{r.points_used}</TableCell>
                <TableCell>
                  <span
                    className={`rounded-full border px-2.5 py-0.5 text-xs font-semibold ${
                      STATUS_BADGE[r.status] ?? ''
                    }`}
                  >
                    {r.status}
                  </span>
                </TableCell>
                <TableCell className="text-sm text-muted-foreground">
                  {new Date(r.created_at).toLocaleDateString()}
                </TableCell>
                <TableCell>
                  <div className="flex items-center gap-1">
                    {r.status === 'pending' && (
                      <>
                        <Button
                          size="sm"
                          variant="ghost"
                          className="text-emerald-600 hover:text-emerald-700"
                          disabled={isPending}
                          onClick={() => doApprove(r.id)}
                        >
                          <CheckCircle className="mr-1 h-3.5 w-3.5" />
                          Approve
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          className="text-red-600 hover:text-red-700"
                          disabled={isPending}
                          onClick={() => { setRejectId(r.id); setRejectReason(''); }}
                        >
                          <XCircle className="mr-1 h-3.5 w-3.5" />
                          Reject
                        </Button>
                      </>
                    )}
                    {r.status === 'approved' && (
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-blue-600 hover:text-blue-700"
                        disabled={isPending}
                        onClick={() => doFulfill(r.id)}
                      >
                        <Gift className="mr-1 h-3.5 w-3.5" />
                        Fulfill
                      </Button>
                    )}
                    {r.status === 'rejected' && r.rejection_reason && (
                      <span className="text-xs text-muted-foreground italic">
                        {r.rejection_reason}
                      </span>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {/* Reject dialog */}
      <Dialog open={!!rejectId} onOpenChange={() => setRejectId(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject Redemption</DialogTitle>
            <DialogDescription>
              The employee's points will be refunded. Provide an optional reason.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Label>Reason (optional)</Label>
            <Input
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="e.g. Out of stock, item unavailable…"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectId(null)}>Cancel</Button>
            <Button
              variant="destructive"
              onClick={doReject}
              disabled={isPending}
            >
              {isPending ? 'Rejecting…' : 'Confirm Reject'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
