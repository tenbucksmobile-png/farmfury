'use client';

/**
 * CsvImportDialog — three-step employee import wizard.
 *
 * Step 1 — DROP:     Drag-and-drop or file picker. Shows template download link.
 * Step 2 — VALIDATE: Per-row status table (valid / update / error). Calls
 *                    /api/employees/import?dryRun=true for server validation.
 * Step 3 — RESULT:   Import summary. Option to download error report.
 */

import {
  useRef,
  useState,
  useCallback,
  type DragEvent,
  type ChangeEvent,
} from 'react';
import { useRouter } from 'next/navigation';
import {
  CheckCircle2,
  RefreshCw,
  XCircle,
  Upload,
  FileText,
  Download,
  AlertTriangle,
  ChevronRight,
} from 'lucide-react';
import { Button }    from '@/components/ui/button';
import { Progress }  from '@/components/ui/progress';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import type { ValidationSummary, ValidatedRow } from '@/lib/csv-import/validator';

// ─── Types ────────────────────────────────────────────────────────────────────

type Step = 'drop' | 'validate' | 'importing' | 'result';

interface ImportResult {
  inserted: number;
  updated:  number;
  errors:   string[];
}

// ─── Row status config ────────────────────────────────────────────────────────

const STATUS_CONFIG = {
  valid: {
    icon:  CheckCircle2,
    label: 'New',
    chip:  'bg-emerald-100 text-emerald-700',
    row:   '',
  },
  update: {
    icon:  RefreshCw,
    label: 'Update',
    chip:  'bg-blue-100 text-blue-700',
    row:   'bg-blue-50/40',
  },
  error: {
    icon:  XCircle,
    label: 'Error',
    chip:  'bg-red-100 text-red-700',
    row:   'bg-red-50/60',
  },
} as const;

// ─── Helper: download CSV template ────────────────────────────────────────────

function downloadTemplate() {
  const csv = [
    'full_name,employee_code,hotel,department,position,email',
    'Jane Smith,EMP001,Indaba Hotel,Front Office,Receptionist,jane.smith@indabahotel.com',
    'John Doe,EMP002,Chobe Safari Lodge,F&B,Waiter,john.doe@chobesafari.com',
  ].join('\n');

  const blob = new Blob([csv], { type: 'text/csv' });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = 'employees_template.csv';
  a.click();
  URL.revokeObjectURL(url);
}

// ─── Helper: download error report ───────────────────────────────────────────

function downloadErrorReport(rows: ValidatedRow[]) {
  const errorRows = rows.filter((r) => r.status === 'error');
  const lines = [
    'line_number,employee_code,full_name,hotel,errors',
    ...errorRows.map((r) =>
      [
        r.lineNumber,
        r.employee_code || r.raw.employee_code || '',
        r.full_name     || r.raw.full_name     || '',
        r.hotel         || r.raw.hotel         || '',
        `"${r.errors.join('; ')}"`,
      ].join(','),
    ),
  ].join('\n');

  const blob = new Blob([lines], { type: 'text/csv' });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = 'import_errors.csv';
  a.click();
  URL.revokeObjectURL(url);
}

// ─── Step indicators ─────────────────────────────────────────────────────────

function Steps({ current }: { current: Step }) {
  const steps: [Step, string][] = [
    ['drop',     '1. Upload'],
    ['validate', '2. Review'],
    ['result',   '3. Done'],
  ];
  const order: Step[] = ['drop', 'validate', 'importing', 'result'];
  const currentIdx = order.indexOf(current);

  return (
    <ol className="flex items-center gap-1 text-xs text-muted-foreground">
      {steps.map(([step, label], i) => {
        const stepIdx = order.indexOf(step);
        const done    = currentIdx > stepIdx;
        const active  = currentIdx === stepIdx || (step === 'validate' && current === 'importing');

        return (
          <li key={step} className="flex items-center gap-1">
            {i > 0 && <ChevronRight className="h-3 w-3 opacity-40" />}
            <span
              className={cn(
                'font-medium transition-colors',
                done   && 'text-emerald-600',
                active && 'text-violet-700',
              )}
            >
              {done ? '✓ ' : ''}{label}
            </span>
          </li>
        );
      })}
    </ol>
  );
}

