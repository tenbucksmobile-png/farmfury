# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

IndabaCares is a hotel-scoped employee recognition and engagement platform. It consists of three separate workspaces in one repo:

- **Mobile app** — React Native + Expo 54 + Expo Router (`app/`, `src/`)
- **Admin dashboard** — Next.js 16 App Router (`admin/`)
- **Backend** — Supabase (PostgreSQL + Edge Functions + Realtime) (`supabase/`)

---

## Commands

### Mobile (root)

```bash
npm install
npx expo start          # dev server (press i=iOS, a=Android)
npm run typecheck       # tsc --noEmit
npm run lint            # eslint
npm test                # jest
npm run test:watch      # jest --watch
npm run test:coverage   # jest --coverage
npm test -- --testPathPattern=session-manager   # run a single test file
npx expo start -c       # clear Metro cache
```

Tests live in `src/__tests__/` (auth and features subdirs) and use `jest-expo`. Path alias `@/` maps to `src/`.

### Admin (`cd admin`)

```bash
npm install
npm run dev             # Next.js dev at http://localhost:3000
npm run build           # production build
npm run lint
```

### Backend

```bash
supabase start          # start local stack (Docker required)
supabase stop
supabase db reset       # wipe + re-run all migrations + seed.sql
supabase migration new <name>
supabase db push --linked       # apply migrations to remote
supabase functions serve        # serve all edge functions locally
supabase functions deploy --linked              # deploy all
supabase functions deploy <name> --linked       # deploy one
supabase functions logs <name> --linked         # stream logs
```

### EAS (mobile builds)

Four build profiles defined in `eas.json`:

```bash
eas build --profile development --platform android   # dev client (internal)
eas build --profile preview --platform android       # APK for QA / TestFlight
eas build --profile production --platform all        # production (autoIncrement)
eas build --profile review --platform ios --clear-cache  # App Store review binary (demo account)
eas update --channel production                      # OTA update via expo-updates
```

`appVersionSource: "remote"` — version is managed by EAS, not `app.json`.

**Android build formats:** `preview` outputs APK (sideloadable for QA). `production` outputs AAB — the Play Store rejects APK for new submissions. Do not change `production` Android back to `apk`.

**Android permissions — `READ_MEDIA_IMAGES` must stay blocked:** The `expo-image-picker` Expo config plugin auto-injects `android.permission.READ_MEDIA_IMAGES` into `AndroidManifest.xml`. The app uses camera-only capture (no gallery picker), so this permission is unnecessary and must stay in `blockedPermissions` in `app.json`. If it appears in the manifest, the Play Store may flag it. Do not move it back to the regular `permissions` array.

---

## Auth Architecture

This project uses a **custom employee auth system**, not Supabase Auth.

**Why:** Employees authenticate with an `employee_code` + password (not email). There are no Supabase Auth JWTs in the mobile flow.

**How it works:**

1. Employee logs in → RPC returns a `session_token` (UUID) stored in `employee_active_sessions`:
   - First login: `first_time_authenticate` (migration 027) — atomic: identity + password hash + session in one transaction
   - Returning login: `authenticate_employee`
2. Client persists the session to `expo-secure-store` via `EmployeeSessionManager` (`src/lib/EmployeeSessionManager.ts`). Legacy AsyncStorage sessions are silently migrated to SecureStore on first load.
3. Every Supabase request from the mobile app passes through `src/lib/secureApi.ts` (domain allowlist + HTTPS enforcement + timeout + redirect guard) then injects `x-session-token` as a custom header (see `src/lib/supabase.ts` — `hotelAwareFetch`). **When adding a new third-party integration, add its domain to `ALLOWED_HOSTNAMES` in `secureApi.ts` or requests will be blocked.**
4. PostgreSQL RLS reads this header via `current_employee_hotel()` to enforce hotel-level tenant isolation (migration 017).
5. Edge Functions validate the token via the `validate_session` RPC inside `withEmployeeAuth` middleware (`supabase/functions/_shared/auth-middleware.ts`).

**Session boot sequence** (handled by `EmployeeProvider` on app start):
1. `loadSession()` — restore employee + token from SecureStore
2. `setSessionToken(token)` — inject header into Supabase client
3. `validateSessionWithDB()` — confirm employee still active in DB
4. If invalid → `clearSession()` — wipe SecureStore and header, route to login

**`EmployeeSession` type** (persisted in SecureStore, injected into context):
```ts
{
  employee_id:   string;   // UUID
  full_name:     string;
  employee_code: string;
  hotel:         string;   // hotel slug
  department:    string | null;
  position:      string | null;
  session_token: string;   // UUID used as x-session-token header
}
```

**`EmployeeContext` API** (`useEmployee()`):
- `employee: EmployeeSession | null`
- `isLoaded: boolean` — true once SecureStore read completes; `AuthProvider` waits before routing
- `setEmployee(session)` — persists + activates session token
- `clearEmployee()` — revokes server-side session, clears store and header
- `hasSeenWelcome: boolean | null` — null until loaded from DB; `AuthProvider` waits for it before routing
- `markWelcomeSeen()` — called by `AuthProvider` for first-time employees (welcome video removed; flag is still written so the DB stays consistent)

**Supabase Auth is disabled on the mobile client** (`src/lib/supabase.ts`):
```ts
auth: { autoRefreshToken: false, persistSession: false, detectSessionInUrl: false }
```
Do not use `supabase.auth.*` in mobile code — all session management goes through `EmployeeSessionManager`.

**Admin dashboard** uses standard Supabase email/password Auth with the `@supabase/ssr` package (HTTP-only cookies). The admin client in `admin/src/lib/supabase/admin.ts` uses `service_role` and bypasses RLS — only use it in Server Components/Actions, never in client components.

**Provider chain** (`app/_layout.tsx`):
```
ErrorBoundary → GestureHandlerRootView → SafeAreaProvider → QueryProvider → EmployeeProvider → AuthProvider → RealtimeProvider → NotificationProvider → ToastProvider
```
Root layout pre-loads two fonts via `useFonts()`: `DancingScript_700Bold` (from `@expo-google-fonts/dancing-script`) and `Ionicons.font` (from `@expo/vector-icons`). After the build-15 patch, `Ionicons.font` resolves to `{ 'Ionicons': require('Ionicons.ttf') }` (capital I). **Ionicons is also embedded natively via the `expo-font` config plugin in `app.json`.**

