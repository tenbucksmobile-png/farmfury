# IndabaCares

Employee recognition and engagement platform. React Native (Expo) mobile app + Next.js admin dashboard + Supabase backend.

## Architecture

```
IndabaCares/
├── app/                    # Expo Router screens (mobile)
│   ├── (tabs)/             # Bottom tab navigator (feed, rewards, profile, etc.)
│   └── (screens)/          # Stack screens (detail views, wallet, orders, etc.)
├── src/                    # Mobile app source
│   ├── api/                # Supabase queries & edge function wrappers
│   ├── components/         # Reusable UI components
│   ├── hooks/              # React Query hooks (feed, rewards, reactions, etc.)
│   ├── lib/                # Supabase client, constants, haptics
│   ├── providers/          # QueryClient, Realtime, Auth providers
│   ├── stores/             # Zustand stores (auth, UI)
│   ├── types/              # TypeScript types (database, API contracts)
│   └── utils/              # Formatting, helpers
├── admin/                  # Next.js 16 admin dashboard
│   └── src/
│       ├── app/            # App Router pages (dashboard, users, rewards, etc.)
│       ├── api/            # Admin queries, mutations, edge function wrappers
│       ├── components/     # shadcn/ui + custom components
│       ├── hooks/          # Admin React Query hooks
│       ├── lib/            # Supabase SSR client, constants, utils
│       ├── stores/         # Admin Zustand stores
│       └── types/          # Admin TypeScript types
└── supabase/
    ├── config.toml         # Supabase project config
    ├── seed.sql            # Demo data for local development
    ├── migrations/         # 10 sequential SQL migrations
    └── functions/          # 14 Deno Edge Functions
        ├── _shared/        # Auth middleware, Supabase clients, audit, notifications
        ├── auth-me/
        ├── auth-signup/
        ├── auth-invite/
        ├── auth-update-role/
        ├── auth-deactivate-user/
        ├── send-recognition/
        ├── boost-recognition/
        ├── submit-mood/
        ├── redeem-reward/
        ├── cancel-redemption/
        ├── manage-redemption/
        ├── evaluate-badges/
        ├── refresh-leaderboard/
        └── reset-budgets/
```

## Prerequisites

