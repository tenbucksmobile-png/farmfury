/**
 * Robust CSV parser.
 *
 * Handles:
 *   - Quoted fields containing commas, newlines, and escaped quotes ("")
 *   - CRLF and LF line endings
 *   - UTF-8 BOM
 *   - Blank rows (skipped)
 *   - Leading/trailing whitespace in headers and cell values
 */

export interface RawRow {
  /** 1-based line number in the original file (skips blank lines). */
  lineNumber: number;
  fields:     Record<string, string>;
}

export function parseCsv(text: string): { headers: string[]; rows: RawRow[] } {
  // Strip UTF-8 BOM
  const cleaned = text.startsWith('\uFEFF') ? text.slice(1) : text;

  const records  = tokenise(cleaned);
  if (records.length === 0) return { headers: [], rows: [] };

  // First record = headers
  const headers = records[0].values.map((h) => h.trim().toLowerCase().replace(/\s+/g, '_'));

  const rows: RawRow[] = [];
  for (let i = 1; i < records.length; i++) {
    const { lineNumber, values } = records[i];

    // Skip entirely blank rows
    if (values.every((v) => v.trim() === '')) continue;

    const fields: Record<string, string> = {};
    headers.forEach((h, idx) => {
      fields[h] = (values[idx] ?? '').trim();
    });

    rows.push({ lineNumber, fields });
  }

  return { headers, rows };
}

// ─── Tokeniser ────────────────────────────────────────────────────────────────

interface CsvRecord {
  lineNumber: number;
  values:     string[];
}

function tokenise(text: string): CsvRecord[] {
  const records: CsvRecord[] = [];
  let pos        = 0;
  let lineNumber = 1;
  let recStart   = true;
  let current: string[] = [];
  let recLineNumber      = 1;

  function pushRecord() {
    if (current.length > 0) {
      records.push({ lineNumber: recLineNumber, values: current });
    }
    current       = [];
    recStart      = true;
    recLineNumber = lineNumber;
  }

  while (pos < text.length) {
    if (recStart) {
      recLineNumber = lineNumber;
      recStart = false;
    }

    const ch = text[pos];

    if (ch === '"') {
      // Quoted field
      pos++;
      let field = '';
      while (pos < text.length) {
        const c = text[pos];
        if (c === '"') {
          if (text[pos + 1] === '"') {
            // Escaped quote
            field += '"';
            pos += 2;
          } else {
            pos++;
            break;
          }
        } else {
          if (c === '\n') lineNumber++;
          field += c;
          pos++;
        }
      }
      current.push(field);
      // Skip trailing comma or newline
      if (text[pos] === ',') pos++;
    } else if (ch === ',') {
      current.push('');
      pos++;
    } else if (ch === '\r') {
      current.push('');
      pushRecord();
      pos++;
      if (text[pos] === '\n') pos++;
      lineNumber++;
    } else if (ch === '\n') {
      current.push('');
      pushRecord();
      pos++;
      lineNumber++;
    } else {
      // Unquoted field
      let field = '';
      while (pos < text.length && text[pos] !== ',' && text[pos] !== '\r' && text[pos] !== '\n') {
        field += text[pos++];
      }
      current.push(field.trim());

      if (pos < text.length) {
        if (text[pos] === ',') {
          pos++;
        } else if (text[pos] === '\r') {
          pushRecord();
          pos++;
          if (text[pos] === '\n') pos++;
          lineNumber++;
        } else if (text[pos] === '\n') {
          pushRecord();
          pos++;
          lineNumber++;
        }
      }
    }
  }

  // Flush last record
  if (current.length > 0) {
    records.push({ lineNumber: recLineNumber, values: current });
  }

  return records;
}
