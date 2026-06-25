-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 064 — Add email column to employees table
--
-- Adds an optional email address to employee records.
-- Used to send redemption voucher emails for hotel rewards.
-- Populated via CSV upload through the admin portal.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE public.employees
  ADD COLUMN IF NOT EXISTS email text;

COMMENT ON COLUMN public.employees.email IS
  'Optional employee email address. Used to deliver hotel reward vouchers on redemption.
   Populated via CSV upload through the admin portal.';
