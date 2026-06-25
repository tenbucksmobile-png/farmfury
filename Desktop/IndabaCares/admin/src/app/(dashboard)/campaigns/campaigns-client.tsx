'use client';

import { useState, useTransition, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { Plus, Pencil, Trash2, Zap, Filter, Megaphone, ImageIcon, Upload, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input }  from '@/components/ui/input';
import { Label }  from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { StorageImagePicker } from '@/components/storage-image-picker';
import { HOTELS } from '@/lib/hotels';
import {
  createCampaign,
  updateCampaign,
  deleteCampaign,
  listCampaignMediaFiles,
} from '@/app/actions/campaigns';
import type { Campaign } from './page';

// ── Status helpers ─────────────────────────────────────────────────────────────

type CampaignStatus = 'active' | 'upcoming' | 'ended';

function getStatus(campaign: Campaign): CampaignStatus {
  const today = new Date().toISOString().slice(0, 10);
  if (campaign.end_date < today)   return 'ended';
  if (campaign.start_date > today) return 'upcoming';
  return 'active';
}

const STATUS_STYLES: Record<CampaignStatus, string> = {
  active:   'bg-emerald-100 text-emerald-700',
  upcoming: 'bg-blue-100    text-blue-700',
  ended:    'bg-slate-100   text-slate-500',
};

const STATUS_LABEL: Record<CampaignStatus, string> = {
  active:   'Active',
  upcoming: 'Upcoming',
  ended:    'Ended',
};

const TYPE_LABEL: Record<string, string> = {
  recognition: 'Recognition',
  sponsor:     'Sponsor Ad',
  both:        'Both',
};

const TYPE_STYLES: Record<string, string> = {
  recognition: 'bg-fuchsia-50  text-fuchsia-700',
  sponsor:     'bg-amber-50    text-amber-700',
  both:        'bg-violet-50   text-violet-700',
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
  });
}

// ── Campaign form dialog ───────────────────────────────────────────────────────

