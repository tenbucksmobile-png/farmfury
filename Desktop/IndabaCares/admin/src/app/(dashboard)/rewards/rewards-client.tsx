'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { Plus, Pencil, Trash2, Filter, Package, Hotel, ShoppingBag } from 'lucide-react';
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
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { HOTELS } from '@/lib/hotels';
import { createReward, updateReward, deleteReward } from '@/app/actions/rewards';

type Category = 'hotel' | 'retail';

interface Reward {
  id:              string;
  title:           string;
  description:     string | null;
  points_required: number;
  hotel:           string;
  hotels:          string[];
  stock:           number | null;
  image_url:       string | null;
  category:        Category;
  wicode:          string | null;
  created_at:      string;
}

interface RewardForm {
  title:           string;
  description:     string;
  points_required: string;
  hotels:          string[];
  stock:           string;
  image_url:       string;
  wicode:          string;
}

const EMPTY: RewardForm = {
  title: '', description: '', points_required: '', hotels: [], stock: '', image_url: '', wicode: '',
};

// ── Tab button ─────────────────────────────────────────────────────────────────

function TabBtn({
  active,
  onClick,
  icon: Icon,
  label,
  count,
}: {
  active:  boolean;
  onClick: () => void;
  icon:    React.ElementType;
  label:   string;
  count:   number;
}) {
  return (
    <button
      onClick={onClick}
      className={`flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition-colors ${
        active
          ? 'bg-violet-700 text-white shadow'
          : 'bg-muted text-muted-foreground hover:bg-muted/80'
      }`}
    >
      <Icon className="h-4 w-4" />
      {label}
      <span className={`rounded-full px-1.5 py-0.5 text-xs ${active ? 'bg-white/20' : 'bg-background'}`}>
        {count}
      </span>
    </button>
  );
}

// ── Reward card ────────────────────────────────────────────────────────────────

function RewardCard({
  r,
  category,
  onEdit,
  onDelete,
}: {
  r:        Reward;
  category: Category;
  onEdit:   (r: Reward) => void;
  onDelete: (id: string) => void;
}) {
  return (
    <Card className="flex flex-col">
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <CardTitle className="text-base leading-snug">{r.title}</CardTitle>
          <span className="shrink-0 rounded-full bg-violet-100 px-2 py-0.5 text-xs font-bold text-violet-700">
            {r.points_required} pts
          </span>
        </div>
        <p className="text-xs text-muted-foreground">
          {(r.hotels?.length ? r.hotels : [r.hotel]).join(' · ')}
        </p>
      </CardHeader>

      <CardContent className="flex-1 space-y-2 text-sm text-muted-foreground">
        <p>{r.description ?? <span className="italic opacity-50">No description</span>}</p>
        {category === 'retail' && r.wicode && (
          <div className="flex items-center gap-1.5 rounded-md border border-dashed border-violet-300 bg-violet-50 px-2 py-1">
            <span className="text-[10px] font-semibold uppercase tracking-wide text-violet-500">WiCode</span>
            <span className="font-mono text-xs font-bold text-violet-800">{r.wicode}</span>
          </div>
        )}
        {category === 'retail' && !r.wicode && (
          <div className="flex items-center gap-1.5 rounded-md border border-dashed border-amber-300 bg-amber-50 px-2 py-1">
            <span className="text-[10px] font-semibold text-amber-600">⚠ WiCode not set</span>
          </div>
        )}
      </CardContent>

      <CardFooter className="flex items-center justify-between border-t pt-3">
        <span className="text-xs text-muted-foreground">
          Stock: {r.stock != null ? r.stock : '∞'}
        </span>
        <div className="flex gap-1">
          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => onEdit(r)}>
            <Pencil className="h-3.5 w-3.5" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-destructive hover:text-destructive"
            onClick={() => onDelete(r.id)}
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        </div>
      </CardFooter>
    </Card>
  );
}

// ── Main component ─────────────────────────────────────────────────────────────