**Root cause of blank icons on iOS 26 (fixed in build 15):** `@expo/vector-icons/build/Ionicons.js` called `createIconSet(glyphMap, 'ionicons', ...)` (lowercase). On iOS `<Text fontFamily="ionicons">` is case-sensitive. The `expo-font` config plugin registers the font via `UIAppFonts` under its PostScript name `'Ionicons'` (capital I). The mismatch meant iOS could never find the font. Two-part fix in `@expo+vector-icons+15.0.3.patch`: (1) `Ionicons.js` now passes `'Ionicons'` to `createIconSet`; (2) `createIconSet.js` initialises `fontIsLoaded = true` immediately on New Architecture (`!!global.expo?.modules` is truthy) so icons render from the UIAppFonts-registered font without waiting for `ExpoFontLoader.loadAsync()`, with a try/catch fallback to prevent a failed load from permanently blanking icons.

Adding new icon families requires: spreading `IconFamily.font` into `useFonts()` AND adding the `.ttf` path to the `expo-font` plugin fonts array in `app.json` AND verifying the font family name passed to `createIconSet` matches the font's PostScript name (patch `build/IconFamily.js` if it doesn't).

`EmployeeProvider` owns the session state. `AuthProvider` wraps it and handles routing: unauthenticated → `/(auth)/employee-auth`; first-time employee (`hasSeenWelcome === false`) → calls `markWelcomeSeen()` then routes directly to `/(tabs)/profile`; returning employee → `/(tabs)/profile`.

**`NotificationProvider`** (`src/providers/NotificationProvider.tsx`) — handles push token re-registration on each login and attaches the notification response listener that drives tapped-notification → deep link routing. It sits inside `RealtimeProvider` in the root layout chain.

---

## Hotel-Level Tenant Isolation

All data is scoped to a `hotel` (string slug), not a `company_id`. Migration 017 established this model. Key PostgreSQL helpers:

- `current_employee_hotel()` — reads `x-session-token` from request headers, resolves to the employee's hotel slug
- `current_employee_id()` — same mechanism, returns the employee UUID
- RLS policies on every table use these functions

**Do not add direct `company_id` checks to new RLS policies** — use `hotel` via these helpers.

The canonical hotel list lives in **three places** that must be kept in sync:
- `src/lib/hotels.ts` (mobile)
- `admin/src/lib/hotels.ts` (admin dashboard)
- `is_valid_hotel()` function in migration 017

Current hotels: `'Indaba Hotel'`, `'Indaba Lodge Richards Bay'`, `'Indaba Lodge Gaborone'`, `'Chobe Safari Lodge'`, `'Nata Lodge'`, `'African Procurement Agencies'`.

**`APA_HOTEL` (`'African Procurement Agencies'`)** is a special-cased slug — migration 066 grants it cross-hotel read visibility. Treat it differently from regular hotels in any visibility or tenant logic.

---

## Mobile Screen Layout

Expo Router route groups:
- `app/(auth)/` — unauthenticated screens (employee login)
- `app/(tabs)/` — bottom-tab navigator: `profile`, `index` (feed), `give` (centre FAB), `leaderboard`, `rewards`
- `app/(screens)/` — full-screen push routes: flat screens (`chat`, `campaigns`, `initiatives`, `mood`, `notifications`, `orders`, `settings`, `team`, `wallet`, `channel-feed`, `edit-profile`, `redeemed`, `faq`, `delete-account`, `terms-of-service`, `privacy-policy`, etc.) plus dynamic routes: `recognition/[id]`, `reward/[id]`, `user/[id]`, `initiative/[slug]`, `team/[department]`, and `skills/` (sub-routes: `index`, `rate`, `my-scores`)

**Feature-flag-gated tabs** — `leaderboard` and `rewards` are hidden from the tab bar when their flags are off via `href: null` in `app/(tabs)/_layout.tsx`. The screens still exist; only the tab entry point is suppressed.

---

## Channel Feature

Hotel channel — a WhatsApp-channel-style public feed of photos, videos and text posts per hotel. Only two hotels have channels: **`'Indaba Hotel'`** and **`'Chobe Safari Lodge'`** (hardcoded in `app/(screens)/csr-hotels.tsx` and `admin/src/app/(dashboard)/channel/page.tsx`).

**Mobile flow:** Profile → hamburger → Channel → `csr-hotels.tsx` (two-hotel picker) → `channel-feed.tsx` (infinite-scroll feed). All employees can view any hotel's channel regardless of their own hotel. No reactions or comments — read-only.

**Video rendering:** `app/(screens)/ChannelVideoPost.tsx` is lazy-loaded via `React.lazy()` (PERF-02 pattern) to keep expo-av out of the main bundle. `app/(screens)/initiative/VideoComponents.tsx` follows the same pattern for initiative media.

**Header colour:** Both channel screens (`csr-hotels.tsx`, `channel-feed.tsx`) use `COLORS.primary` from `@/lib/constants` for the header splash — do not introduce a local `PURPLE` constant in these files.

**Admin portal:** `/channel` page in the admin sidebar. Hotel admins post to their hotel; super admin sees a hotel dropdown.

**Admin user scoping** — set on Supabase Auth user metadata (Auth → Users in the dashboard):
- Super admin: `{ "is_super_admin": true }` — sees all channel hotels, hotel selector dropdown
- Hotel admin: `{ "hotel": "Indaba Hotel" }` or `{ "hotel": "Chobe Safari Lodge" }` — locked to their hotel

**Upload flow:** client uploads the file directly to the `channel-media` Storage bucket (authenticated Supabase Auth session), then a Server Action inserts the `channel_posts` row via service_role.

**`channel-media` bucket** — public read, 100 MB per file limit, accepts `image/*` and `video/mp4,quicktime,webm`.

---

## Data Layer

**Mobile:** React Query hooks in `src/hooks/` are the consumption layer. The query/mutation logic lives one level below in `src/api/` — PostgREST wrappers in `queries.ts` and domain service files:

- `edge-functions.ts` — typed wrappers for all Edge Function calls
- `reward-service.ts`, `chat-service.ts`, `campaigns-service.ts`, `initiative-service.ts`
- `leaderboard-service.ts`, `legends-service.ts`, `notification-service.ts`
- `reaction-analytics-service.ts`, `team-service.ts`
- `channel-service.ts` — paginated cursor fetch for `channel_posts` (cross-hotel, used by the Channel tab)

