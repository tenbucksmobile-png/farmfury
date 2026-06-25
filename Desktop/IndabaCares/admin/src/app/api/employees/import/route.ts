/**
 * POST /api/employees/import
 *
 * Accepts multipart/form-data:
 *   file    — CSV file (required)
 *   dryRun  — "true" | "false"  (optional, default "false")
 *
 * Security:
 *   • Requires valid Supabase admin session (admin or super_admin role).
 *   • Rate limited: 10 imports per 5 minutes per IP.
 *   • File size cap: 5 MB.
 *   • Row cap: 2,000 rows.
 *
 * Flow:
 *   1. Auth check — reject if not authenticated admin
 *   2. Rate limit check
 *   3. Parse CSV with robust tokeniser
 *   4. Validate headers — return 400 if required columns missing
 *   5. Phase-1 validate all rows (pure, no DB)
 *   6. Fetch existing (employee_code, hotel) pairs from DB for hotels in CSV
 *   7. Phase-2 classify rows as INSERT vs UPDATE
 *   8. If dryRun=true  → return validation summary only
 *   9. If dryRun=false → bulk upsert valid+update rows, return full result
 */

import { NextRequest, NextResponse } from 'next/server';
import { createServerClient }  from '@supabase/ssr';
import { createAdminClient }   from '@/lib/supabase/admin';
import { parseCsv }            from '@/lib/csv-import/parser';
import { rateLimit, getClientIp } from '@/lib/rate-limit';
import {
  validateHeaders,
  validateRows,
  applyDbCheck,
  buildSummary,
  type ValidatedRow,
} from '@/lib/csv-import/validator';

// ── Auth helper ───────────────────────────────────────────────────────────────

async function getAdminUser(request: NextRequest) {
  const response = NextResponse.next();

  const supabase = createServerClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
    {
      cookies: {
        getAll:  () => request.cookies.getAll(),
        setAll:  (cookies) => cookies.forEach(({ name, value, options }) =>
          response.cookies.set(name, value, options),
        ),
      },
    },
  );

  const { data: { user }, error } = await supabase.auth.getUser();
  if (error || !user) return null;

  const role = user.app_metadata?.role as string | undefined;
  if (role !== 'admin' && role !== 'super_admin') return null;

  return user;
}

// ── Route handler ─────────────────────────────────────────────────────────────