// ─── Step 1: Drop zone ────────────────────────────────────────────────────────

function DropStep({
  onFile,
  error,
}: {
  onFile: (file: File) => void;
  error?: string;
}) {
  const inputRef  = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  const accept = (file: File) => {
    if (!file.name.endsWith('.csv')) {
      return; // silently ignore; browser already filters
    }
    onFile(file);
  };

  const onDrop = useCallback(
    (e: DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      setDragging(false);
      const file = e.dataTransfer.files[0];
      if (file) accept(file);
    },
    [],
  );

  const onInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) accept(file);
    e.target.value = '';
  };

  return (
    <div className="space-y-4">
      {/* Drop zone */}
      <div
        onClick={() => inputRef.current?.click()}
        onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        className={cn(
          'flex cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed px-8 py-14 text-center transition-colors',
          dragging
            ? 'border-violet-500 bg-violet-50'
            : 'border-muted-foreground/25 hover:border-violet-400 hover:bg-muted/40',
        )}
      >
        <Upload
          className={cn(
            'mb-3 h-10 w-10 transition-colors',
            dragging ? 'text-violet-600' : 'text-muted-foreground/50',
          )}
        />
        <p className="text-sm font-semibold text-foreground">
          Drop your CSV here, or click to browse
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          Required: <code className="rounded bg-muted px-1">full_name</code>,{' '}
          <code className="rounded bg-muted px-1">employee_code</code>,{' '}
          <code className="rounded bg-muted px-1">hotel</code>
          {' '}· Optional: <code className="rounded bg-muted px-1">department</code>,{' '}
          <code className="rounded bg-muted px-1">position</code>,{' '}
          <code className="rounded bg-muted px-1">email</code>
        </p>
        <p className="mt-0.5 text-xs text-muted-foreground">Max 2,000 rows · 5 MB</p>
        <input
          ref={inputRef}
          type="file"
          accept=".csv"
          className="hidden"
          onChange={onInputChange}
        />
      </div>

      {error && (
        <div className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          {error}
        </div>
      )}

      {/* Template download */}
      <button
        type="button"
        onClick={downloadTemplate}
        className="flex items-center gap-1.5 text-xs text-muted-foreground underline-offset-2 hover:text-violet-700 hover:underline"
      >
        <Download className="h-3.5 w-3.5" />
        Download CSV template
      </button>
    </div>
  );
}

// ─── Step 2: Validation table ─────────────────────────────────────────────────

