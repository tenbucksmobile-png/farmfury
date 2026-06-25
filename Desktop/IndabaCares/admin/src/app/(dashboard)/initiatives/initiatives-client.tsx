'use client';

import { useState, useTransition, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import {
  Plus, Pencil, Trash2, X, ImageIcon, Video, Upload,
} from 'lucide-react';
import { StorageImagePicker } from '@/components/storage-image-picker';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
  DialogFooter,
  DialogHeader,
  DialogTitle,
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
import { Badge } from '@/components/ui/badge';
import { HOTELS } from '@/lib/hotels';
import {
  createInitiative,
  updateInitiative,
  deleteInitiative,
  removeGalleryImage,
} from '@/app/actions/initiatives';

// ─── Types ────────────────────────────────────────────────────────────────────

interface Initiative {
  id:         string;
  hotel:      string;
  tab:        string;
  mascot_url: string | null;
  image_urls: string[];
  video_url:  string | null;
  sort_order: number;
  created_at: string;
}

interface Props {
  initiatives:   Initiative[];
  selectedHotel: string | undefined;
}

// ─── File input helper ────────────────────────────────────────────────────────

function FileInputRow({
  label,
  name,
  accept,
  multiple = false,
  currentUrl,
  hint,
  storagePreviewUrls,
  onStoragePick,
}: {
  label:      string;
  name:       string;
  accept:     string;
  multiple?:  boolean;
  currentUrl?: string | null;
  hint?:      string;
  /** URLs already selected from the storage picker (shown as previews). */
  storagePreviewUrls?: string[];
  /** Called when the user picks image(s) from the storage browser. */
  onStoragePick?: (urls: string[]) => void;
}) {
  const isImage = accept.startsWith('image/');

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between">
        <Label>{label}</Label>
        {isImage && onStoragePick && (
          <StorageImagePicker
            onSelect={onStoragePick}
            multiple={multiple}
            label="Browse library"
          />
        )}
      </div>

      {/* Storage-picked previews */}
      {storagePreviewUrls && storagePreviewUrls.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {storagePreviewUrls.map((url) => (
            <div key={url} className="relative h-14 w-14 overflow-hidden rounded border">
              <img src={url} alt="" className="h-full w-full object-cover" />
            </div>
          ))}
          <p className="w-full text-xs text-violet-600">
            {storagePreviewUrls.length} image{storagePreviewUrls.length !== 1 ? 's' : ''} selected from library
          </p>
        </div>
      )}

      {currentUrl && (
        <p className="text-xs text-muted-foreground truncate">
          Current: <a href={currentUrl} target="_blank" rel="noopener noreferrer" className="underline">{currentUrl.split('/').pop()}</a>
        </p>
      )}
      <Input type="file" name={name} accept={accept} multiple={multiple} />
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

// ─── Main component ───────────────────────────────────────────────────────────

export function InitiativesClient({ initiatives, selectedHotel }: Props) {
  const router       = useRouter();
  const [isPending, startTransition] = useTransition();

  const [dialogOpen,  setDialogOpen]  = useState(false);
  const [deleteOpen,  setDeleteOpen]  = useState(false);
  const [editing,     setEditing]     = useState<Initiative | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Initiative | null>(null);
  const [filterHotel, setFilterHotel] = useState(selectedHotel ?? '');
  const [formError,   setFormError]   = useState('');

  // Storage-picker selections (images already in the bucket)
  const [mascotStorageUrl,   setMascotStorageUrl]   = useState<string | null>(null);
  const [galleryStorageUrls, setGalleryStorageUrls] = useState<string[]>([]);

  const formRef = useRef<HTMLFormElement>(null);

  // ── Filter ────────────────────────────────────────────────────────────────

  function applyHotelFilter(hotel: string) {
    setFilterHotel(hotel);
    const params = new URLSearchParams();
    if (hotel) params.set('hotel', hotel);
    router.push(`/initiatives?${params.toString()}`);
  }

  // ── Group by hotel → tab ──────────────────────────────────────────────────

  const grouped: Record<string, Record<string, Initiative[]>> = {};
  for (const item of initiatives) {
    grouped[item.hotel] ??= {};
    grouped[item.hotel][item.tab] ??= [];
    grouped[item.hotel][item.tab].push(item);
  }

  // ── Dialog ────────────────────────────────────────────────────────────────

  function openCreate() {
    setEditing(null);
    setFormError('');
    setMascotStorageUrl(null);
    setGalleryStorageUrls([]);
    setDialogOpen(true);
  }

  function openEdit(item: Initiative) {
    setEditing(item);
    setFormError('');
    setMascotStorageUrl(null);
    setGalleryStorageUrls([]);
    setDialogOpen(true);
  }

  function openDelete(item: Initiative) {
    setDeleteTarget(item);
    setDeleteOpen(true);
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setFormError('');
    const formData = new FormData(e.currentTarget);

    // Append storage-picker selections as hidden fields
    if (mascotStorageUrl) {
      formData.set('mascot_storage_url', mascotStorageUrl);
    }
    if (galleryStorageUrls.length > 0) {
      formData.set('gallery_storage_urls', JSON.stringify(galleryStorageUrls));
    }

    startTransition(async () => {
      const result = editing
        ? await updateInitiative(editing.id, formData)
        : await createInitiative(formData);

      if (result.error) {
        setFormError(result.error);
      } else {
        toast.success(editing ? 'Initiative updated.' : 'Initiative created.');
        setDialogOpen(false);
        router.refresh();
      }
    });
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    startTransition(async () => {
      const result = await deleteInitiative(deleteTarget.id);
      if (result.error) {
        toast.error(result.error);
      } else {
        toast.success('Deleted.');
        setDeleteOpen(false);
        setDeleteTarget(null);
        router.refresh();
      }
    });
  }

  async function handleRemoveImage(item: Initiative, url: string) {
    startTransition(async () => {
      const result = await removeGalleryImage(item.id, url);
      if (result.error) {
        toast.error(result.error);
      } else {
        toast.success('Image removed.');
        router.refresh();
      }
    });
  }

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <>
      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-3">
        <Select value={filterHotel || '__all__'} onValueChange={(v) => applyHotelFilter(v === '__all__' ? '' : v)}>
          <SelectTrigger className="w-56">
            <SelectValue placeholder="All hotels" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">All hotels</SelectItem>
            {HOTELS.map((h) => <SelectItem key={h} value={h}>{h}</SelectItem>)}
          </SelectContent>
        </Select>

        <Button onClick={openCreate} className="ml-auto gap-1">
          <Plus className="h-4 w-4" />
          Add content block
        </Button>
      </div>

      {/* Content grouped by hotel → tab */}
      {Object.keys(grouped).length === 0 ? (
        <div className="rounded-lg border border-dashed py-20 text-center text-muted-foreground">
          No initiatives yet. Click <strong>Add content block</strong> to get started.
        </div>
      ) : (
        <div className="space-y-8">
          {Object.entries(grouped).map(([hotel, tabs]) => (
            <div key={hotel}>
              <h2 className="mb-3 text-base font-semibold text-foreground">{hotel}</h2>
              <div className="space-y-4">
                {Object.entries(tabs).map(([tab, blocks]) => (
                  <div key={tab} className="rounded-lg border bg-card p-4">
                    <div className="mb-3 flex items-center justify-between">
                      <span className="font-medium">{tab}</span>
                      <Badge variant="secondary">{blocks.length} block{blocks.length !== 1 ? 's' : ''}</Badge>
                    </div>

                    <div className="space-y-3">
                      {blocks.map((block) => (
                        <div
                          key={block.id}
                          className="flex items-start gap-4 rounded-md border bg-muted/30 p-3"
                        >
                          {/* Mascot thumbnail */}
                          <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-md border bg-background">
                            {block.mascot_url ? (
                              <img
                                src={block.mascot_url}
                                alt="mascot"
                                className="h-full w-full rounded-md object-contain"
                              />
                            ) : (
                              <ImageIcon className="h-6 w-6 text-muted-foreground" />
                            )}
                          </div>

                          {/* Details */}
                          <div className="flex-1 min-w-0 space-y-1">
                            <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
                              <span>Order: {block.sort_order}</span>
                              <span>·</span>
                              <span>{block.image_urls.length} gallery image{block.image_urls.length !== 1 ? 's' : ''}</span>
                              {block.video_url && (
                                <>
                                  <span>·</span>
                                  <span className="flex items-center gap-1">
                                    <Video className="h-3 w-3" /> video
                                  </span>
                                </>
                              )}
                            </div>

                            {/* Gallery thumbnails */}
                            {block.image_urls.length > 0 && (
                              <div className="flex flex-wrap gap-1 pt-1">
                                {block.image_urls.map((url) => (
                                  <div key={url} className="group relative h-10 w-10 overflow-hidden rounded border">
                                    <img src={url} alt="" className="h-full w-full object-cover" />
                                    <button
                                      onClick={() => handleRemoveImage(block, url)}
                                      disabled={isPending}
                                      className="absolute inset-0 flex items-center justify-center bg-black/50 opacity-0 transition-opacity group-hover:opacity-100"
                                    >
                                      <X className="h-3 w-3 text-white" />
                                    </button>
                                  </div>
                                ))}
                              </div>
                            )}
                          </div>

                          {/* Actions */}
                          <div className="flex shrink-0 gap-2">
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => openEdit(block)}
                            >
                              <Pencil className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="text-destructive hover:text-destructive"
                              onClick={() => openDelete(block)}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ── Create / Edit dialog ─────────────────────────────────────────────── */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit content block' : 'New content block'}</DialogTitle>
          </DialogHeader>

          <form ref={formRef} onSubmit={handleSubmit} className="space-y-5 py-2">

            {/* Hotel */}
            <div className="space-y-1">
              <Label>Hotel *</Label>
              <select
                name="hotel"
                defaultValue={editing?.hotel ?? filterHotel ?? ''}
                required
                className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              >
                <option value="" disabled>Select hotel…</option>
                {HOTELS.map((h) => <option key={h} value={h}>{h}</option>)}
              </select>
            </div>

            {/* Initiative name (tab) */}
            <div className="space-y-1">
              <Label>Initiative name *</Label>
              <Input
                name="tab"
                placeholder="e.g. Billy Says"
                defaultValue={editing?.tab ?? ''}
                required
              />
              <p className="text-xs text-muted-foreground">
                This is the display name shown in the mobile app. Blocks with the same name are grouped under one initiative.
              </p>
            </div>

            {/* Sort order */}
            <div className="space-y-1">
              <Label>Sort order</Label>
              <Input
                name="sort_order"
                type="number"
                min="0"
                defaultValue={editing?.sort_order ?? 0}
              />
            </div>

            {/* Mascot image */}
            <FileInputRow
              label="Mascot / hero image"
              name="mascot"
              accept="image/jpeg,image/jpg,image/png,image/webp"
              currentUrl={mascotStorageUrl ?? editing?.mascot_url}
              hint="Shown at the top of the initiative. Leave blank to keep existing."
              storagePreviewUrls={mascotStorageUrl ? [mascotStorageUrl] : []}
              onStoragePick={(urls) => setMascotStorageUrl(urls[0] ?? null)}
            />

            {/* Gallery images */}
            <FileInputRow
              label="Gallery photos"
              name="gallery"
              accept="image/jpeg,image/jpg,image/png,image/webp"
              multiple
              hint={
                editing && editing.image_urls.length > 0
                  ? `${editing.image_urls.length} existing photo(s). New uploads will be appended.`
                  : 'Select one or more photos for the gallery grid.'
              }
              storagePreviewUrls={galleryStorageUrls}
              onStoragePick={(urls) => setGalleryStorageUrls(prev => [...prev, ...urls])}
            />

            {/* Video */}
            <FileInputRow
              label="Video (optional)"
              name="video"
              accept="video/mp4,video/quicktime,video/x-m4v"
              currentUrl={editing?.video_url}
              hint="MP4 / MOV up to 50 MB. Leave blank to keep existing."
            />

            {formError && (
              <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {formError}
              </p>
            )}

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={isPending} className="gap-1">
                <Upload className="h-4 w-4" />
                {isPending ? 'Saving…' : editing ? 'Save changes' : 'Create'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Delete confirmation ──────────────────────────────────────────────── */}
      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete this content block?</AlertDialogTitle>
            <AlertDialogDescription>
              This removes the block from <strong>{deleteTarget?.hotel}</strong> —{' '}
              <strong>{deleteTarget?.tab}</strong>. Media files in Storage are not deleted.
              This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              disabled={isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {isPending ? 'Deleting…' : 'Delete'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