export async function POST(req: NextRequest) {

  // ── 1. Auth check ─────────────────────────────────────────────────────────

  const user = await getAdminUser(req);
  if (!user) {
    return NextResponse.json(
      { error: 'Authentication required. Sign in to the admin portal.' },
      { status: 401 },
    );
  }

  // ── 2. Rate limit: 10 imports per 5 minutes per IP ───────────────────────

  const ip     = getClientIp(req);
  const result = rateLimit(`import:${ip}`, 10, 5 * 60);

  if (!result.allowed) {
    return NextResponse.json(
      { error: 'Too many import requests. Please wait a few minutes and try again.' },
      {
        status: 429,
        headers: {
          'Retry-After':        String(Math.ceil((result.resetAt - Date.now()) / 1000)),
          'X-RateLimit-Limit':  '10',
          'X-RateLimit-Remaining': '0',
        },
      },
    );
  }

  // ── 3. Parse multipart form ───────────────────────────────────────────────

  let formData: FormData;
  try {
    formData = await req.formData();
  } catch {
    return NextResponse.json({ error: 'Expected multipart/form-data' }, { status: 400 });
  }

  const file   = formData.get('file');
  const dryRun = formData.get('dryRun') === 'true';

  if (!file || typeof file === 'string') {
    return NextResponse.json({ error: 'No file uploaded' }, { status: 400 });
  }

  // ── 4. Size guard: 5 MB max ───────────────────────────────────────────────

  if (file.size > 5 * 1024 * 1024) {
    return NextResponse.json({ error: 'File exceeds 5 MB limit' }, { status: 413 });
  }

  const text = await file.text();

  // ── 5. Parse CSV ──────────────────────────────────────────────────────────

  const { headers, rows: rawRows } = parseCsv(text);

  if (headers.length === 0) {
    return NextResponse.json(
      { error: 'CSV file is empty or has no headers' },
      { status: 400 },
    );
  }

  // ── 6. Header validation ──────────────────────────────────────────────────

  const missingCols = validateHeaders(headers);
  if (missingCols.length > 0) {
    return NextResponse.json(
      {
        error: `Missing required column${missingCols.length > 1 ? 's' : ''}: ${missingCols.join(', ')}`,
        headers,
      },
      { status: 400 },
    );
  }

  if (rawRows.length === 0) {
    return NextResponse.json(
      { error: 'CSV has headers but no data rows' },
      { status: 400 },
    );
  }

  // ── 7. Row limit: 2,000 per upload ────────────────────────────────────────

  if (rawRows.length > 2000) {
    return NextResponse.json(
      { error: `Too many rows (${rawRows.length}). Maximum is 2,000 per upload.` },
      { status: 400 },
    );
  }

  // ── 8. Phase-1 validation (pure) ──────────────────────────────────────────

  const phase1Rows = validateRows(rawRows);

  // ── 9. Fetch existing records for DB check ────────────────────────────────

  const db = createAdminClient();

  const hotelsInCsv = [
    ...new Set(
      phase1Rows
        .filter((r) => r.status !== 'error' && r.hotel)
        .map((r) => r.hotel),
    ),
  ];

  let existingKeys = new Set<string>();

  if (hotelsInCsv.length > 0) {
    const { data: existing, error } = await db
      .from('employees')
      .select('employee_code, hotel')
      .in('hotel', hotelsInCsv);

    if (error) {
      return NextResponse.json(
        { error: `DB error during validation: ${error.message}` },
        { status: 500 },
      );
    }

    existingKeys = new Set(
      (existing ?? []).map((e: { employee_code: string; hotel: string }) =>
        `${e.employee_code.toUpperCase()}::${e.hotel}`,
      ),
    );
  }

  // ── 10. Phase-2: classify INSERT vs UPDATE ────────────────────────────────

  const finalRows = applyDbCheck(phase1Rows, existingKeys);
  const summary   = buildSummary(finalRows);

  // ── 11. Dry run ───────────────────────────────────────────────────────────

  if (dryRun) {
    return NextResponse.json({ validation: summary });
  }

  // ── 12. Perform import ────────────────────────────────────────────────────

  const importable   = finalRows.filter((r) => r.status === 'valid' || r.status === 'update');
  const importErrors: string[] = [];
  let inserted = 0;
  let updated  = 0;

  const toInsert = importable.filter((r) => r.status === 'valid');
  const toUpdate = importable.filter((r) => r.status === 'update');

  // Bulk insert in batches of 500
  for (let i = 0; i < toInsert.length; i += 500) {
    const batch = toInsert.slice(i, i + 500).map(rowToPayload);
    const { error } = await db.from('employees').insert(batch);

    if (error) {
      importErrors.push(`Insert batch ${Math.floor(i / 500) + 1} failed: ${error.message}`);
    } else {
      inserted += batch.length;
    }
  }

  // Per-row updates
  for (const row of toUpdate) {
    const { error } = await db
      .from('employees')
      .update({
        full_name:     row.full_name,
        department:    row.raw.department?.trim() || null,
        position:      row.raw.position?.trim()   || null,
        email:         row.email,
        date_of_birth: row.date_of_birth ?? null,
        start_date:    row.start_date    ?? null,
      })
      .eq('employee_code', row.employee_code)
      .eq('hotel', row.hotel);

    if (error) {
      importErrors.push(`Row ${row.lineNumber} (${row.employee_code}): ${error.message}`);
    } else {
      updated++;
    }
  }

  return NextResponse.json({
    validation: summary,
    import: { inserted, updated, errors: importErrors },
  });
}

// ── Helper ────────────────────────────────────────────────────────────────────

function rowToPayload(row: ValidatedRow) {
  return {
    employee_code: row.employee_code,
    full_name:     row.full_name,
    hotel:         row.hotel,
    department:    row.raw.department?.trim() || null,
    position:      row.raw.position?.trim()   || null,
    email:         row.email,
    date_of_birth: row.date_of_birth ?? null,
    start_date:    row.start_date    ?? null,
    status:        'active',
  };
}