| Tool | Version | Install |
|------|---------|---------|
| Node.js | >= 20 | [nodejs.org](https://nodejs.org) |
| npm | >= 10 | ships with Node.js |
| Supabase CLI | >= 2.0 | `npm install -g supabase` |
| Expo CLI | >= 54 | `npm install -g expo-cli` |
| EAS CLI | latest | `npm install -g eas-cli` |
| Docker | latest | [docker.com](https://docker.com) (for local Supabase) |

---

## 1. Supabase Project Setup

### Local Development

```bash
# Start local Supabase (requires Docker)
cd IndabaCares
supabase start

# This runs all 10 migrations and seed.sql automatically.
# Local dashboard: http://localhost:54323
# Local API:       http://localhost:54321
# Local Inbucket:  http://localhost:54324 (email testing)
```

After `supabase start`, note the output:

```
API URL:     http://127.0.0.1:54321
anon key:    eyJhbGciOi...
service_role key: eyJhbGciOi...
```

### Production Setup

1. Create a project at [supabase.com/dashboard](https://supabase.com/dashboard)
2. Note your project ref (e.g., `typfhdrmtusmffxfclfq`)
3. Link and push:

```bash
supabase link --project-ref <your-project-ref>
supabase db push          # applies all migrations
supabase db seed --linked  # seeds demo data (optional, staging only)
```

### Post-Migration Checklist

After migrations are applied, run these in the Supabase SQL Editor:

```sql
-- 1. Enable pg_cron extension (for scheduled jobs)
CREATE EXTENSION IF NOT EXISTS pg_cron;
GRANT USAGE ON SCHEMA cron TO postgres;

-- 2. Schedule daily leaderboard refresh (02:00 UTC)
SELECT cron.schedule('refresh-leaderboard', '0 2 * * *', $$
  SELECT net.http_post(
    url := current_setting('app.settings.supabase_url') || '/functions/v1/refresh-leaderboard',
    headers := jsonb_build_object(
      'Authorization', 'Bearer ' || current_setting('app.settings.service_role_key'),
      'Content-Type', 'application/json'
    )
  )
$$);

-- 3. Schedule monthly budget reset (1st of month, 00:05 UTC)
SELECT cron.schedule('reset-budgets', '5 0 1 * *', $$
  SELECT net.http_post(
    url := current_setting('app.settings.supabase_url') || '/functions/v1/reset-budgets',
    headers := jsonb_build_object(
      'Authorization', 'Bearer ' || current_setting('app.settings.service_role_key'),
      'Content-Type', 'application/json'
    )
  )
$$);

-- 4. Schedule rate limit cleanup (hourly)
SELECT cron.schedule('cleanup-rate-limits', '0 * * * *', $$
  SELECT public.cleanup_rate_limits()
$$);
```

### Storage Buckets

Create these via the Supabase Dashboard (Storage > New Bucket):

| Bucket | Public | Max Size | Allowed MIME |
|--------|--------|----------|-------------|
| `avatars` | Yes | 2MB | image/jpeg, image/png, image/webp |
| `recognition-images` | Yes | 5MB | image/jpeg, image/png, image/webp, image/gif |
| `reward-images` | Yes | 5MB | image/jpeg, image/png, image/webp |

### Realtime Publication

Migration 006 adds 5 tables to the `supabase_realtime` publication. Verify in SQL Editor:

```sql
SELECT * FROM pg_publication_tables WHERE pubname = 'supabase_realtime';
-- Should include: recognitions, reactions, comments, notifications, leaderboard_cache
```

---

## 2. Environment Variables

### Mobile App (`IndabaCares/.env`)

```env
EXPO_PUBLIC_SUPABASE_URL=https://<project-ref>.supabase.co
EXPO_PUBLIC_SUPABASE_ANON_KEY=eyJhbGciOi...
```

### Admin Dashboard (`IndabaCares/admin/.env.local`)

```env
NEXT_PUBLIC_SUPABASE_URL=https://<project-ref>.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=eyJhbGciOi...
```

### Edge Functions (automatic)

Edge Functions automatically receive these from the Supabase project:
- `SUPABASE_URL`
- `SUPABASE_ANON_KEY`
- `SUPABASE_SERVICE_ROLE_KEY`

No manual configuration needed for Edge Functions.

### Production Email (Supabase Dashboard > Auth > SMTP)

| Setting | Value |
|---------|-------|
| Host | `smtp.sendgrid.net` (or your provider) |
| Port | 587 |
| User | `apikey` |
| Password | your SendGrid API key |
| Sender email | `noreply@indabacares.com` |
| Sender name | `IndabaCares` |

### Auth Redirect URLs (Supabase Dashboard > Auth > URL Configuration)

| URL | Purpose |
|-----|---------|
| `https://admin.indabacares.com` | Admin dashboard (production) |
| `https://admin.indabacares.com/**` | Admin OAuth callbacks |
| `indabacares://auth/callback` | Mobile deep link (production) |
| `http://localhost:3000/**` | Admin local dev |
| `exp://localhost:8081/--/auth/callback` | Expo Go local dev |

---

## 3. Seeding Strategy

### Local Development

`supabase db reset` automatically runs `seed.sql` which creates:

- 1 demo company (Acme Corp)
- 4 departments
- 5 thumbs-up recognition types
- 4 company values
- 5 badges
- 2 budget configs (employee + manager)
- 3 reward categories + 6 rewards (physical, digital, experience)
- 3 skill categories + 6 skill indicators

Users are created via the auth-signup Edge Function. After signup, promote to super_admin:

```sql
-- In Supabase Studio SQL Editor:
UPDATE auth.users SET raw_app_meta_data =
  raw_app_meta_data || '{"role":"super_admin"}'::jsonb
WHERE email = 'admin@acme.com';

UPDATE public.profiles SET role = 'super_admin'
WHERE email = 'admin@acme.com';
```

### Staging

```bash
supabase db seed --linked
```

Then create test users via the auth-signup or auth-invite Edge Functions.

### Production

Do NOT run seed.sql. New customers are onboarded via:
1. Admin creates company (future: self-service signup)
2. Super admin uses admin dashboard to configure thumbs-up types, values, badges, budgets, rewards
3. Users invited via `auth-invite` Edge Function (sends email with magic link)

---

## 4. Edge Functions Deployment

### Deploy All Functions

```bash
cd IndabaCares
supabase functions deploy --linked
```

### Deploy Individual Function

```bash
supabase functions deploy send-recognition --linked
supabase functions deploy cancel-redemption --linked
```

### Function Inventory

| Function | Method | Auth | Description |
|----------|--------|------|-------------|
| `auth-me` | GET | user | Returns user profile, company, session state |
| `auth-signup` | POST | anon | User registration (email + password) |
| `auth-invite` | POST | admin | Send email invitation with magic link |
| `auth-update-role` | POST | admin | Change a user's role |
| `auth-deactivate-user` | POST | admin | Deactivate a user account |
| `send-recognition` | POST | user | Send peer-to-peer recognition with stars |
| `boost-recognition` | POST | manager | Manager boosts a recognition (bonus stars) |
| `submit-mood` | POST | user | Submit daily mood check-in (once per day) |
| `redeem-reward` | POST | user | Redeem stars for a reward (atomic) |
| `cancel-redemption` | POST | user | Cancel pending reward order (atomic refund) |
| `manage-redemption` | POST | admin | Approve/prepare/ship/fulfill/reject orders |
| `evaluate-badges` | POST | system | Evaluate and award badges based on thresholds |
| `refresh-leaderboard` | POST | system | Recalculate leaderboard rankings (cron) |
| `reset-budgets` | POST | system | Monthly giving star budget reset (cron) |

### Verify Deployment

```bash
# List deployed functions
supabase functions list --linked

# Test a function
curl -X GET \
  'https://<project-ref>.supabase.co/functions/v1/auth-me' \
  -H 'Authorization: Bearer <user-jwt>' \
  -H 'apikey: <anon-key>'
```

---

## 5. Mobile Build Steps

### Development

```bash
cd IndabaCares
npm install
npx expo start
```

- Press `i` for iOS simulator, `a` for Android emulator
- Scan QR with Expo Go for physical device

### TypeScript Verification

```bash
npm run typecheck   # runs: tsc --noEmit
```

### EAS Build Setup

```bash
# Login to Expo account
eas login

# Configure EAS (creates eas.json)
eas build:configure
```

Create `eas.json` if not present:

```json
{
  "cli": { "version": ">= 15.0.0" },
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal",
      "env": {
        "EXPO_PUBLIC_SUPABASE_URL": "https://<staging-ref>.supabase.co",
        "EXPO_PUBLIC_SUPABASE_ANON_KEY": "eyJ..."
      }
    },
    "preview": {
      "distribution": "internal",
      "env": {
        "EXPO_PUBLIC_SUPABASE_URL": "https://<staging-ref>.supabase.co",
        "EXPO_PUBLIC_SUPABASE_ANON_KEY": "eyJ..."
      }
    },
    "production": {
      "env": {
        "EXPO_PUBLIC_SUPABASE_URL": "https://<prod-ref>.supabase.co",
        "EXPO_PUBLIC_SUPABASE_ANON_KEY": "eyJ..."
      }
    }
  },
  "submit": {
    "production": {
      "ios": {
        "appleId": "your@apple.id",
        "ascAppId": "your-app-store-connect-id",
        "appleTeamId": "YOUR_TEAM_ID"
      },
      "android": {
        "serviceAccountKeyPath": "./google-service-account.json",
        "track": "internal"
      }
    }
  }
}
```

### Build & Submit

```bash
# iOS
eas build --platform ios --profile production
eas submit --platform ios --profile production

# Android
eas build --platform android --profile production
eas submit --platform android --profile production

# Both platforms
eas build --platform all --profile production
```

### OTA Updates (post-release patches)

```bash
eas update --branch production --message "Fix: recognition card layout"
```

---

## 6. Admin Web Deployment

### Local Development

```bash
cd IndabaCares/admin
npm install
npm run dev
# Opens at http://localhost:3000
```

### Build Verification

```bash
npm run build   # Next.js production build
npm run start   # Test production build locally
```

### Deploy to Vercel (recommended)

```bash
# Install Vercel CLI
npm install -g vercel

# Deploy from admin directory
cd IndabaCares/admin
vercel

# Production deploy
vercel --prod
```

**Vercel Environment Variables** (set in Vercel Dashboard > Settings > Environment Variables):

| Variable | Value |
|----------|-------|
| `NEXT_PUBLIC_SUPABASE_URL` | `https://<prod-ref>.supabase.co` |
| `NEXT_PUBLIC_SUPABASE_ANON_KEY` | `eyJhbGciOi...` |

**Vercel Project Settings:**
- Root Directory: `admin`
- Framework Preset: Next.js
- Build Command: `npm run build`
- Output Directory: `.next`

### Alternative: Docker

```dockerfile
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
COPY --from=builder /app/public ./public
EXPOSE 3000
CMD ["node", "server.js"]
```

Add to `admin/next.config.ts` for Docker:

```ts
const nextConfig: NextConfig = {
  output: "standalone",
};
```

### Admin Pages

| Route | Description |
|-------|-------------|
| `/login` | Admin authentication |
| `/` | Dashboard (analytics overview) |
| `/users` | User management (list, invite, roles) |
| `/users/[id]` | User detail (profile, activity) |
| `/recognitions` | Recognition analytics (charts, top senders) |
| `/mood` | Mood analytics (trends, distribution) |
| `/rewards` | Reward catalog (CRUD, physical/digital/experience) |
| `/rewards/categories` | Reward category management |
| `/rewards/redemptions` | Redemption queue (approve/reject/ship/fulfill) |
| `/gamification/thumbs-up-types` | Recognition type configuration |
| `/gamification/company-values` | Company value management |
| `/gamification/badges` | Badge configuration |
| `/gamification/skills` | Skill categories & indicators |
| `/gamification/budgets` | Budget configuration |
| `/settings` | Company settings (name, logo, color) |
| `/audit-logs` | Immutable audit trail |

---

## 7. Monitoring & Logging

### Supabase Dashboard

Access at `https://supabase.com/dashboard/project/<project-ref>`:

| Section | What to Monitor |
|---------|----------------|
| **API** > Logs | PostgREST request logs, slow queries, errors |
| **Auth** > Users | User registrations, failed logins |
| **Edge Functions** > Logs | Function invocations, errors, execution time |
| **Database** > Queries | Active connections, slow query log |
| **Realtime** > Inspector | Active channels, message throughput |
| **Storage** > Usage | Bucket sizes, bandwidth |

### Edge Function Logging

All Edge Functions use structured `console.error()` for failures:

```typescript
// Errors are captured in Supabase Function Logs
console.error("process_redemption failed:", procError);
console.error("Audit log write failed:", error);
console.error("Notification failed for user:", userId, error);
```

View logs:
```bash
supabase functions logs send-recognition --linked
supabase functions logs cancel-redemption --linked
```

### Audit Trail

Every admin action is recorded in the immutable `audit_logs` table:

```sql
-- View recent admin actions
SELECT a.action, a.target_type, a.metadata, a.created_at,
       p.full_name as actor_name
FROM audit_logs a
LEFT JOIN profiles p ON p.id = a.actor_id
ORDER BY a.created_at DESC
LIMIT 50;
```

Audit actions tracked:
- `user.invite`, `user.role_change`, `user.deactivate`
- `redemption.approve`, `redemption.reject`, `redemption.prepare`, `redemption.ship`, `redemption.fulfill`, `redemption.cancel`
- `recognition.boost`

### Health Checks

```sql
-- Active realtime channels
SELECT * FROM pg_stat_activity WHERE application_name LIKE '%realtime%';

-- Recent cron job executions
SELECT * FROM cron.job_run_details ORDER BY start_time DESC LIMIT 10;

-- Database size
SELECT pg_size_pretty(pg_database_size(current_database()));

-- Table sizes
SELECT relname, pg_size_pretty(pg_total_relation_size(relid))
FROM pg_catalog.pg_statio_user_tables
ORDER BY pg_total_relation_size(relid) DESC
LIMIT 10;
```

### Application Monitoring (recommended additions)

| Service | Purpose | Integration |
|---------|---------|-------------|
| [Sentry](https://sentry.io) | Error tracking (mobile + admin) | `@sentry/react-native`, `@sentry/nextjs` |
| [PostHog](https://posthog.com) | Product analytics | `posthog-react-native`, `posthog-js` |
| [Better Uptime](https://betteruptime.com) | Uptime monitoring | Ping Supabase health endpoint |

### Alerts to Configure

Set these up in the Supabase Dashboard or your monitoring tool:

- **Edge Function error rate** > 5% in 5 minutes
- **Database connections** > 80% of pool size
- **Storage** approaching plan limit
- **Auth** spike in failed login attempts (brute force)
- **Realtime** channel count approaching plan limit

---

## 8. Backup Strategy

### Automatic Backups (Supabase Managed)

| Plan | Frequency | Retention |
|------|-----------|-----------|
| Free | None | N/A |
| Pro | Daily | 7 days |
| Team | Daily | 14 days |
| Enterprise | Daily + PITR | 30 days |

Point-in-Time Recovery (PITR) is available on Team/Enterprise plans and allows restoration to any point within the retention window.

### Manual Backups

```bash
# Full database dump (run periodically via cron or CI)
pg_dump \
  -h db.<project-ref>.supabase.co \
  -p 5432 \
  -U postgres \
  -d postgres \
  --no-owner \
  --no-privileges \
  -F custom \
  -f "indabacares-$(date +%Y%m%d-%H%M%S).dump"

# Restore from dump
pg_restore \
  -h db.<project-ref>.supabase.co \
  -p 5432 \
  -U postgres \
  -d postgres \
  --no-owner \
  --no-privileges \
  "indabacares-20260215-120000.dump"
```

### What to Back Up

| Data | Method | Frequency |
|------|--------|-----------|
| Database | Supabase automatic + pg_dump | Daily (auto) + weekly (manual) |
| Storage buckets | Supabase automatic | Daily (auto) |
| Edge Function source | Git repository | Every commit |
| Migrations | Git repository | Every commit |
| Environment variables | Secure vault (1Password/Vault) | On change |
| `eas.json` credentials | Secure vault | On change |

### Backup Verification

Monthly, test your backup restore process:

```bash
# 1. Create a fresh Supabase project for testing
# 2. Restore the latest dump
# 3. Run migrations to verify they're idempotent
# 4. Verify data integrity:
SELECT count(*) FROM profiles;
SELECT count(*) FROM recognitions;
SELECT count(*) FROM star_transactions;
SELECT count(*) FROM redemptions;
```

### Immutable Data

These tables have DELETE/UPDATE triggers that prevent mutation — they serve as their own audit trail:

- `star_transactions` — full star ledger history
- `point_transactions` — full points ledger history
- `audit_logs` — admin action audit trail

---

## Database Migrations

Migrations are applied sequentially:

| # | File | Description |
|---|------|-------------|
| 001 | `001_foundation.sql` | Enums, companies, profiles, departments, helper functions |
| 002 | `002_recognition_engine.sql` | Recognitions, recipients, reactions, comments |
| 003 | `003_immutable_ledgers.sql` | Star transactions, point transactions (append-only) |
| 004 | `004_skills_mood_gamification.sql` | Skills, mood entries, badges, user badges |
| 005 | `005_rewards_notifications_admin.sql` | Rewards, redemptions, notifications, budgets, leaderboard, audit logs |
| 006 | `006_rls_policies.sql` | Row Level Security on all tables + Realtime publication |
| 007 | `007_auth_hardening.sql` | Rate limiting, session management, auth helpers |
| 008 | `008_business_procedures.sql` | Atomic Postgres functions (recognition, redemption, refund, mood, leaderboard, budgets) |
| 009 | `009_badge_helper_functions.sql` | Badge evaluation helpers, cron schedule docs |
| 010 | `010_reward_type_and_cancellation.sql` | Reward types (physical/digital/experience), fulfillment fields, self-cancellation |

### Adding New Migrations

```bash
supabase migration new <description>
# Edit the generated file in supabase/migrations/
supabase db push --linked
```

---

## Security Model

### Authentication
- Email + password via Supabase Auth
- JWT carries `app_metadata.company_id` and `app_metadata.role`
- Tokens expire in 1 hour with automatic refresh
- Sessions stored in expo-secure-store (mobile) / HTTP-only cookies (admin)

### Authorization
- **RLS on every table** — tenant isolation via `company_id = current_company_id()`
- **Role hierarchy**: employee < manager < admin < super_admin
- **Edge Functions** enforce role checks via `withAuth()` middleware
- **Immutable ledgers** — star/point transactions and audit logs cannot be modified

### Rate Limiting
- Application-level: `auth_rate_limits` table + `check_rate_limit()` function
- Supabase-level: Auth rate limits in `config.toml`
- Per-function: e.g., 5 redemptions/hour, 5 recognitions/day

---

## Quick Start (Full Stack)

```bash
# 1. Clone and install
cd IndabaCares
npm install
cd admin && npm install && cd ..

# 2. Start Supabase locally
supabase start

# 3. Update env files with local keys (from supabase start output)
# .env: EXPO_PUBLIC_SUPABASE_URL=http://127.0.0.1:54321
# admin/.env.local: NEXT_PUBLIC_SUPABASE_URL=http://127.0.0.1:54321

# 4. Deploy Edge Functions locally
supabase functions serve

# 5. Start mobile app
npx expo start

# 6. Start admin dashboard (separate terminal)
cd admin && npm run dev

# 7. Create a user: sign up in the app
# 8. Promote to super_admin (see Seeding Strategy section)
# 9. Log into admin dashboard
```