Mutations that need business logic (balance checks, atomic updates) call Edge Functions via `src/api/edge-functions.ts`. Direct PostgREST calls are used for reads and simple writes.

All Supabase calls should be wrapped via `src/lib/api-client.ts`. Use `fetchWithGuards()` as the primary wrapper — it combines `withTimeout` (default 10 s) and exponential-backoff retry for transient network errors. The module also exports `debounce()` for search/autocomplete inputs.

React Query cache keys are centralised in `QUERY_KEYS` in `src/lib/constants.ts` — use these rather than inline string arrays in new hooks.

**State split:**
- Server state → React Query (`@tanstack/react-query`)
- Auth/session → `EmployeeContext` (React Context)
- UI-only state → Zustand (`src/stores/ui-store.ts`)
- Admin auth state → Zustand (`admin/src/stores/auth-store.ts`)

**Feature flags** — `src/hooks/use-feature-flags.ts` reads per-hotel feature toggles (`moods_enabled`, `rewards_enabled`, `skills_enabled`, `leaderboards_enabled`, `custom_hashtags_enabled`, `boost_enabled`) from `hotel_settings.feature_flags` (JSONB). Cached 5 minutes via React Query. Check these before rendering feature-gated UI.

**Admin data layer** — three tiers, each with a distinct role:
- `admin/src/api/queries.ts` — PostgREST read functions (paginated lists, filters) called by admin hooks via React Query
- `admin/src/api/mutations.ts` — PostgREST write functions for client-side direct updates (departments, thumbs-up types, etc.)
- `admin/src/api/edge-functions.ts` — typed wrappers for admin-initiated Edge Function calls
- `admin/src/api/export.ts` — bulk data export helpers

**Admin mutations** use Next.js Server Actions in `admin/src/app/actions/` (`employees.ts`, `rewards.ts`, `redemptions.ts`, `campaigns.ts`, `initiatives.ts`, `notifications.ts`, `channel.ts`) for form-based mutations that need server-side validation. Server Components fetch data directly via `createAdminClient()`. Client components call Server Actions via `useTransition`.

**Admin hooks** — `admin/src/hooks/` contains eight React hooks for the admin dashboard: `use-audit-logs`, `use-auth`, `use-departments`, `use-gamification`, `use-mood`, `use-recognitions`, `use-rewards`, `use-users`. These wrap `admin/src/api/queries.ts` via React Query. Check here before writing new data-fetching logic in admin client components.

**Admin routes** (`admin/src/app/`): `(dashboard)/` contains all authenticated admin pages (`analytics`, `audit-logs`, `campaigns`, `channel`, `departments`, `employees`, `gamification`, `initiatives`, `mood`, `notifications`, `recognitions`, `redemptions`, `rewards`, `settings`, `users`); `login/`, `forgot-password/`, `reset-password/`, `privacy/` are public routes (no auth). API routes live in `api/`. The `gamification/` directory has five nested pages: `badges`, `budgets`, `company-values`, `skills`, `thumbs-up-types`. The `users/` directory has sub-pages `[id]` (user detail) and `invite`. The `rewards/` directory has a nested `redemptions/` sub-page (in addition to the top-level `redemptions/` route). The `rewards/` directory also has a `categories/` sub-page.

**`privacy/page.tsx`** — public privacy policy page served at `indabacares.co.za/privacy`. Required by Apple for App Store submission. No auth guard.

**Auth middleware** — `admin/src/proxy.ts` is the Next.js middleware (exported as `default` with a `matcher` config). It enforces three layers of access control: (1) redirects unauthenticated requests to `/login`; (2) blocks users whose `app_metadata.role` is not `'admin'` or `'super_admin'`; (3) restricts `SUPER_ADMIN_ONLY` routes (`/audit-logs`, `/settings`) to `super_admin` only. It also injects security headers (CSP, HSTS, X-Frame-Options, etc.) on every response. To add a new public route, add its path to the `PUBLIC_PATHS` array at the top of that file. Current public paths: `/login`, `/forgot-password`, `/reset-password`, `/privacy`.

**Admin utilities:**
- `admin/src/lib/supabase/admin.ts` — service_role client (`createAdminClient()`); use in Server Components/Actions only. `server.ts` — SSR-safe client (cookie-based, for Server Components). `client.ts` — browser client for Client Components.
- `admin/src/lib/csv-import/parser.ts` + `validator.ts` — bulk employee import; handles quoted fields, CRLF/LF, UTF-8 BOM, and blank rows. Consumed by `admin/src/app/api/employees/import/route.ts`.
- `admin/src/lib/email/voucher-template.ts` — plain-HTML voucher email builder (no JSX) sent via Resend when a hotel-category redemption is approved.
- `admin/src/lib/validation.ts` — **server-side** Zod schemas for all Server Actions and API routes (hotel enum, UUIDs, trimmed text, etc.). Use `schema.parse(data)` in Server Actions.
- `admin/src/lib/validations.ts` — **client-side** Zod schemas for form validation (login, invite rows, etc.). Used with `react-hook-form` in client components.

**Realtime:** `RealtimeProvider` (`src/providers/RealtimeProvider.tsx`) subscribes to Postgres changes for notifications, reactions, and chat via `use-realtime.ts` and `use-presence.ts`. The `supabase_realtime` publication includes: `recognitions`, `reactions`, `comments`, `notifications`.

---

## Edge Functions

18 functions live in `supabase/functions/`. Shared utilities are in `supabase/functions/_shared/`:

- `auth-middleware.ts` — `withEmployeeAuth()` wrapper (validates `x-session-token`); also exports `errorResponse()`/`jsonResponse()` helpers and CORS headers
- `supabase-client.ts` — `createAdminClient()` (service_role)
- `rate-limit.ts` — per-operation rate limit enforcement
- `notifications.ts` — shared push notification helpers
- `audit.ts` — shared audit logging helpers

Every new Edge Function should use `withEmployeeAuth` for authenticated routes or handle CORS OPTIONS manually for public routes. All DB writes use `adminClient` (service_role) — RLS is enforced at the DB layer, not the application layer.

**Function inventory:**

