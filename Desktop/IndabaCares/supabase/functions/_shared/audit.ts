/**
 * Audit logging helper for Edge Functions.
 * Every sensitive action is recorded immutably in public.audit_logs.
 *
 * NOTE: audit_logs.company_id column still exists (FK to companies was dropped
 * by CASCADE in migration 030 but the column itself was not removed).
 * hotel is stored in company_id for now — a future migration should rename
 * the column or add a dedicated hotel column.
 */

import type { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";

interface AuditEntry {
  hotel:       string;        // tenant identifier (stored in company_id column)
  actorId:     string | null; // employee_id of the acting employee
  action:      string;        // e.g. 'redemption.cancel', 'recognition.boost'
  targetType?: string;        // e.g. 'redemption', 'recognition'
  targetId?:   string;
  metadata?:   Record<string, unknown>;
  req?:        Request;       // to extract IP and user-agent
}

export async function writeAuditLog(
  adminClient: SupabaseClient,
  entry: AuditEntry
): Promise<void> {
  const ipAddress = entry.req?.headers.get("x-forwarded-for")
    || entry.req?.headers.get("cf-connecting-ip")
    || null;
  const userAgent = entry.req?.headers.get("user-agent") || null;

  const { error } = await adminClient.from("audit_logs").insert({
    company_id:  entry.hotel,       // hotel stored here until column is renamed
    actor_id:    entry.actorId,
    action:      entry.action,
    target_type: entry.targetType  ?? null,
    target_id:   entry.targetId    ?? null,
    metadata:    entry.metadata    ?? {},
    ip_address:  ipAddress,
    user_agent:  userAgent,
  });

  if (error) {
    // Non-fatal — audit failure must not break business logic
    console.error("Audit log write failed:", error);
  }
}
