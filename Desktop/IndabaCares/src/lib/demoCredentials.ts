// src/lib/demoCredentials.ts
//
// Pre-created demo account for App Store / Play Store review.
// This employee MUST exist in the Supabase production database before
// submitting the app for review.
//
// REMOVE THIS FILE (and the Try Demo button in employee-auth.tsx) once
// the app has been approved by Apple and Google.

// Credentials are injected via EAS Secrets for review builds only.
// In production builds (without EXPO_PUBLIC_REVIEW_MODE=true) these are empty
// strings, ensuring no demo credentials are bundled into the production JS.
export const DEMO_EMPLOYEE_CODE = process.env.EXPO_PUBLIC_DEMO_CODE     ?? '';
export const DEMO_HOTEL         = process.env.EXPO_PUBLIC_DEMO_HOTEL    ?? '';
export const DEMO_PASSWORD      = process.env.EXPO_PUBLIC_DEMO_PASSWORD ?? '';