function ValidateStep({
  summary,
  filename,
  onBack,
  onImport,
}: {
  summary:   ValidationSummary;
  filename:  string;
  onBack:    () => void;
  onImport:  () => void;
}) {
  const [showAll, setShowAll] = useState(false);
  const displayRows = showAll ? summary.rows : summary.rows.slice(0, 50);

  return (
    <div className="space-y-4">
      {/* Summary bar */}
      <div className="flex flex-wrap gap-2">
        <StatPill
          icon={CheckCircle2}
          label={`${summary.validCount} to insert`}
          color="text-emerald-600"
          bg="bg-emerald-50"
        />
        <StatPill
          icon={RefreshCw}
          label={`${summary.updateCount} to update`}
          color="text-blue-600"
          bg="bg-blue-50"
        />
        {summary.errorCount > 0 && (
          <StatPill
            icon={XCircle}
            label={`${summary.errorCount} error${summary.errorCount !== 1 ? 's' : ''} (skipped)`}
            color="text-red-600"
            bg="bg-red-50"
          />
        )}
      </div>

      {/* File name */}
      <div className="flex items-center gap-2 rounded-lg bg-muted/50 px-3 py-2">
        <FileText className="h-4 w-4 text-muted-foreground" />
        <span className="text-sm text-muted-foreground">{filename}</span>
        <span className="ml-auto text-xs text-muted-foreground">
          {summary.totalRows} row{summary.totalRows !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Per-row table */}
      <div className="max-h-72 overflow-y-auto rounded-lg border">
        <Table>
          <TableHeader className="sticky top-0 bg-card">
            <TableRow>
              <TableHead className="w-12 text-center">#</TableHead>
              <TableHead>Code</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Hotel</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {displayRows.map((row) => {
              const cfg = STATUS_CONFIG[row.status];
              const Icon = cfg.icon;
              return (
                <TableRow key={row.lineNumber} className={cfg.row}>
                  <TableCell className="text-center text-xs text-muted-foreground">
                    {row.lineNumber}
                  </TableCell>
                  <TableCell className="font-mono text-xs">
                    {row.employee_code || row.raw.employee_code || '—'}
                  </TableCell>
                  <TableCell className="text-sm">
                    {row.full_name || row.raw.full_name || '—'}
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {row.hotel || row.raw.hotel || '—'}
                  </TableCell>
                  <TableCell>
                    <div className="space-y-0.5">
                      <span
                        className={cn(
                          'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold',
                          cfg.chip,
                        )}
                      >
                        <Icon className="h-2.5 w-2.5" />
                        {cfg.label}
                      </span>
                      {row.errors.map((e, i) => (
                        <p key={i} className="text-[10px] text-red-600">{e}</p>
                      ))}
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>

        {!showAll && summary.rows.length > 50 && (
          <button
            className="w-full py-2 text-center text-xs text-muted-foreground hover:text-foreground"
            onClick={() => setShowAll(true)}
          >
            Show all {summary.rows.length} rows
          </button>
        )}
      </div>

      {/* Error download */}
      {summary.errorCount > 0 && (
        <button
          type="button"
          onClick={() => downloadErrorReport(summary.rows)}
          className="flex items-center gap-1.5 text-xs text-muted-foreground underline-offset-2 hover:text-red-600 hover:underline"
        >
          <Download className="h-3.5 w-3.5" />
          Download error report ({summary.errorCount} rows)
        </button>
      )}

      {/* Footer actions */}
      <div className="flex justify-between pt-1">
        <Button variant="outline" onClick={onBack}>
          ← Back
        </Button>
        <Button
          onClick={onImport}
          disabled={!summary.canImport}
        >
          Import {summary.validCount + summary.updateCount} employee
          {summary.validCount + summary.updateCount !== 1 ? 's' : ''}
        </Button>
      </div>
    </div>
  );
}

// ─── Step 3: Result ───────────────────────────────────────────────────────────

function ResultStep({
  result,
  rows,
  onClose,
}: {
  result:  ImportResult;
  rows:    ValidatedRow[];
  onClose: () => void;
}) {
  const hasErrors = result.errors.length > 0;

  return (
    <div className="space-y-5 py-2 text-center">
      <div
        className={cn(
          'mx-auto flex h-16 w-16 items-center justify-center rounded-full',
          hasErrors ? 'bg-amber-100' : 'bg-emerald-100',
        )}
      >
        {hasErrors ? (
          <AlertTriangle className="h-8 w-8 text-amber-600" />
        ) : (
          <CheckCircle2 className="h-8 w-8 text-emerald-600" />
        )}
      </div>

      <div>
        <p className="text-lg font-bold">
          {hasErrors ? 'Import completed with errors' : 'Import successful!'}
        </p>
        <p className="mt-1 text-sm text-muted-foreground">
          {result.inserted} inserted · {result.updated} updated
          {hasErrors ? ` · ${result.errors.length} failed` : ''}
        </p>
      </div>

      {hasErrors && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-left">
          <p className="mb-1 text-xs font-semibold text-amber-800">
            Import errors ({result.errors.length}):
          </p>
          <ul className="space-y-0.5">
            {result.errors.slice(0, 5).map((e, i) => (
              <li key={i} className="text-xs text-amber-700">{e}</li>
            ))}
            {result.errors.length > 5 && (
              <li className="text-xs text-amber-500">
                … and {result.errors.length - 5} more
              </li>
            )}
          </ul>
        </div>
      )}

      <div className="flex justify-center gap-3">
        {rows.filter((r) => r.status === 'error').length > 0 && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => downloadErrorReport(rows)}
          >
            <Download className="mr-2 h-3.5 w-3.5" />
            Error report
          </Button>
        )}
        <Button onClick={onClose}>Done</Button>
      </div>
    </div>
  );
}

// ─── Stat pill ────────────────────────────────────────────────────────────────

function StatPill({
  icon: Icon,
  label,
  color,
  bg,
}: {
  icon:  React.ElementType;
  label: string;
  color: string;
  bg:    string;
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold',
        bg,
        color,
      )}
    >
      <Icon className="h-3.5 w-3.5" />
      {label}
    </span>
  );
}