export function RewardsClient({
  rewards,
  selectedHotel,
}: {
  rewards:       Reward[];
  selectedHotel?: string;
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [activeTab,   setActiveTab]  = useState<Category>('hotel');
  const [form,        setForm]       = useState<RewardForm>(EMPTY);
  const [editId,      setEditId]     = useState<string | null>(null);
  const [dialogOpen,  setDialogOpen] = useState(false);
  const [deleteId,    setDeleteId]   = useState<string | null>(null);

  const hotelRewards     = rewards.filter((r) => r.category === 'hotel');
  const retailRewards    = rewards.filter((r) => r.category === 'retail');
  const displayedRewards = activeTab === 'hotel' ? hotelRewards : retailRewards;

  function handleHotelFilter(val: string) {
    const url = new URLSearchParams();
    if (val !== 'all') url.set('hotel', val);
    router.push(`/rewards${url.toString() ? '?' + url.toString() : ''}`);
  }

  function openCreate() {
    setForm({ ...EMPTY, hotels: selectedHotel ? [selectedHotel] : [] });
    setEditId(null);
    setDialogOpen(true);
  }

  function openEdit(r: Reward) {
    setForm({
      title:           r.title,
      description:     r.description ?? '',
      points_required: String(r.points_required),
      hotels:          r.hotels?.length ? r.hotels : [r.hotel],
      stock:           r.stock != null ? String(r.stock) : '',
      image_url:       r.image_url ?? '',
      wicode:          r.wicode    ?? '',
    });
    setEditId(r.id);
    setDialogOpen(true);
  }

  function handleSave() {
    const payload = {
      title:           form.title.trim(),
      description:     form.description.trim() || undefined,
      points_required: Number(form.points_required),
      hotels:          form.hotels,
      stock:           form.stock !== '' ? Number(form.stock) : undefined,
      image_url:       form.image_url.trim() || undefined,
      category:        activeTab,
      wicode:          form.wicode.trim() || undefined,
    };

    if (!payload.title || !payload.hotels.length || !payload.points_required) {
      toast.error('Title, at least one hotel, and points are required.');
      return;
    }

    startTransition(async () => {
      try {
        if (editId) {
          await updateReward(editId, payload);
          toast.success('Reward updated');
        } else {
          await createReward(payload);
          toast.success('Reward created');
        }
        setDialogOpen(false);
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  function handleDelete() {
    if (!deleteId) return;
    startTransition(async () => {
      const result = await deleteReward(deleteId);
      if (result.error) {
        toast.error(result.error);
      } else {
        toast.success('Reward deleted');
      }
      setDeleteId(null);
    });
  }

  return (
    <>
      {/* Tabs + toolbar */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex gap-2">
          <TabBtn
            active={activeTab === 'hotel'}
            onClick={() => setActiveTab('hotel')}
            icon={Hotel}
            label="Hotel Rewards"
            count={hotelRewards.length}
          />
          <TabBtn
            active={activeTab === 'retail'}
            onClick={() => setActiveTab('retail')}
            icon={ShoppingBag}
            label="Marketplace"
            count={retailRewards.length}
          />
        </div>

        <div className="flex-1" />

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

        <Button onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" />
          New {activeTab === 'hotel' ? 'Hotel Reward' : 'Marketplace Reward'}
        </Button>
      </div>

      {/* Grid */}
      {displayedRewards.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-dashed py-20 text-muted-foreground">
          <Package className="mb-3 h-10 w-10 opacity-40" />
          <p className="font-medium">No {activeTab === 'hotel' ? 'hotel rewards' : 'marketplace rewards'} yet</p>
          <p className="text-sm">Click "New {activeTab === 'hotel' ? 'Hotel Reward' : 'Marketplace Reward'}" to get started.</p>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {displayedRewards.map((r) => (
            <RewardCard
              key={r.id}
              r={r}
              category={activeTab}
              onEdit={openEdit}
              onDelete={setDeleteId}
            />
          ))}
        </div>
      )}

      {/* Create / Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {editId ? 'Edit' : 'New'} {activeTab === 'hotel' ? 'Hotel Reward' : 'Marketplace Reward'}
            </DialogTitle>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-1">
              <Label>Title *</Label>
              <Input
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                placeholder={activeTab === 'hotel' ? 'Weekend spa voucher' : 'R100 Woolworths voucher'}
              />
            </div>

            <div className="space-y-1">
              <Label>Description</Label>
              <Textarea
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                rows={2}
                placeholder="Optional details…"
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1">
                <Label>Points Required *</Label>
                <Input
                  type="number"
                  min={1}
                  value={form.points_required}
                  onChange={(e) => setForm({ ...form, points_required: e.target.value })}
                  placeholder="100"
                />
              </div>
              <div className="space-y-1">
                <Label>Stock (blank = unlimited)</Label>
                <Input
                  type="number"
                  min={0}
                  value={form.stock}
                  onChange={(e) => setForm({ ...form, stock: e.target.value })}
                  placeholder="∞"
                />
              </div>
            </div>

            <div className="space-y-1">
              <Label>Hotels * <span className="text-muted-foreground font-normal">(select all that apply)</span></Label>
              <div className="rounded-md border p-3 space-y-2 max-h-48 overflow-y-auto">
                {HOTELS.map((h) => {
                  const checked = form.hotels.includes(h);
                  return (
                    <label key={h} className="flex items-center gap-2.5 cursor-pointer select-none">
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() =>
                          setForm({
                            ...form,
                            hotels: checked
                              ? form.hotels.filter((x) => x !== h)
                              : [...form.hotels, h],
                          })
                        }
                        className="h-4 w-4 rounded border-gray-300 accent-violet-700"
                      />
                      <span className="text-sm">{h}</span>
                    </label>
                  );
                })}
              </div>
              {form.hotels.length === 0 && (
                <p className="text-xs text-destructive">Select at least one hotel.</p>
              )}
            </div>

            <div className="space-y-1">
              <Label>Image URL</Label>
              <Input
                value={form.image_url}
                onChange={(e) => setForm({ ...form, image_url: e.target.value })}
                placeholder="https://…"
              />
            </div>

            {activeTab === 'retail' && (
              <div className="space-y-1">
                <Label>WiCode</Label>
                <Input
                  value={form.wicode}
                  onChange={(e) => setForm({ ...form, wicode: e.target.value })}
                  placeholder="Enter WiCode…"
                  className="font-mono"
                />
                <p className="text-xs text-muted-foreground">
                  The WiCode used to redeem this marketplace reward at point of sale.
                </p>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleSave} disabled={isPending}>
              {isPending ? 'Saving…' : editId ? 'Save Changes' : 'Create Reward'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation */}
      <AlertDialog open={!!deleteId} onOpenChange={() => setDeleteId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete reward?</AlertDialogTitle>
            <AlertDialogDescription>
              This cannot be undone. Redemptions that reference this reward will remain.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