function CampaignDialog({
  open,
  editing,
  defaultHotel,
  onClose,
  onDone,
  saving,
  setSaving,
}: {
  open:         boolean;
  editing:      Campaign | null;
  defaultHotel: string;
  onClose:      () => void;
  onDone:       () => void;
  saving:       boolean;
  setSaving:    (v: boolean) => void;
}) {
  const formRef  = useRef<HTMLFormElement>(null);
  const [type,          setType]          = useState<string>(editing?.type ?? 'recognition');
  const [multiplier,    setMultiplier]    = useState<string>(String(editing?.points_multiplier ?? 2));
  const [hotel,         setHotel]         = useState<string>(editing?.hotel ?? defaultHotel);
  const [bannerUrl,     setBannerUrl]     = useState<string>(editing?.banner_url ?? '');
  const [formError,     setFormError]     = useState('');

  // Reset when dialog opens for a new/different campaign
  const prevEditingId = useRef<string | null>(null);
  if (open && (editing?.id ?? null) !== prevEditingId.current) {
    prevEditingId.current = editing?.id ?? null;
    // Use setTimeout 0 to avoid setting state during render
  }

  function handleOpenChange(isOpen: boolean) {
    if (!isOpen) onClose();
  }

  const showSponsorFields = type === 'sponsor' || type === 'both';
  const showMultiplier    = type === 'recognition' || type === 'both';

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setFormError('');

    const formData = new FormData(e.currentTarget);
    // Inject controlled values that aren't plain inputs
    formData.set('type',              type);
    formData.set('points_multiplier', multiplier);
    formData.set('hotel',             hotel);
    if (bannerUrl) {
      formData.set('banner_storage_url', bannerUrl);
    }

    setSaving(true);
    const result = editing
      ? await updateCampaign(editing.id, formData)
      : await createCampaign(formData);
    setSaving(false);

    if (result.error) {
      setFormError(result.error);
    } else {
      toast.success(editing ? 'Campaign updated.' : 'Campaign created.');
      onDone();
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editing ? 'Edit Campaign' : 'New Campaign'}</DialogTitle>
        </DialogHeader>

        <form ref={formRef} onSubmit={handleSubmit} className="space-y-5 py-2">

          {/* Campaign type */}
          <div className="space-y-1">
            <Label>Campaign Type *</Label>
            <Select value={type} onValueChange={setType}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="recognition">
                  Recognition — points multiplier only
                </SelectItem>
                <SelectItem value="sponsor">
                  Sponsor Ad — banner advertisement only
                </SelectItem>
                <SelectItem value="both">
                  Both — multiplier + sponsor banner
                </SelectItem>
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              {type === 'recognition' && 'Boosts points earned during this period.'}
              {type === 'sponsor'     && 'Displays a sponsor banner in the mobile app. No points multiplier.'}
              {type === 'both'        && 'Combines a points multiplier with a sponsor banner.'}
            </p>
          </div>

          {/* Title */}
          <div className="space-y-1">
            <Label>Title *</Label>
            <Input
              name="title"
              placeholder="e.g. Customer Service Week"
              defaultValue={editing?.title ?? ''}
              required
            />
          </div>

          {/* Description */}
          <div className="space-y-1">
            <Label>Description <span className="text-muted-foreground">(optional)</span></Label>
            <Input
              name="description"
              placeholder="Brief description shown to employees"
              defaultValue={editing?.description ?? ''}
            />
          </div>

          {/* Hotel */}
          <div className="space-y-1">
            <Label>Hotel *</Label>
            <Select value={hotel} onValueChange={setHotel}>
              <SelectTrigger>
                <SelectValue placeholder="Select hotel" />
              </SelectTrigger>
              <SelectContent>
                {HOTELS.map((h) => (
                  <SelectItem key={h} value={h}>{h}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Multiplier (recognition / both only) */}
          {showMultiplier && (
            <div className="space-y-1">
              <Label>Points Multiplier</Label>
              <Select value={multiplier} onValueChange={setMultiplier}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {[2, 3, 4, 5].map((m) => (
                    <SelectItem key={m} value={String(m)}>
                      {m}× — {10 * m} pts per recognition (base: 10)
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Employees earn {Number(multiplier) * 10} pts per recognition during this campaign.
              </p>
            </div>
          )}

          {/* ── Sponsor fields ──────────────────────────────────────────────── */}
          {showSponsorFields && (
            <div className="rounded-lg border border-amber-200 bg-amber-50/40 p-4 space-y-4">
              <p className="text-sm font-semibold text-amber-700 flex items-center gap-1.5">
                <Megaphone className="h-4 w-4" />
                Sponsor Details
              </p>

              {/* Sponsor name */}
              <div className="space-y-1">
                <Label>Sponsor Name *</Label>
                <Input
                  name="sponsor_name"
                  placeholder="e.g. Coca-Cola"
                  defaultValue={editing?.sponsor_name ?? ''}
                  required={showSponsorFields}
                />
                <p className="text-xs text-muted-foreground">Displayed below the banner in the app.</p>
              </div>

              {/* Banner image */}
              <div className="space-y-1">
                <div className="flex items-center justify-between">
                  <Label>Banner Image</Label>
                  <StorageImagePicker
                    onSelect={(urls) => setBannerUrl(urls[0] ?? '')}
                    multiple={false}
                    label="Browse campaign-media"
                    bucketLabel="campaign-media bucket"
                    listFn={listCampaignMediaFiles}
                  />
                </div>

                {/* Current / picked banner preview */}
                {bannerUrl ? (
                  <div className="relative w-full overflow-hidden rounded-md border">
                    <img src={bannerUrl} alt="banner preview" className="w-full object-cover max-h-32" />
                    <button
                      type="button"
                      onClick={() => setBannerUrl('')}
                      className="absolute right-1 top-1 rounded-full bg-black/60 p-0.5 text-white hover:bg-black/80"
                    >
                      <X className="h-3.5 w-3.5" />
                    </button>
                  </div>
                ) : editing?.banner_url ? (
                  <p className="text-xs text-muted-foreground truncate">
                    Current: <a href={editing.banner_url} target="_blank" rel="noopener noreferrer" className="underline">{editing.banner_url.split('/').pop()}</a>
                  </p>
                ) : null}

                <Input
                  type="file"
                  name="banner_file"
                  accept="image/jpeg,image/jpg,image/png,image/webp"
                />
                <p className="text-xs text-muted-foreground">Upload a new file or browse the bucket above. Max 10 MB.</p>
              </div>

              {/* Banner link URL */}
              <div className="space-y-1">
                <Label>Banner Link URL <span className="text-muted-foreground">(optional)</span></Label>
                <Input
                  name="banner_link_url"
                  type="url"
                  placeholder="https://sponsor.com/offer"
                  defaultValue={editing?.banner_link_url ?? ''}
                />
                <p className="text-xs text-muted-foreground">Opened when an employee taps the banner.</p>
              </div>

              {/* Voucher description */}
              <div className="space-y-1">
                <Label>Voucher / Reward Description</Label>
                <Textarea
                  name="voucher_description"
                  placeholder="Describe the voucher or reward the sponsor is providing…"
                  defaultValue={editing?.voucher_description ?? ''}
                  rows={3}
                />
              </div>
            </div>
          )}

          {/* Dates */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label>Start Date *</Label>
              <Input
                name="start_date"
                type="date"
                defaultValue={editing?.start_date ?? ''}
                required
              />
            </div>
            <div className="space-y-1">
              <Label>End Date *</Label>
              <Input
                name="end_date"
                type="date"
                defaultValue={editing?.end_date ?? ''}
                required
              />
            </div>
          </div>

          {formError && (
            <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>
              Cancel
            </Button>
            <Button type="submit" disabled={saving} className="gap-1">
              <Upload className="h-4 w-4" />
              {saving ? 'Saving…' : editing ? 'Save Campaign' : 'Create Campaign'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ── Main component ─────────────────────────────────────────────────────────────

export function CampaignsClient({
  campaigns,
  selectedHotel,
}: {
  campaigns:     Campaign[];
  selectedHotel?: string;
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [saving,    setSaving]       = useState(false);

  const [dialogOpen,   setDialogOpen]   = useState(false);
  const [editTarget,   setEditTarget]   = useState<Campaign | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Campaign | null>(null);

  // ── Filters ──────────────────────────────────────────────────────────────────

  function handleHotelFilter(val: string) {
    const url = new URLSearchParams();
    if (val !== 'all') url.set('hotel', val);
    router.push(`/campaigns${url.toString() ? '?' + url.toString() : ''}`);
  }

  // ── CRUD ─────────────────────────────────────────────────────────────────────

  function openCreate() {
    setEditTarget(null);
    setDialogOpen(true);
  }

  function openEdit(c: Campaign) {
    setEditTarget(c);
    setDialogOpen(true);
  }

  function handleDialogDone() {
    setDialogOpen(false);
    setEditTarget(null);
    router.refresh();
  }

  function handleDelete() {
    if (!deleteTarget) return;
    startTransition(async () => {
      try {
        const result = await deleteCampaign(deleteTarget.id);
        if (result.error) {
          toast.error(result.error);
        } else {
          toast.success('Campaign deleted.');
          setDeleteTarget(null);
          router.refresh();
        }
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  // ── Group by status ───────────────────────────────────────────────────────────

  const active   = campaigns.filter((c) => getStatus(c) === 'active');
  const upcoming = campaigns.filter((c) => getStatus(c) === 'upcoming');
  const ended    = campaigns.filter((c) => getStatus(c) === 'ended');

  return (
    <>
      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-3">
        <Select value={selectedHotel ?? 'all'} onValueChange={handleHotelFilter}>
          <SelectTrigger className="w-52">
            <Filter className="mr-2 h-4 w-4" />
            <SelectValue placeholder="All hotels" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Hotels</SelectItem>
            {HOTELS.map((h) => (
              <SelectItem key={h} value={h}>{h}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Button className="ml-auto" onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" />
          New Campaign
        </Button>
      </div>

      {/* Campaign lists */}
      {campaigns.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-dashed py-16 text-center text-muted-foreground">
          <Zap className="mb-3 h-8 w-8 opacity-40" />
          <p className="font-medium">No campaigns yet</p>
          <p className="text-sm">Create a recognition campaign to boost points, or a sponsor ad campaign.</p>
        </div>
      ) : (
        <div className="space-y-8">
          {[
            { label: 'Active',   items: active   },
            { label: 'Upcoming', items: upcoming },
            { label: 'Ended',    items: ended    },
          ].map(({ label, items }) =>
            items.length === 0 ? null : (
              <section key={label}>
                <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                  {label} ({items.length})
                </h2>
                <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                  {items.map((c) => {
                    const status = getStatus(c);
                    return (
                      <div
                        key={c.id}
                        className="relative flex flex-col gap-3 rounded-lg border bg-card overflow-hidden"
                      >
                        {/* Banner image */}
                        {c.banner_url && (
                          <div className="relative h-28 w-full overflow-hidden bg-muted">
                            <img
                              src={c.banner_url}
                              alt={c.sponsor_name ?? 'sponsor banner'}
                              className="h-full w-full object-cover"
                            />
                            {c.sponsor_name && (
                              <span className="absolute bottom-1 left-2 rounded bg-black/60 px-1.5 py-0.5 text-[11px] text-white">
                                {c.sponsor_name}
                              </span>
                            )}
                          </div>
                        )}

                        <div className="p-4 pt-2 flex flex-col gap-3">
                          {/* Badges row */}
                          <div className="flex items-start justify-between gap-2">
                            <div className="flex gap-1.5 flex-wrap">
                              <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-semibold ${STATUS_STYLES[status]}`}>
                                {STATUS_LABEL[status]}
                              </span>
                              <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-semibold ${TYPE_STYLES[c.type ?? 'recognition']}`}>
                                {TYPE_LABEL[c.type ?? 'recognition']}
                              </span>
                            </div>

                            <div className="flex shrink-0 gap-1">
                              <Button
                                variant="ghost"
                                size="icon"
                                className="h-7 w-7"
                                onClick={() => openEdit(c)}
                                disabled={isPending}
                              >
                                <Pencil className="h-3.5 w-3.5" />
                              </Button>
                              <Button
                                variant="ghost"
                                size="icon"
                                className="h-7 w-7 text-red-500 hover:text-red-600"
                                onClick={() => setDeleteTarget(c)}
                                disabled={isPending}
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
                            </div>
                          </div>

                          {/* Title + description */}
                          <div>
                            <h3 className="font-semibold leading-tight">{c.title}</h3>
                            {c.description && (
                              <p className="mt-0.5 text-sm text-muted-foreground line-clamp-2">
                                {c.description}
                              </p>
                            )}
                          </div>

                          {/* Multiplier pill (recognition / both) */}
                          {(c.type === 'recognition' || c.type === 'both') && (
                            <div className="flex items-center gap-1.5">
                              <span className="flex items-center gap-1 rounded-md bg-fuchsia-50 px-2 py-1 text-sm font-bold text-fuchsia-700">
                                <Zap className="h-3.5 w-3.5" />
                                {c.points_multiplier}× points
                              </span>
                              <span className="text-xs text-muted-foreground">
                                = {c.points_multiplier * 10} pts per recognition
                              </span>
                            </div>
                          )}

                          {/* Voucher description (sponsor) */}
                          {c.voucher_description && (
                            <p className="text-xs text-amber-700 bg-amber-50 rounded px-2 py-1 line-clamp-2">
                              {c.voucher_description}
                            </p>
                          )}

                          {/* Hotel + dates */}
                          <div className="space-y-0.5 text-xs text-muted-foreground">
                            <p className="font-medium text-foreground">{c.hotel}</p>
                            <p>
                              {formatDate(c.start_date)} — {formatDate(c.end_date)}
                            </p>
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </section>
            ),
          )}
        </div>
      )}

      {/* Create / Edit dialog */}
      <CampaignDialog
        open={dialogOpen}
        editing={editTarget}
        defaultHotel={selectedHotel ?? ''}
        onClose={() => { setDialogOpen(false); setEditTarget(null); }}
        onDone={handleDialogDone}
        saving={saving}
        setSaving={setSaving}
      />

      {/* Delete confirmation */}
      <AlertDialog open={!!deleteTarget} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete campaign?</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{deleteTarget?.title}</strong> will be permanently deleted.
              Any bonus points already awarded will not be reversed.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isPending}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-red-600 hover:bg-red-700"
              onClick={handleDelete}
              disabled={isPending}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
