'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { DataTable } from '@/components/data-table/data-table';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { CheckCircle, XCircle, CircleCheck } from 'lucide-react';
import { formatDateTime, getInitials } from '@/lib/utils';
import {
  approveRedemption,
  rejectRedemption,
  fulfillRedemption,
} from '@/app/actions/redemptions';
import type { ColumnDef } from '@tanstack/react-table';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface RedemptionRow {
  id:               string;
  points_used:      number;
  status:           string;
  hotel:            string;
  rejection_reason: string | null;
  created_at:       string;
  approved_at:      string | null;
  rejected_at:      string | null;
  fulfilled_at:     string | null;
  employee: { id: string; full_name: string; photo_url: string | null; employee_code: string } | null;
  reward:   { id: string; title: string; image_url: string | null; points_required: number } | null;
}

// ── Status config ─────────────────────────────────────────────────────────────

const STATUS_META: Record<string, { label: string; color: string }> = {
  all:       { label: 'All',       color: '' },
  pending:   { label: 'Pending',   color: 'bg-amber-100 text-amber-800 border-amber-200' },
  approved:  { label: 'Approved',  color: 'bg-blue-100 text-blue-800 border-blue-200' },
  fulfilled: { label: 'Fulfilled', color: 'bg-green-100 text-green-800 border-green-200' },
  rejected:  { label: 'Rejected',  color: 'bg-red-100 text-red-800 border-red-200' },
};

const STATUS_TABS = ['all', 'pending', 'approved', 'fulfilled', 'rejected'];

// ── Component ─────────────────────────────────────────────────────────────────

export function RedemptionsClient({
  redemptions,
  total,
  status,
  page,
}: {
  redemptions: RedemptionRow[];
  total:       number;
  status:      string;
  page:        number;
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [rejectId,   setRejectId]   = useState<string | null>(null);
  const [rejectNote, setRejectNote] = useState('');

  function navigate(newStatus: string, newPage: number) {
    const params = new URLSearchParams();
    if (newStatus !== 'all') params.set('status', newStatus);
    if (newPage > 0)         params.set('page', String(newPage));
    router.push(`/rewards/redemptions${params.toString() ? '?' + params.toString() : ''}`);
  }

  function handleApprove(id: string) {
    startTransition(async () => {
      try {
        await approveRedemption(id);
        toast.success('Redemption approved');
      } catch (e: any) {
        toast.error(e.message);
      }
    });
  }

  function handleFulfill(id: string) {
    startTransition(async () => {
      try {
        await fulfillRedemption(id);
        toast.success('Redemption fulfilled');
      } catch (e: any) {
        toast.error(e.message);
      }
    });
  }

  function openReject(id: string) {
    setRejectId(id);
    setRejectNote('');
  }

  function confirmReject() {
    if (!rejectId) return;
    startTransition(async () => {
      try {
        await rejectRedemption(rejectId, rejectNote || undefined);
        toast.success('Redemption rejected & points refunded');
        setRejectId(null);
      } catch (e: any) {
        toast.error(e.message);
      }
    });
  }

  const columns: ColumnDef<RedemptionRow, unknown>[] = [
    {
      accessorKey: 'employee',
      header: 'Employee',
      cell: ({ row }) => {
        const e = row.original.employee;
        return e ? (
          <div className="flex items-center gap-2">
            <Avatar className="h-7 w-7">
              <AvatarImage src={e.photo_url ?? undefined} />
              <AvatarFallback className="text-xs">{getInitials(e.full_name)}</AvatarFallback>
            </Avatar>
            <div>
              <p className="text-sm font-medium leading-none">{e.full_name}</p>
              <p className="text-xs text-muted-foreground">{e.employee_code}</p>
            </div>
          </div>
        ) : '—';
      },
    },
    {
      accessorKey: 'reward',
      header: 'Reward',
      cell: ({ row }) => (
        <span className="text-sm font-medium">{row.original.reward?.title ?? '—'}</span>
      ),
    },
    {
      accessorKey: 'points_used',
      header: 'Points',
      cell: ({ row }) => (
        <Badge variant="secondary">{row.original.points_used} pts</Badge>
      ),
    },
    {
      accessorKey: 'hotel',
      header: 'Hotel',
      cell: ({ row }) => (
        <span className="text-xs text-muted-foreground">{row.original.hotel}</span>
      ),
    },
    {
      accessorKey: 'status',
      header: 'Status',
      cell: ({ row }) => {
        const meta = STATUS_META[row.original.status];
        return (
          <Badge className={meta?.color ?? ''} variant="outline">
            {meta?.label ?? row.original.status}
          </Badge>
        );
      },
    },
    {
      accessorKey: 'created_at',
      header: 'Requested',
      cell: ({ row }) => (
        <span className="text-xs text-muted-foreground">
          {formatDateTime(row.original.created_at)}
        </span>
      ),
    },
    {
      id: 'actions',
      cell: ({ row }) => {
        const s = row.original.status;
        return (
          <div className="flex items-center gap-1">
            {s === 'pending' && (
              <>
                <Button
                  variant="outline" size="sm"
                  onClick={() => handleApprove(row.original.id)}
                  disabled={isPending}
                >
                  <CheckCircle className="mr-1 h-3 w-3" />Approve
                </Button>
                <Button
                  variant="destructive" size="sm"
                  onClick={() => openReject(row.original.id)}
                  disabled={isPending}
                >
                  <XCircle className="mr-1 h-3 w-3" />Reject
                </Button>
              </>
            )}
            {s === 'approved' && (
              <Button
                variant="outline" size="sm"
                onClick={() => handleFulfill(row.original.id)}
                disabled={isPending}
              >
                <CircleCheck className="mr-1 h-3 w-3" />Fulfill
              </Button>
            )}
          </div>
        );
      },
    },
  ];

  return (
    <>
      <Tabs value={status} onValueChange={(v) => navigate(v, 0)}>
        <TabsList>
          {STATUS_TABS.map((s) => (
            <TabsTrigger key={s} value={s} className="capitalize">
              {STATUS_META[s]?.label ?? s}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      <DataTable
        columns={columns}
        data={redemptions}
        totalCount={total}
        page={page}
        onPageChange={(p) => navigate(status, p)}
        isLoading={false}
        emptyMessage="No redemptions found."
      />

      <Dialog open={!!rejectId} onOpenChange={(v) => !v && setRejectId(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject Redemption</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div>
              <Label>Reason (optional)</Label>
              <Textarea
                value={rejectNote}
                onChange={(e) => setRejectNote(e.target.value)}
                placeholder="Explain why this is being rejected…"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectId(null)}>Cancel</Button>
            <Button
              variant="destructive"
              onClick={confirmReject}
              disabled={isPending}
            >
              Reject &amp; Refund Points
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