// ─── Main component ───────────────────────────────────────────────────────────

interface CsvImportDialogProps {
  open:    boolean;
  onClose: () => void;
}

export function CsvImportDialog({ open, onClose }: CsvImportDialogProps) {
  const router = useRouter();

  const [step,     setStep]     = useState<Step>('drop');
  const [filename, setFilename] = useState('');
  const [fileRef,  setFileRef]  = useState<File | null>(null);
  const [summary,  setSummary]  = useState<ValidationSummary | null>(null);
  const [result,   setResult]   = useState<ImportResult | null>(null);
  const [dropError, setDropError] = useState('');
  const [progress, setProgress] = useState(0);

  function reset() {
    setStep('drop');
    setFilename('');
    setFileRef(null);
    setSummary(null);
    setResult(null);
    setDropError('');
    setProgress(0);
  }

  function handleClose() {
    reset();
    onClose();
  }

  // ── Step 1 → 2: validate (dry run) ────────────────────────────────────────

  async function handleFile(file: File) {
    setDropError('');
    setFilename(file.name);
    setFileRef(file);
    setStep('validate');
    setSummary(null);

    const form = new FormData();
    form.append('file', file);
    form.append('dryRun', 'true');

    try {
      const res = await fetch('/api/employees/import', {
        method: 'POST',
        body:   form,
      });
      const json = await res.json();

      if (!res.ok) {
        setStep('drop');
        setDropError(json.error ?? 'Validation failed');
        return;
      }

      setSummary(json.validation as ValidationSummary);
    } catch {
      setStep('drop');
      setDropError('Network error — please try again.');
    }
  }

  // ── Step 2 → 3: import ────────────────────────────────────────────────────

  async function handleImport() {
    if (!fileRef) return;
    setStep('importing');
    setProgress(0);

    // Animate progress bar while waiting
    const tick = setInterval(() => {
      setProgress((p) => Math.min(p + 8, 85));
    }, 200);

    const form = new FormData();
    form.append('file', fileRef);
    form.append('dryRun', 'false');

    try {
      const res  = await fetch('/api/employees/import', {
        method: 'POST',
        body:   form,
      });
      const json = await res.json();

      clearInterval(tick);
      setProgress(100);

      if (!res.ok) {
        // Re-show validation step with error
        setStep('validate');
        setDropError(json.error ?? 'Import failed');
        return;
      }

      setResult(json.import as ImportResult);
      router.refresh();
      setStep('result');
    } catch {
      clearInterval(tick);
      setStep('validate');
      setDropError('Network error — please try again.');
    }
  }

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) handleClose(); }}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Import Employees</DialogTitle>
          <DialogDescription asChild>
            <div className="mt-1">
              <Steps current={step} />
            </div>
          </DialogDescription>
        </DialogHeader>

        {/* Progress bar (importing only) */}
        {step === 'importing' && (
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">Importing employees…</p>
            <Progress value={progress} className="h-2" />
          </div>
        )}

        {step === 'drop' && (
          <DropStep onFile={handleFile} error={dropError} />
        )}

        {step === 'validate' && !summary && (
          <div className="flex items-center justify-center py-10">
            <div className="h-6 w-6 animate-spin rounded-full border-2 border-violet-600 border-t-transparent" />
            <span className="ml-3 text-sm text-muted-foreground">Validating…</span>
          </div>
        )}

        {step === 'validate' && summary && (
          <ValidateStep
            summary={summary}
            filename={filename}
            onBack={reset}
            onImport={handleImport}
          />
        )}

        {step === 'result' && result && summary && (
          <ResultStep
            result={result}
            rows={summary.rows}
            onClose={handleClose}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}