| Function | Purpose |
|----------|---------|
| `auth-signup` | Employee registration |
| `auth-invite` | Admin-initiated employee invite |
| `auth-me` | Fetch current employee profile |
| `auth-update-role` | Change employee role |
| `auth-deactivate-user` | Deactivate an employee account |
| `claim-employee-code` | Link employee code to account |
| `send-recognition` | Create a peer recognition |
| `boost-recognition` | Manager star boost on a recognition |
| `evaluate-badges` | Check + award badge criteria |
| `manage-redemption` | Admin approve/reject redemption |
| `redeem-reward` | Employee redeem from catalogue |
| `cancel-redemption` | Cancel a pending redemption |
| `submit-mood` | Log daily mood check-in |
| `refresh-leaderboard` | Rebuild leaderboard cache (cron) |
| `award-monthly-legend` | Pick monthly top performer (cron) |
| `daily-celebrations` | Birthday/work-anniversary push notifications (cron) — must also INSERT into the `celebrations` table for `CelebrationCard` to appear in the feed |
| `reset-budgets` | Reset recognition budgets (cron) |
| `remove-background` | External AI image background removal — not behind `withEmployeeAuth`, handle CORS manually |

---

## Database Migrations

87 migrations (001–087) in `supabase/migrations/`. Notable architectural migrations:

| Migration | What it does |
|-----------|-------------|
| 006 | RLS policies on all tables + Realtime publication |
| 008 | Atomic Postgres RPCs (recognition, redemption, refund, mood, leaderboard) |
| 017 | Hotel-level RLS isolation (replaces company_id-based isolation) |
| 032 | Rebuilt session architecture (`employee_active_sessions`) |
| 033 | Dynamic leaderboard (no more static cache) |
| 078 | Points system overhaul |
| 079 | Reward wallet (`trg_guard_wallet_balance` — blocks direct UPDATE, requires `indabacares.allow_wallet_update` GUC) |
| 080 | Sponsor ad campaigns |
| 081 | `mood_entries.user_id` made nullable (legacy NOT NULL constraint removed) |
| 082 | `admin_set_wallet_balance` SECURITY DEFINER RPC — sets `reward_wallet_balance` directly, bypassing guard trigger |
| 083 | `admin_set_points_balance` SECURITY DEFINER RPC — sets `points_balance` directly, bypassing `trg_guard_points_balance` |
| 084 | `notifications.company_id` made nullable — `trg_notify_redemption` inserts without it |
| 085 | `notifications.user_id` made nullable — same trigger, same pattern as 084 |
| 086 | `channel_posts` table — hotel channel feed (photo/video/text); RLS allows cross-hotel SELECT for all authenticated employees; service_role only for writes; `channel-media` Storage bucket (public read, 100 MB limit) |
| 087 | Enable RLS on `auth_rate_limits` and `notifications_archive` — both accessed only via SECURITY DEFINER functions (postgres superuser bypasses RLS); no client-facing policies needed |

**Guard triggers** — `employees.points_balance` and `employees.reward_wallet_balance` are protected by triggers that block all direct UPDATEs. Use `admin_set_points_balance` / `admin_set_wallet_balance` RPCs instead. Never attempt to UPDATE these columns directly from application code.

**`points_ledger.source` CHECK constraint** — only specific values are allowed (e.g. `'admin_bonus'`, `'campaign_reward'`). The value `'admin_adjustment'` is NOT in the allowed list — use `'admin_bonus'` for admin-initiated balance corrections.

**Legacy NOT NULL pattern** — Several tables were created in early migrations (001–005) with `company_id` and `user_id` as NOT NULL. As the schema migrated to hotel-based tenancy, new code omits these columns. When a trigger or RPC inserts without them you get `null value in column "X" violates not-null constraint`. Fix with `ALTER TABLE public.<table> ALTER COLUMN <col> DROP NOT NULL;` (see migrations 074, 081, 084, 085 for prior examples).

**Immutable tables** — `star_transactions`, `point_transactions`, and `audit_logs` have triggers preventing UPDATE/DELETE. Never attempt to modify these rows.

To add a migration: `supabase migration new <description>`, edit the generated file, then `supabase db push --linked`.

`src/types/database.ts` is **manually maintained** — it does not use Supabase-generated types. Update it by hand when adding new tables or columns (or regenerate with `npx supabase gen types typescript --linked > src/types/database.ts` and merge carefully). Database enum mirrors for client-side use are in `src/types/enums.ts` — keep them in sync with `001_foundation.sql`.

`src/types/api.ts` documents the typed request/response contracts for every Edge Function call (e.g. `AuthMeResponse`, `SendRecognitionRequest`). Update this file when adding or changing Edge Function signatures.

---

## Utility Helpers (`src/utils/`)

Six small utility modules — prefer these over inline helpers:

- `format.ts` — `formatRelativeTime()` (relative date string), `formatNumber()` (K/M abbreviations)
- `notification-router.ts` — `routeFromNotification()` maps a notification payload (`referenceType` + `referenceId`) to the correct Expo Router route
- `image.ts` — image manipulation helpers (resize, compress)
- `validation.ts` — input validation helpers
- `linking.ts` — deep-link URL construction helpers
- `src/lib/haptics.ts` — `notificationHaptic()` (success feedback) and `impactHaptic(style)` (`'light' | 'medium' | 'heavy'`); no-ops on web

---

## Shared Constants

`src/lib/constants.ts` is the single source of truth for the mobile app:
- `COLORS` — brand palette (primary `#7C3AED`, etc.)
- `QUERY_KEYS` — React Query cache key factory (use in all new mobile hooks)
- `PAGE_SIZE`, `MAX_RECIPIENTS`, `MAX_HASHTAGS`, `MIN/MAX_MESSAGE_LENGTH` — limits
- `RECOGNITION_BADGES`, `REACTION_EMOJIS`, `MOOD_MAP`, `VISIBILITY_OPTIONS`, `REDEMPTION_STATUS`
- `BADGE_ICONS`

`admin/src/lib/constants.ts` is the equivalent for the admin dashboard — its own `QUERY_KEYS`, `PAGE_SIZE`, `ROLE_LABELS`, `ROLE_COLORS`, `MOOD_MAP`, and `REDEMPTION_STATUS`. Do not import mobile constants in admin code or vice versa.

---

## Shared Mobile Components

`src/components/` is organised into subdirectories by domain — `ui/`, `feed/`, `reactions/`, `recognition/`, `rewards/`, `profile/`, `notifications/`, `comments/`, `leaderboard/`, `mood/`. Prefer composing from these before writing screen-local components.

---

## UI Conventions

