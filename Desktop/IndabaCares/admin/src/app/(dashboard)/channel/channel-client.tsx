'use client';

import { useState, useTransition, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import {
  ImageIcon, Film, Type, Trash2, Plus, Loader2, Rss, UserPlus, X,
} from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button }   from '@/components/ui/button';
import { Label }    from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel,
  AlertDialogContent, AlertDialogDescription,
  AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { createClient } from '@/lib/supabase/client';
import { createChannelPost, deleteChannelPost, inviteChannelAdmin, removeChannelAdmin } from '@/app/actions/channel';

// ─── Types ────────────────────────────────────────────────────────────────────

type PostType = 'photo' | 'video' | 'text';

interface ChannelPost {
  id:            string;
  hotel:         string;
  post_type:     PostType;
  media_url:     string | null;
  media_path:    string | null;
  thumbnail_url: string | null;
  caption:       string | null;
  created_at:    string;
  is_published:  boolean;
}

interface ChannelAdmin {
  id:    string;
  email: string;
  hotel: string;
}

interface Props {
  initialPosts:  ChannelPost[];
  activeHotel:   string;
  isSuperAdmin:  boolean;
  channelHotels: string[];
  channelAdmins: ChannelAdmin[];
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function timeAgo(iso: string) {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.floor(diff / 60_000);
  if (m < 1)  return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
}

function fileExt(name: string) {
  return name.split('.').pop()?.toLowerCase() ?? 'bin';
}

const POST_TYPE_ICONS: Record<PostType, React.ElementType> = {
  photo: ImageIcon,
  video: Film,
  text:  Type,
};

// ─── Component ────────────────────────────────────────────────────────────────

export function ChannelClient({
  initialPosts,
  activeHotel,
  isSuperAdmin,
  channelHotels,
  channelAdmins,
}: Props) {
  const router  = useRouter();
  const [isPending, startTransition] = useTransition();

  // ── Compose state ──────────────────────────────────────────────────────────
  const [postType,      setPostType]      = useState<PostType>('photo');
  const [caption,       setCaption]       = useState('');
  const [mediaFile,     setMediaFile]     = useState<File | null>(null);
  const [thumbFile,     setThumbFile]     = useState<File | null>(null);
  const [uploading,     setUploading]     = useState(false);
  const mediaInputRef = useRef<HTMLInputElement>(null);
  const thumbInputRef = useRef<HTMLInputElement>(null);

  // ── Delete confirmation ────────────────────────────────────────────────────
  const [confirmPost, setConfirmPost] = useState<ChannelPost | null>(null);

  // ── Invite admin state (super admin only) ──────────────────────────────────
  const [inviteEmail,  setInviteEmail]  = useState('');
  const [inviteHotel,  setInviteHotel]  = useState(channelHotels[0] ?? '');
  const [inviting,     setInviting]     = useState(false);
  const [removeTarget, setRemoveTarget] = useState<ChannelAdmin | null>(null);

  // ── Upload to Storage + create row ────────────────────────────────────────

  async function handlePublish() {
    if (postType !== 'text' && !mediaFile) {
      toast.error('Please select a file to upload.');
      return;
    }
    if (postType === 'text' && !caption.trim()) {
      toast.error('Text posts need a caption.');
      return;
    }

    setUploading(true);
    try {
      const supabase = createClient();
      let mediaUrl:     string | null = null;
      let mediaPath:    string | null = null;
      let thumbnailUrl: string | null = null;

      // Upload main media
      if (mediaFile) {
        const path = `${activeHotel}/${crypto.randomUUID()}.${fileExt(mediaFile.name)}`;
        const { data: up, error: upErr } = await supabase.storage
          .from('channel-media')
          .upload(path, mediaFile, { cacheControl: '3600', upsert: false });
        if (upErr) throw new Error(upErr.message);
        mediaPath = up.path;
        const { data: { publicUrl } } = supabase.storage
          .from('channel-media')
          .getPublicUrl(up.path);
        mediaUrl = publicUrl;
      }

      // Upload optional video thumbnail
      if (postType === 'video' && thumbFile) {
        const tPath = `${activeHotel}/thumb_${crypto.randomUUID()}.${fileExt(thumbFile.name)}`;
        const { data: tUp, error: tErr } = await supabase.storage
          .from('channel-media')
          .upload(tPath, thumbFile, { cacheControl: '3600', upsert: false });
        if (tErr) throw new Error(tErr.message);
        const { data: { publicUrl: tUrl } } = supabase.storage
          .from('channel-media')
          .getPublicUrl(tUp.path);
        thumbnailUrl = tUrl;
      }

      startTransition(async () => {
        try {
          await createChannelPost({
            hotel:         activeHotel,
            post_type:     postType,
            media_url:     mediaUrl,
            media_path:    mediaPath,
            thumbnail_url: thumbnailUrl,
            caption:       caption.trim() || null,
          });
          toast.success('Post published!');
          setCaption('');
          setMediaFile(null);
          setThumbFile(null);
          if (mediaInputRef.current) mediaInputRef.current.value = '';
          if (thumbInputRef.current) thumbInputRef.current.value = '';
          router.refresh();
        } catch (err: any) {
          toast.error(err.message);
        }
      });
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setUploading(false);
    }
  }

  function handleDelete(post: ChannelPost) {
    setConfirmPost(post);
  }

  function confirmDelete() {
    if (!confirmPost) return;
    const p = confirmPost;
    setConfirmPost(null);
    startTransition(async () => {
      try {
        await deleteChannelPost(p.id, p.media_path);
        toast.success('Post deleted.');
        router.refresh();
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  async function handleInvite() {
    if (!inviteEmail.includes('@') || !inviteHotel) {
      toast.error('Enter a valid email and select a hotel.');
      return;
    }
    setInviting(true);
    try {
      await inviteChannelAdmin(inviteEmail, inviteHotel);
      toast.success(`Invite sent to ${inviteEmail}`);
      setInviteEmail('');
      router.refresh();
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setInviting(false);
    }
  }

  function confirmRemoveAdmin() {
    if (!removeTarget) return;
    const t = removeTarget;
    setRemoveTarget(null);
    startTransition(async () => {
      try {
        await removeChannelAdmin(t.id);
        toast.success(`${t.email} removed as channel admin.`);
        router.refresh();
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  const isBusy = uploading || isPending;

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <>
      <div className="grid gap-6 lg:grid-cols-5">

        {/* ── Compose panel ─────────────────────────────────────────── */}
        <div className="lg:col-span-2 space-y-4">
          <Card>
            <CardHeader className="border-b pb-3">
              <CardTitle className="flex items-center gap-2 text-base">
                <Rss className="h-4 w-4 text-violet-600" />
                New Post
                {isSuperAdmin && (
                  <span className="ml-auto text-xs font-normal text-muted-foreground">
                    {activeHotel}
                  </span>
                )}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 pt-4">

              {/* Super admin hotel switcher */}
              {isSuperAdmin && (
                <div className="space-y-1">
                  <Label>Hotel</Label>
                  <Select
                    value={activeHotel}
                    onValueChange={(h) =>
                      router.push(`/channel?hotel=${encodeURIComponent(h)}`)
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {channelHotels.map((h) => (
                        <SelectItem key={h} value={h}>{h}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}

              {/* Post type */}
              <div className="space-y-1">
                <Label>Post type</Label>
                <div className="flex gap-2">
                  {(['photo', 'video', 'text'] as PostType[]).map((t) => {
                    const Icon = POST_TYPE_ICONS[t];
                    return (
                      <button
                        key={t}
                        type="button"
                        onClick={() => { setPostType(t); setMediaFile(null); setThumbFile(null); }}
                        className={`flex flex-1 items-center justify-center gap-1.5 rounded-lg border py-2 text-sm font-medium transition-colors ${
                          postType === t
                            ? 'border-violet-600 bg-violet-50 text-violet-700'
                            : 'border-border text-muted-foreground hover:bg-accent'
                        }`}
                      >
                        <Icon className="h-3.5 w-3.5" />
                        {t.charAt(0).toUpperCase() + t.slice(1)}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* File input for photo */}
              {postType === 'photo' && (
                <div className="space-y-1">
                  <Label>Image</Label>
                  <input
                    ref={mediaInputRef}
                    type="file"
                    accept="image/jpeg,image/png,image/webp,image/gif"
                    className="w-full text-sm file:mr-3 file:rounded-lg file:border-0 file:bg-violet-50 file:px-3 file:py-1.5 file:text-xs file:font-semibold file:text-violet-700 hover:file:bg-violet-100"
                    onChange={(e) => setMediaFile(e.target.files?.[0] ?? null)}
                  />
                  {mediaFile && (
                    <p className="flex items-center gap-1 text-xs text-muted-foreground">
                      <ImageIcon className="h-3 w-3" /> {mediaFile.name}
                    </p>
                  )}
                </div>
              )}

              {/* File inputs for video */}
              {postType === 'video' && (
                <>
                  <div className="space-y-1">
                    <Label>Video file</Label>
                    <input
                      ref={mediaInputRef}
                      type="file"
                      accept="video/mp4,video/quicktime,video/webm"
                      className="w-full text-sm file:mr-3 file:rounded-lg file:border-0 file:bg-violet-50 file:px-3 file:py-1.5 file:text-xs file:font-semibold file:text-violet-700 hover:file:bg-violet-100"
                      onChange={(e) => setMediaFile(e.target.files?.[0] ?? null)}
                    />
                    {mediaFile && (
                      <p className="flex items-center gap-1 text-xs text-muted-foreground">
                        <Film className="h-3 w-3" /> {mediaFile.name}
                      </p>
                    )}
                  </div>
                  <div className="space-y-1">
                    <Label>
                      Thumbnail image{' '}
                      <span className="text-muted-foreground font-normal">(optional)</span>
                    </Label>
                    <input
                      ref={thumbInputRef}
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      className="w-full text-sm file:mr-3 file:rounded-lg file:border-0 file:bg-violet-50 file:px-3 file:py-1.5 file:text-xs file:font-semibold file:text-violet-700 hover:file:bg-violet-100"
                      onChange={(e) => setThumbFile(e.target.files?.[0] ?? null)}
                    />
                  </div>
                </>
              )}

              {/* Caption */}
              <div className="space-y-1">
                <Label>
                  Caption
                  {postType !== 'text' && (
                    <span className="ml-1 text-muted-foreground font-normal">(optional)</span>
                  )}
                </Label>
                <Textarea
                  value={caption}
                  onChange={(e) => setCaption(e.target.value)}
                  placeholder={postType === 'text' ? 'Write your message…' : 'Add a caption…'}
                  rows={4}
                  maxLength={2000}
                />
                <p className="text-right text-[10px] text-muted-foreground">
                  {caption.length}/2000
                </p>
              </div>

              <Button
                className="w-full"
                onClick={handlePublish}
                disabled={isBusy}
              >
                {isBusy ? (
                  <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Publishing…</>
                ) : (
                  <><Plus className="mr-2 h-4 w-4" /> Publish post</>
                )}
              </Button>
            </CardContent>
          </Card>
        </div>

        {/* ── Posts list ────────────────────────────────────────────── */}
        <div className="lg:col-span-3">
          <Card>
            <CardHeader className="border-b pb-3">
              <CardTitle className="text-base">
                {activeHotel} — {initialPosts.length} post{initialPosts.length !== 1 ? 's' : ''}
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4">
              {initialPosts.length === 0 ? (
                <div className="flex flex-col items-center gap-2 py-12 text-sm text-muted-foreground">
                  <Rss className="h-10 w-10 opacity-25" />
                  <p>No posts yet. Publish the first one!</p>
                </div>
              ) : (
                <ul className="space-y-3">
                  {initialPosts.map((post) => {
                    const Icon = POST_TYPE_ICONS[post.post_type];
                    return (
                      <li
                        key={post.id}
                        className="flex items-start gap-3 rounded-lg border bg-card p-3"
                      >
                        {/* Thumbnail / type icon */}
                        <div className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-muted">
                          {post.media_url && post.post_type !== 'text' ? (
                            // eslint-disable-next-line @next/next/no-img-element
                            <img
                              src={post.post_type === 'video' && post.thumbnail_url
                                ? post.thumbnail_url
                                : post.media_url}
                              alt=""
                              className="h-full w-full object-cover"
                            />
                          ) : (
                            <Icon className="h-6 w-6 text-muted-foreground" />
                          )}
                        </div>

                        {/* Content */}
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-2">
                            <span className="rounded-full bg-violet-100 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-violet-700">
                              {post.post_type}
                            </span>
                            {!post.is_published && (
                              <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold text-amber-700">
                                Hidden
                              </span>
                            )}
                            <span className="ml-auto text-xs text-muted-foreground">
                              {timeAgo(post.created_at)}
                            </span>
                          </div>
                          {post.caption && (
                            <p className="mt-1 line-clamp-2 text-sm text-foreground">
                              {post.caption}
                            </p>
                          )}
                        </div>

                        {/* Delete */}
                        <button
                          type="button"
                          onClick={() => handleDelete(post)}
                          disabled={isPending}
                          className="shrink-0 rounded-md p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                          title="Delete post"
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* ── Invite panel (super admin only) ───────────────────────────── */}
      {isSuperAdmin && (
        <div className="grid gap-6 lg:grid-cols-2">
          {/* Invite form */}
          <Card>
            <CardHeader className="border-b pb-3">
              <CardTitle className="flex items-center gap-2 text-base">
                <UserPlus className="h-4 w-4 text-violet-600" />
                Invite Channel Admin
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 pt-4">
              <p className="text-sm text-muted-foreground">
                Send a sign-up invite to a hotel staff member. They will only see the Channel page when they log in.
              </p>
              <div className="space-y-1">
                <Label>Email address</Label>
                <Input
                  type="email"
                  placeholder="admin@hotel.com"
                  value={inviteEmail}
                  onChange={(e) => setInviteEmail(e.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label>Hotel</Label>
                <Select value={inviteHotel} onValueChange={setInviteHotel}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {channelHotels.map((h) => (
                      <SelectItem key={h} value={h}>{h}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <Button
                className="w-full"
                onClick={handleInvite}
                disabled={inviting || !inviteEmail.includes('@')}
              >
                {inviting
                  ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Sending…</>
                  : <><UserPlus className="mr-2 h-4 w-4" /> Send invite</>}
              </Button>
            </CardContent>
          </Card>

          {/* Existing channel admins */}
          <Card>
            <CardHeader className="border-b pb-3">
              <CardTitle className="text-base">Channel Admins</CardTitle>
            </CardHeader>
            <CardContent className="pt-4">
              {channelAdmins.length === 0 ? (
                <p className="text-sm text-muted-foreground">No channel admins yet.</p>
              ) : (
                <ul className="space-y-2">
                  {channelAdmins.map((admin) => (
                    <li key={admin.id} className="flex items-center justify-between rounded-lg border bg-card px-3 py-2 text-sm">
                      <div>
                        <p className="font-medium">{admin.email}</p>
                        <p className="text-xs text-muted-foreground">{admin.hotel}</p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setRemoveTarget(admin)}
                        disabled={isPending}
                        className="rounded p-1 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                        title="Remove access"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      {/* Delete post confirmation */}
      <AlertDialog open={!!confirmPost} onOpenChange={(o) => !o && setConfirmPost(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete post?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently remove the post and its media file. This cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={confirmDelete}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      {/* Remove channel admin confirmation */}
      <AlertDialog open={!!removeTarget} onOpenChange={(o) => !o && setRemoveTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove channel admin?</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{removeTarget?.email}</strong> will lose access to the Channel page.
              Their account is not deleted — only the hotel assignment is removed.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={confirmRemoveAdmin}
            >
              Remove
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
