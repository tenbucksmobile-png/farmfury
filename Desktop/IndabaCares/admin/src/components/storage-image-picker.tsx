'use client';

import { useState, useCallback } from 'react';
import { Check, ChevronRight, FolderOpen, ImageIcon, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { listStorageFiles } from '@/app/actions/initiatives';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface StorageEntry {
  name: string;
  path: string;
  url:  string;
  isFolder: boolean;
}

interface Props {
  /** Called with the selected public URL(s) when the user clicks "Use selected". */
  onSelect:  (urls: string[]) => void;
  /** Allow picking more than one image (for gallery). Default false. */
  multiple?: boolean;
  /** Button label. */
  label?:    string;
  /** Override the default bucket title shown in the dialog header. */
  bucketLabel?: string;
  /** Override the default listStorageFiles action (e.g. for a different bucket). */
  listFn?: (prefix: string) => Promise<StorageEntry[]>;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function StorageImagePicker({
  onSelect,
  multiple    = false,
  label       = 'Browse library',
  bucketLabel = 'initiative-media bucket',
  listFn      = listStorageFiles,
}: Props) {
  const [open,     setOpen]     = useState(false);
  const [prefix,   setPrefix]   = useState('');
  const [entries,  setEntries]  = useState<StorageEntry[]>([]);
  const [loading,  setLoading]  = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  // ── Load a folder ──────────────────────────────────────────────────────────

  const loadFolder = useCallback(async (p: string) => {
    setLoading(true);
    setPrefix(p);
    const files = await listFn(p);
    setEntries(files);
    setLoading(false);
  }, [listFn]);

  // ── Open ───────────────────────────────────────────────────────────────────

  function handleOpen() {
    setSelected(new Set());
    setEntries([]);
    setOpen(true);
    loadFolder('');
  }

  // ── Selection ──────────────────────────────────────────────────────────────

  function toggleSelect(url: string) {
    setSelected(prev => {
      const next = new Set(prev);
      if (multiple) {
        if (next.has(url)) next.delete(url); else next.add(url);
      } else {
        next.clear();
        next.add(url);
      }
      return next;
    });
  }

  // ── Confirm ────────────────────────────────────────────────────────────────

  function handleConfirm() {
    onSelect(Array.from(selected));
    setOpen(false);
  }

  // ── Breadcrumbs ────────────────────────────────────────────────────────────

  const crumbs = prefix ? prefix.split('/') : [];

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <>
      <button
        type="button"
        onClick={handleOpen}
        className="text-xs text-violet-600 hover:underline focus:outline-none"
      >
        {label}
      </button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="flex max-h-[80vh] max-w-2xl flex-col gap-0 p-0">
          <DialogHeader className="border-b px-4 py-3">
            <DialogTitle className="text-base">{bucketLabel}</DialogTitle>
          </DialogHeader>

          {/* Breadcrumb */}
          <div className="flex items-center gap-1 border-b bg-muted/30 px-4 py-2 text-xs text-muted-foreground">
            <button
              type="button"
              onClick={() => loadFolder('')}
              className="hover:text-foreground"
            >
              Root
            </button>
            {crumbs.map((crumb, i) => (
              <span key={i} className="flex items-center gap-1">
                <ChevronRight className="h-3 w-3" />
                <button
                  type="button"
                  onClick={() => loadFolder(crumbs.slice(0, i + 1).join('/'))}
                  className="hover:text-foreground"
                >
                  {crumb}
                </button>
              </span>
            ))}
          </div>

          {/* Grid */}
          <div className="flex-1 overflow-y-auto p-3">
            {loading ? (
              <div className="flex h-48 items-center justify-center">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            ) : entries.length === 0 ? (
              <div className="flex h-48 flex-col items-center justify-center gap-2 text-sm text-muted-foreground">
                <ImageIcon className="h-10 w-10 opacity-30" />
                <p>No files in this folder.</p>
              </div>
            ) : (
              <div className="grid grid-cols-4 gap-2 sm:grid-cols-5">
                {entries.map((entry) =>
                  entry.isFolder ? (
                    <button
                      key={entry.path}
                      type="button"
                      onClick={() => loadFolder(entry.path)}
                      className="flex flex-col items-center gap-1.5 rounded-lg border bg-card p-2 hover:bg-accent"
                    >
                      <FolderOpen className="h-10 w-10 text-amber-400" />
                      <span className="w-full truncate text-center text-[11px]">
                        {entry.name}
                      </span>
                    </button>
                  ) : (
                    <button
                      key={entry.url}
                      type="button"
                      onClick={() => toggleSelect(entry.url)}
                      className={`group relative aspect-square overflow-hidden rounded-lg border-2 transition-colors ${
                        selected.has(entry.url)
                          ? 'border-violet-600'
                          : 'border-transparent hover:border-violet-300'
                      }`}
                    >
                      <img
                        src={entry.url}
                        alt={entry.name}
                        className="h-full w-full object-cover"
                        loading="lazy"
                      />
                      {selected.has(entry.url) && (
                        <div className="absolute inset-0 flex items-center justify-center bg-violet-600/30">
                          <Check className="h-7 w-7 drop-shadow text-white" />
                        </div>
                      )}
                      <p className="absolute bottom-0 left-0 right-0 truncate bg-black/50 px-1 py-0.5 text-[9px] text-white">
                        {entry.name}
                      </p>
                    </button>
                  )
                )}
              </div>
            )}
          </div>

          {/* Footer */}
          <div className="flex items-center justify-between border-t px-4 py-3">
            <span className="text-xs text-muted-foreground">
              {multiple
                ? `${selected.size} selected — hold Ctrl/⌘ not needed, just click`
                : selected.size === 1
                  ? '1 image selected'
                  : 'Click an image to select it'}
            </span>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setOpen(false)}
              >
                Cancel
              </Button>
              <Button
                type="button"
                size="sm"
                disabled={selected.size === 0}
                onClick={handleConfirm}
              >
                Use selected ({selected.size})
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