- **Mobile buttons:** Use `TouchableOpacity`, not `Pressable` inline in screen files — `Pressable` does not render `backgroundColor` correctly on the target Android device when using inline `style` props. The shared `src/components/ui/Button.tsx` uses `Pressable` with NativeWind `className` (not inline styles), which avoids the bug and is the safe exception.
- **Styling:** NativeWind (Tailwind for React Native) on mobile; Tailwind CSS v4 + shadcn/ui on admin.
- **Icons:** `@expo/vector-icons` on mobile; `lucide-react` on admin.
- **Forms (admin):** `react-hook-form` + `zod` for validation.
- **Tables (admin):** `@tanstack/react-table`.
- **Charts (admin):** `recharts`.
- **Toasts (admin):** `sonner`.
- **Zod version mismatch:** Mobile uses Zod v3 (`"^3.24.2"`); admin uses Zod v4 (`"^4.3.6"`). Zod v4 has breaking API changes — do not copy validation schemas between workspaces without reviewing the diff. The two `validation.ts` / `validations.ts` files in `admin/src/lib/` are already on v4 syntax.

---

## Environment Variables

**Mobile (`.env`):**
```
EXPO_PUBLIC_SUPABASE_URL=
EXPO_PUBLIC_SUPABASE_ANON_KEY=
```

**Admin (`admin/.env.local`):**
```
NEXT_PUBLIC_SUPABASE_URL=
NEXT_PUBLIC_SUPABASE_ANON_KEY=
SUPABASE_SERVICE_ROLE_KEY=      # required for admin client (Server Components only)
RESEND_API_KEY=                 # voucher emails via Resend
RESEND_FROM_DOMAIN=             # sender domain (e.g. indabacares.co.za)
```

Edge Functions receive `SUPABASE_URL`, `SUPABASE_ANON_KEY`, and `SUPABASE_SERVICE_ROLE_KEY` automatically from the Supabase project.

### Storage Buckets

Create these in Supabase Dashboard → Storage → New Bucket (or via migration). All must exist before the app can upload files:

