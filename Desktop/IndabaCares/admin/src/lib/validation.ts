/**
 * Zod schemas for all admin server actions and API routes.
 *
 * Every schema strips / trims inputs before DB write.
 * Call schema.parse(data) in server actions — throws ZodError on invalid input.
 */

import { z } from 'zod';
import { HOTELS } from '@/lib/hotels';

// ── Shared primitives ─────────────────────────────────────────────────────────

const hotel = z.enum(HOTELS as unknown as [string, ...string[]], {
  error: () => ({ message: 'Invalid hotel name.' }),
});

const uuid = z.string().uuid('Invalid ID format.');

const trimmedText = (min: number, max: number, label: string) =>
  z.string()
    .transform((s) => s.trim())
    .pipe(
      z.string()
        .min(min, `${label} must be at least ${min} character${min === 1 ? '' : 's'}.`)
        .max(max, `${label} must not exceed ${max} characters.`),
    );

const isoDate = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, 'Date must be in YYYY-MM-DD format.');

// ── Campaign ──────────────────────────────────────────────────────────────────

export const campaignSchema = z
  .object({
    title:                trimmedText(3, 120, 'Title'),
    description:          z.string().transform((s) => s.trim()).optional(),
    type:                 z.enum(['recognition', 'sponsor', 'both']).default('recognition'),
    points_multiplier:    z.number().int().min(1).max(10),
    hotel,
    start_date:           isoDate,
    end_date:             isoDate,
    sponsor_name:         z.string().transform((s) => s.trim()).optional(),
    banner_url:           z.string().url('Banner URL must be a valid URL.').optional().or(z.literal('')),
    banner_link_url:      z.string().url('Banner link must be a valid URL.').optional().or(z.literal('')),
    voucher_description:  z.string().transform((s) => s.trim()).optional(),
  })
  .refine(
    (d) => d.end_date >= d.start_date,
    { message: 'End date must be on or after start date.', path: ['end_date'] },
  );

export type CampaignInput = z.infer<typeof campaignSchema>;

// ── Reward ────────────────────────────────────────────────────────────────────

export const rewardSchema = z.object({
  title:           trimmedText(2, 120, 'Title'),
  description:     z.string().transform((s) => s.trim()).optional(),
  points_required: z.number().int().min(1, 'Points required must be at least 1.').max(100_000),
  hotels:          z.array(hotel).min(1, 'Select at least one hotel.'),
  stock:           z.number().int().min(0, 'Stock cannot be negative.').optional(),
  image_url:       z
    .string()
    .url('Image URL must be a valid URL.')
    .optional()
    .or(z.literal('')),
  category:        z.enum(['hotel', 'retail']).default('hotel'),
  wicode:          z.string().trim().optional().or(z.literal('')),
});

export type RewardInput = z.infer<typeof rewardSchema>;

// ── Redemption actions ────────────────────────────────────────────────────────

export const rejectRedemptionSchema = z.object({
  id:     uuid,
  reason: z.string().transform((s) => s.trim()).optional(),
});

export const redemptionIdSchema = z.object({ id: uuid });

// ── Announcement ──────────────────────────────────────────────────────────────

export const announcementSchema = z.object({
  hotel,
  title:   trimmedText(3, 120, 'Title'),
  message: trimmedText(10, 2000, 'Message'),
});

export type AnnouncementInput = z.infer<typeof announcementSchema>;

// ── Employee ──────────────────────────────────────────────────────────────────

export const employeeIdSchema = z.object({ id: uuid });

// ── CSV import ────────────────────────────────────────────────────────────────

export const csvImportQuerySchema = z.object({
  dryRun: z.enum(['true', 'false']).default('false'),
});

// ── Helper: format ZodError for user display ──────────────────────────────────

export function formatValidationError(error: z.ZodError): string {
  return error.issues.map((e) => e.message).join(' · ');
}