| Bucket | Public | Max size | Allowed MIME |
|--------|--------|----------|--------------|
| `avatars` | Yes | 2 MB | image/jpeg, image/png, image/webp |
| `recognition-images` | Yes | 5 MB | image/jpeg, image/png, image/webp, image/gif |
| `reward-images` | Yes | 5 MB | image/jpeg, image/png, image/webp |
| `channel-media` | Yes | 100 MB | image/*, video/mp4, video/quicktime, video/webm |

**App Store / Play Store reviewer access (set via EAS Secrets — never commit):**

```
EXPO_PUBLIC_REVIEW_MODE=true        # enables auto-login with demo account
EXPO_PUBLIC_DEMO_CODE=DEMO01
EXPO_PUBLIC_DEMO_HOTEL=Indaba Hotel
EXPO_PUBLIC_DEMO_PASSWORD=DemoViewer1!
```

Set these with `eas secret:create --scope project` for review builds only. Standard production builds must NOT set `EXPO_PUBLIC_REVIEW_MODE=true`.

---

## App Store Review — Demo Account

`supabase/demo_seed.sql` creates **DEMO01** (the reviewer login) plus four fictional colleagues (Zanele, Sipho, Ayanda, Thandi) at Indaba Hotel, with recognitions, reactions, comments, mood history, and a rewards catalogue so every screen has visible content.

**Credentials:** Employee code `DEMO01` · Hotel `Indaba Hotel` · Password `DemoViewer1!`

**The seed is time-sensitive.** All recognitions and mood entries use timestamps relative to when the seed was last run. Re-run Section 1 of `demo_seed.sql` in the Supabase SQL Editor before every review submission to refresh all dates. The script is idempotent — it wipes and recreates cleanly each time.

**The "Try Demo" button** in `app/(auth)/employee-auth.tsx` is conditionally rendered — it only appears when `process.env.EXPO_PUBLIC_REVIEW_MODE === 'true'`. It is invisible in standard production builds (Metro dead-code eliminates the block).

**Building the review binary** — use the dedicated `review` profile in `eas.json` (inherits from `production`, sets all demo env vars as static strings):

```bash
eas build --profile review --platform ios --clear-cache
```

Do not use EAS Secrets for `EXPO_PUBLIC_REVIEW_MODE` — secrets are project-wide and would appear in production builds. The `review` profile in `eas.json` scopes them correctly. Do not use `$VAR` references in env blocks (EAS servers resolve them literally).

**Seed content for reviewers:** recognitions (received + sent), reactions, likes, comments, mood history (5 days), rewards catalogue (4 tiers), and 4 notifications (2 unread). Feature flags for Indaba Hotel default to all-enabled (migration 055 seeded them; missing rows fall back to all-enabled via `COALESCE` in `get_hotel_settings()`).

**Before every submission:** re-run Section 1 of `demo_seed.sql` in the Supabase SQL Editor (service_role) to refresh timestamps. The script is idempotent — it wipes and recreates cleanly each time.

**After Apple and Google approval:** run the cleanup block (Section 3 of `demo_seed.sql`) to remove all demo rows, then delete `src/lib/demoCredentials.ts` and remove the `handleDemoLogin` function and button from `employee-auth.tsx`.

---

## WiCode / Retail Voucher Integration

The `rewards` table has a `wicode text` column (migration 065). Admins can set a WiCode value on any `category = 'retail'` reward via the dashboard. **No live API integration exists yet** — the value is stored but never surfaced to employees or included in voucher emails.

**Planned integration vendor: Yoyo Rewards** (formerly WiGroup) — the originators of WiCode technology in South Africa. Their **IYOB (Issue Your Own Barcode)** B2B API is the correct product for programmatic voucher issuance. Contact yoyorewards.co.za and request the IYOB API.

**When the integration is ready, only three files need changing — no mobile build required:**

| File | Change |
|------|--------|
| `supabase/functions/manage-redemption/index.ts` | Call Yoyo Rewards IYOB API on approval, store returned code |
| `admin/src/lib/email/voucher-template.ts` | Surface WiCode in the voucher email for retail redemptions |
| `supabase/functions/_shared/` | Add Yoyo API key as an Edge Function secret |

Hotel rewards (spa, room upgrades, etc.) use voucher email with the redemption UUID and are unaffected.

---

## Scheduled Jobs (pg_cron)

Five cron jobs must be configured manually after migrations. Run this in the Supabase SQL Editor (requires `pg_cron` extension — enable it first under Database > Extensions):

```sql
-- Daily leaderboard refresh (02:00 UTC)
SELECT cron.schedule('refresh-leaderboard', '0 2 * * *', $$
  SELECT net.http_post(
    url := current_setting('app.settings.supabase_url') || '/functions/v1/refresh-leaderboard',
    headers := jsonb_build_object('Authorization', 'Bearer ' || current_setting('app.settings.service_role_key'), 'Content-Type', 'application/json')
  )
$$);

-- Monthly budget reset (1st of month, 00:05 UTC)
SELECT cron.schedule('reset-budgets', '5 0 1 * *', $$
  SELECT net.http_post(
    url := current_setting('app.settings.supabase_url') || '/functions/v1/reset-budgets',
    headers := jsonb_build_object('Authorization', 'Bearer ' || current_setting('app.settings.service_role_key'), 'Content-Type', 'application/json')
  )
$$);

-- Hourly rate limit cleanup
SELECT cron.schedule('cleanup-rate-limits', '0 * * * *', $$
  SELECT public.cleanup_rate_limits()
$$);

-- Daily birthday/anniversary push notifications + feed cards (08:00 UTC)
SELECT cron.schedule('daily-celebrations', '0 8 * * *', $$
  SELECT net.http_post(
    url := current_setting('app.settings.supabase_url') || '/functions/v1/daily-celebrations',
    headers := jsonb_build_object('Authorization', 'Bearer ' || current_setting('app.settings.service_role_key'), 'Content-Type', 'application/json')
  )
$$);

-- Monthly legend award (1st of month, 00:30 UTC)
SELECT cron.schedule('award-monthly-legend', '30 0 1 * *', $$
  SELECT net.http_post(
    url := current_setting('app.settings.supabase_url') || '/functions/v1/award-monthly-legend',
    headers := jsonb_build_object('Authorization', 'Bearer ' || current_setting('app.settings.service_role_key'), 'Content-Type', 'application/json')
  )
$$);
```

---

## Rate Limiting

Application-level rate limiting uses the `auth_rate_limits` table + `check_rate_limit()` function (migration 007). Edge Functions enforce per-operation limits (e.g., 5 recognitions/day, 5 redemptions/hour). Do not bypass these checks in new Edge Functions.

---

## Custom Expo Config Plugins (`plugins/`)

Three custom `withDangerousMod` plugins run at `npx expo prebuild` time (listed in `app.json` plugins array). Do not remove them — each fills a gap that Expo's built-in plugins don't cover.

| Plugin | Platform | What it does |
|--------|----------|--------------|
| `withPrivacyManifest.js` | iOS | Injects `PrivacyInfo.xcprivacy` (required by Apple since May 2024). Declares four required-reason APIs: `UserDefaults` (AsyncStorage/SecureStore/Reanimated/Notifications), `FileTimestamp` (expo-image disk cache), `DiskSpace` (expo-image cache eviction), `SystemBootTime` (Reanimated animation timing). |
| `withNetworkSecurityConfig.js` | Android | Writes `network_security_config.xml` to enforce OS-level HTTPS. Cleartext HTTP is blocked in release builds; `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16` are exempt in debug builds only (for Metro and local Supabase). This is a second enforcement layer complementing `secureApi.ts`. |
| `withAdiRegistration.js` | Android | Writes `adi-registration.properties` (ADI token `DKZYIVTASY4XOAAAAAAAAAAAAA`) into `app/src/main/assets/` — required for Play Store's Application Defence Initiative verification. |

If you add a new permission or SDK that accesses a "required reason" API on iOS, update `withPrivacyManifest.js` to declare it.

---

## iOS 26 / New Architecture Compatibility

**RN 0.81 always runs New Architecture (TurboModules / Bridgeless) regardless of `newArchEnabled`.** Do not set `newArchEnabled: false` — it was tried in the sibling project and crashed identically.

### The crash pattern

On iOS 26 with New Architecture, `NativeModules` is `BridgelessNativeModuleProxy`. Any property access routes through `turbomodulemanager.queue`, which dispatches a void method if the TurboModule isn't registered yet — causing `NSException → SIGABRT`. This happens when package code calls `NativeModules.*` or `requireNativeModule()` at **module-eval time** on the Hermes background thread (the thread that evaluates the JS bundle).

### Critical rules

- **Do not add `@sentry/react-native`** — `Sentry.init()` dispatches void TurboModule methods even with `autoInitializeNativeSdk: false` → SIGABRT on iOS 26.
- **Do not set `newArchEnabled: false`** — has no effect in RN 0.81; crashes identically.
- **`expo-notifications` must be lazy-required with a 3-second delay** — `BadgeModule.native.js` calls `requireNativeModule('ExpoBadgeModule')` at module-eval time → void TurboModule dispatch → NSException → `convertNSExceptionToJSError` creates Hermes JSI values from the wrong thread → Hermes heap corruption → SIGBUS ~60s after launch. Fixed in `src/providers/NotificationProvider.tsx` with a module-level lazy loader (`_loadNotifications()`) and a 3-second `setTimeout` before `setNotificationHandler`. Never add a top-level `import * as Notifications from 'expo-notifications'`.
- **`expo-updates` native init fires regardless of `checkOnLaunch` setting** — the `"checkOnLaunch": "NEVER"` in `app.json` is JS-layer only; native ObjC still registers and initialises its SQLite database via `dispatch_once` on a GCD background thread at launch. The risk is accepted because OTA updates (`eas update`) are a core feature. If unexplained launch crashes appear, this is the first thing to investigate.
- **`app/(screens)/notification-permission.tsx` has a top-level `import * as Notifications from 'expo-notifications'`** — this violates the pattern but is safe because `EXPO_ROUTER_IMPORT_MODE=lazy` defers screen module evaluation until first navigation (post-login, after TurboModules are registered). Do not remove lazy mode or this becomes a launch crash.
- **Never use `ImageBackground` from React Native** — on iOS 26 New Architecture its image layer does not render (the background shows as transparent, revealing only the overlay colour). Replace with an explicit `View` containing an `expo-image` `<Image style={StyleSheet.absoluteFillObject} contentFit="cover" />` as the first child. The `overflow: 'hidden'` + `borderRadius` on the parent `View` clips it correctly. `app/(tabs)/profile.tsx` is the canonical example.
- **Local `require()` images are completely broken on iOS 26 New Architecture — use base64 data URIs.** Three approaches were tried and all fail silently: (1) `require()` number → RN `Image`; (2) `require()` number → expo-image; (3) `_resolveLocal` (`{ uri: resolveAssetSource(...).uri }`) → expo-image. Root cause: Fabric codegen cannot serialise `require()` numbers for custom Fabric views (expo-image), and SDWebImage silently rejects `file://` app-bundle URIs on iOS 26. Remote `{ uri: 'https://...' }` sources work fine. **The correct fix** is to pre-encode static local images as base64 data URIs in `src/lib/localImages.ts` and use `source={{ uri: dataUri }}` with expo-image:
  ```ts
  // src/lib/localImages.ts
  export const myImage: string = 'data:image/png;base64,iVBOR...';

  // In component:
  import { myImage } from '@/lib/localImages';
  <Image source={{ uri: myImage }} style={...} contentFit="contain" />
  ```
  `src/lib/localImages.ts` currently exports: `indabaHotel`, `indabalodgeRichardsBay`, `indabalodgeGaborone`, `chobeSafariLodge`, `nataLodge` (hotel logos), and `usedLogo` (brand logo used in feed cards). When adding a new static local image, add it to `localImages.ts` — never use `require()` for local image assets on iOS.
- **Never import or top-level `require` a package with native modules.** Use a lazy loader:

```ts
let _mod: typeof import('some-native-pkg') | null = null;
function _loadMod() {
  if (!_mod) _mod = require('some-native-pkg');
  return _mod;
}
```

If the package itself calls `NativeModules.*` at its own module-level code, the lazy-require pattern isn't enough — apply the `patch-package` Proxy pattern instead (see below).

### patch-package patches (applied automatically on `npm install`)

**Critical Metro resolution note:** Metro (RN bundler) prefers the `"react-native"` field in `package.json` over `"main"`. Packages like `react-native-gesture-handler` and `react-native-reanimated` set `"react-native": "src/index.ts"` — Metro loads TypeScript source from `src/`, completely ignoring `lib/commonjs/`. When adding a new patch, always check the package's `package.json` `"react-native"` and `"main"` fields and target whichever one Metro resolves to. Patching the wrong file has zero effect.

17 patches in `patches/` guard packages that access `NativeModules.*` at module-eval time. **`scripts/fix-expo-asset.js`** (run via `postinstall` after patch-package) applies two additional fixes to `expo-asset` that cannot be expressed as a simple patch-package diff:

| Patch | What it fixes |
|-------|--------------|
| `@expo+vector-icons+15.0.3.patch` | Four fixes: (1) `NativeModules.RNVectorIconsManager` eval-time → lazy Proxy; (2) `ensureNativeModuleAvailable` skipped on New Arch; (3) `Ionicons.js` font family name `'ionicons'` → `'Ionicons'` (PostScript name, iOS case-sensitive); (4) `createIconSet.js` initialises `fontIsLoaded=true` on New Arch (`!!global.expo?.modules`) so icons render from UIAppFonts without ExpoFontLoader |
| `@react-native-community+netinfo+11.4.1.patch` | `NativeModules.RNCNetInfo` in `src/internal/nativeModule.ts` eval-time → lazy Proxy. Metro uses `"react-native": "src/index.ts"`. |
| `expo+54.0.33.patch` | `NativeModules.EXDevLauncher` in `Expo.fx.tsx` → `false` (production-only) |
| `expo-asset+12.0.12.patch` | `requireNativeModule('ExpoAsset')` eval-time → lazy Proxy. **Additionally patched by `scripts/fix-expo-asset.js`** (runs in `postinstall` after patch-package): (1) Proxy gains try/catch + null guard + `downloadAsync` fallback (returns URL directly if ExpoAsset TurboModule unavailable); (2) `build/Asset.js` gains an iOS `file://` shortcut — marks embedded assets `downloaded=true` immediately, skipping `downloadAsync()` entirely (mirrors the Android drawable shortcut). |
| `expo-constants+18.0.13.patch` | `NativeModules.EXDevLauncher` block removed; `getManifest()` gains lazy `_nativeInitAttempted` retry guard |
| `expo-font+14.0.11.patch` | `requireNativeModule('ExpoFontLoader')` in `build/ExpoFontLoader.js` — `typeof window === 'undefined'` is `false` in RN/Hermes so the ternary takes the native branch at eval time → lazy Proxy |
| `expo-image+3.0.11.patch` | `requireNativeModule('ExpoImage')` in `src/ImageModule.ts` eval-time → lazy Proxy. Metro uses `"main": "src/index.ts"` (no `react-native` field). |
| `expo-linking+8.0.11.patch` | `requireNativeModule('ExpoLinking')` eval-time → lazy Proxy |
| `expo-modules-core+3.0.29.patch` | `NativeModulesProxy.native.ts` — critical New Arch detection rule: use `global.expo?.modules` (registry existence), NOT `global.expo?.modules?.NativeModulesProxy` (may be null even on New Arch) |
| `expo-router+6.0.23.patch` | `splash.js`: lazy `_getSplashModule()` + `_splashHidden` guard (re-dispatching void `hide()` after dismiss → SIGABRT); `statusbar.js`: lazy `canOverrideStatusBarBehavior` getter |
| `expo-system-ui+6.0.9.patch` | `requireNativeModule('ExpoSystemUI')` in `build/ExpoSystemUI.js` eval-time → lazy Proxy. Metro uses `"main": "build/SystemUI.js"` (no `react-native` field). |
| `react-native-gesture-handler+2.28.0.patch` | `TurboModuleRegistry.getEnforcing('RNGestureHandlerModule')` in `src/specs/NativeRNGestureHandlerModule.ts` eval-time → lazy Proxy. **Targets `src/` because Metro uses `"react-native": "src/index.ts"` — `lib/commonjs/` is never loaded.** |
| `react-native-reanimated+4.1.6.patch` | `TurboModuleRegistry.get('ReanimatedModule')` in `src/specs/NativeReanimatedModule.ts` eval-time → lazy Proxy. Metro uses `"react-native": "src/index"`. |
| `react-native-safe-area-context+5.6.2.patch` | `TurboModuleRegistry.get('RNCSafeAreaContext')` eval-time → lazy Proxy; `initialWindowMetrics` set to `null` (safe: `SafeAreaProvider` fills it via `onInsetsChange`) |
| `react-native-screens+4.16.0.patch` | `TurboModuleRegistry.get('RNSModule')` in `src/fabric/NativeScreensModule.ts` eval-time → lazy Proxy. Pulled in by expo-router → react-navigation → react-native-screens. Metro uses `"react-native": "src/index"`. |
| `react-native-svg+15.12.1.patch` | `TurboModuleRegistry.getEnforcing('RNSVGSvgViewModule')` in `src/fabric/NativeSvgViewModule.ts` eval-time → lazy Proxy. Metro uses `"react-native": "src/index.ts"`. |
| `react-native-worklets+0.5.1.patch` | `TurboModuleRegistry.get('WorkletsModule')` in `src/specs/NativeWorkletsModule.ts` eval-time → lazy Proxy. Pulled in by react-native-reanimated v4. Metro uses `"react-native": "./src/index"`. |

**Adding a new package with native modules:** follow the lazy-require pattern above. If the package accesses `NativeModules.*` in its own module-level code, apply the Proxy pattern via `patch-package`: edit the file in `node_modules/`, then run `npx patch-package <package-name>`.

### expo-updates SIGABRT risk

`expo-updates` v29 (installed) initialises its SQLite database via a `dispatch_once` block on a background GCD thread at app launch — even with a `runtimeVersion` policy configured. On New Architecture this is a **potential SIGABRT**. The package is kept because OTA updates (`eas update`) are a core feature. If iOS launch crashes appear with no obvious JS cause, this is the first thing to investigate. There is no simple fix other than removing `expo-updates` (which would break OTA) or waiting for Expo to patch the native init sequence.

### Pre-submission build gate

**Run a preview build before every store submission.** Preview builds are production-equivalent (Hermes bytecode, New Architecture / Bridgeless, `EXPO_ROUTER_IMPORT_MODE=lazy`) and catch crashes that the Expo dev client never sees.

```bash
eas build --profile preview --platform all --clear-cache
```

**`EXPO_ROUTER_IMPORT_MODE=lazy`** is set in `eas.json` env for both preview and production builds. This makes Expo Router defer loading all route modules until first navigation, reducing the module-eval-time surface to only `app/_layout.tsx` and the initial route. Do not add `$VAR` references to eas.json env blocks — EAS build servers resolve them literally (no shell env expansion). Only static string values belong there.

**Always use `--clear-cache` for iOS builds.** EAS fingerprints `package.json`, `app.json`, and plugin files — JS-only changes reuse a cached native binary that may predate patch fixes. `--clear-cache` forces a full native recompile so all patches are included.

- Test on iOS 26 (current production OS) before submitting to App Store.
- Any new SIGABRT or hang at launch is almost certainly another package accessing `NativeModules.<anything>` at module-eval time — apply the lazy-Proxy `patch-package` pattern.

---

## Deployment

**Admin dashboard** is deployed as a standalone Vercel project under the `indabacares` GitHub/Vercel account, served at `indabacares.co.za`.

- Vercel root directory must be set to `admin`, framework preset `Next.js`
- `.vercelignore` uses `/`-prefixed paths (`/src`, `/app`, `/supabase`) to exclude mobile-only root dirs without accidentally stripping `admin/src/`
- Git commits must be authored by the account linked to the Vercel project (`hr@indabahotel.co.za`) — Hobby plan blocks deployments from unrecognised commit authors on private repos
- Admin env vars required in Vercel: `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_ANON_KEY`, `SUPABASE_SERVICE_ROLE_KEY`, `RESEND_API_KEY`, `RESEND_FROM_DOMAIN`

**Mobile — Android Play Store:**

- Live in production (South Africa + Botswana). **Each Play Console track (Alpha, Production, etc.) requires its own country/region list** — countries added to Alpha are not inherited by Production; set them separately under Production → Countries / regions before creating a production release.

**Mobile — iOS TestFlight / App Store:**

- Bundle ID: `com.indabacares.app` · EAS Project ID: `2769acb0-54c1-4935-9da1-864c41506d37`
- Credentials managed by EAS (`credentialsSource: "remote"` in `production` profile)
- Always use `--clear-cache` on iOS builds — EAS fingerprint reuse can exclude patch-package fixes
- Build command: `eas build --profile production --platform ios --clear-cache`
- Review build (for App Store submission): `eas build --profile review --platform ios --clear-cache` — inherits production + sets `EXPO_PUBLIC_REVIEW_MODE=true` to show the Try Demo button for Apple reviewers
- Submit to TestFlight: `eas submit --platform ios --latest` (interactive — prompts for Apple ID + 2FA). **Must run in PowerShell or cmd.exe, not bash.** In cmd: `set NODE_TLS_REJECT_UNAUTHORIZED=0 && eas submit --platform ios --latest`. In PowerShell: `$env:NODE_TLS_REJECT_UNAUTHORIZED = "0"; eas submit --platform ios --latest`. Non-interactive submit requires `ascAppId` in `eas.json` submit profile.
- On Windows, EAS CLI and Vercel CLI both require `NODE_TLS_REJECT_UNAUTHORIZED=0` if behind a corporate proxy — SSL certificate chain cannot be verified by Node.js. Use `set VAR=0` in cmd or `$env:VAR = "0"` in PowerShell (bash `$env:` syntax does not work in cmd/bash).
- Privacy policy hosted at `indabacares.co.za/privacy` (required for App Store submission)
- `eas.json` android `buildType` must be `"app-bundle"` (not `"aab"`) — EAS validation rejects `"aab"`
- Vercel CLI: run `npx vercel --prod` from the **repo root** (not from `admin/`) — the Vercel project has `rootDirectory: admin` set, so running from inside `admin/` doubles the path. The `.vercel` config folder lives at the repo root.
- TestFlight testers: internal testing (up to 100) requires no Beta App Review — add testers by Apple ID in App Store Connect → TestFlight → Internal Testing

**App Store Connect metadata required before submission** (one-time setup):
- Copyright: `© 2025 Indaba Hospitality Group` (App Store Connect → Version → Copyright field)
- Content Rights: App Information → Content Rights → "Does not contain third-party content"
- Primary Category: Business (App Information → Category)
- App Privacy: complete the data collection questionnaire (Contact Info, Identifiers, Usage Data — all linked to identity, not sold)
- Pricing: Free (Tier 0) under Pricing and Availability

**Current submission state (2026-06-21):** Build 20 (`review` profile, build number 20) submitted to Apple for review. Build 21 (`review` profile, build number 21, includes base64 logo fix + purple leaderboard header) is in TestFlight ready to submit if build 20 is rejected.
