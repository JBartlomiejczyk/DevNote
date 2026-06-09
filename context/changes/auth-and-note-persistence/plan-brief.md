# Auth & Note Persistence — Plan Brief

> Full plan: `context/changes/auth-and-note-persistence/plan.md`

## What & Why

Add user authentication (email/password registration, login, logout, password reset) and persistent note storage to DevNote. Currently the wizard is stateless — data is lost on page refresh. This change enables multi-session usage and per-user data isolation, which are prerequisites for market-feedback validation with real meeting conversations (PRD: FR-001, FR-002, US-01).

## Starting Point

DevNote is a Blazor Server app with an 8-section wizard that calls Azure OpenAI for A/B/C classification. All state lives in a scoped in-memory service (`WizardStateService`). There is no database, no auth, and no persistence layer. The app deploys to Railway via Dockerfile but has no managed database provisioned.

## Desired End State

A user can register, log in, and fill the wizard. On classification, the wizard data + result are saved as a `ConversationNote` in PostgreSQL. Unauthenticated users see only the login page. Password reset works via email. The app runs against Railway-managed PostgreSQL in production and Docker PostgreSQL locally.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Anonymous access | Login required before wizard | Simpler flow, no orphan data or session migration complexity | Plan |
| Auth UI approach | Custom Blazor pages (Polish) | Full control over UX, consistent language and styling with existing app | Plan |
| Auth feature scope | Register + Login + Logout + Forgot Password | Password recovery avoids support burden; email confirmation deferred (small user base) | Plan |
| Note entity structure | Single table, all fields inlined | Matches fixed 8-section schema, simple queries, no unnecessary normalization | Plan |
| Save timing | On classification (single write) | No partial saves needed; matches existing UX where result appears after classify | Plan |
| Email infrastructure | SendGrid/Mailgun free tier via SMTP | Zero cost at MVP scale, reliable delivery | Plan |
| Database setup | Railway Postgres (prod) + Docker Postgres (local) | Production parity; Railway one-click provisioning | Plan |
| Session expiry handling | Redirect to login, lose wizard state | Simple, predictable; rare edge case with Blazor Server keep-alive | Plan |

## Scope

**In scope:**
- ASP.NET Core Identity with email/password
- EF Core + PostgreSQL (Npgsql)
- `ConversationNote` entity persisted on classification
- Custom Blazor login/register/forgot-password/reset-password pages (Polish)
- SMTP email sender for password reset
- Docker Compose for local dev PostgreSQL
- Railway PostgreSQL configuration + auto-migration

**Out of scope:**
- Note listing/management UI (S-03)
- Draft auto-save
- Email confirmation on registration
- 2FA, account management
- Role-based authorization
- Preserving wizard state across auth redirects

## Architecture / Approach

Bottom-up layering: EF Core DbContext + entity → Identity on top → Blazor auth pages → wizard persistence integration → email + deploy config. Cookie-based auth with `AuthorizeRouteView` gates all pages. Single `ConversationNote` table stores the full wizard + classification payload per user.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Data Layer Foundation | EF Core + PostgreSQL + ConversationNote entity + migration | Npgsql connection string format issues with Railway |
| 2. Identity & Auth Middleware | Identity registration, cookies, auth pipeline | Blazor Server interactive mode + cookie auth interaction quirks |
| 3. Auth UI Pages | Register, Login, Logout, Forgot/Reset Password (Polish) | SignInManager usage within Blazor Server components (requires static SSR for auth forms) |
| 4. Note Persistence Integration | Wizard → classify → persist note | Getting authenticated user ID within interactive Blazor component |
| 5. Email Sender & Deployment Config | SMTP email, docker-compose, Railway Postgres | Email deliverability; Railway DATABASE_URL parsing |

**Prerequisites:** Railway account with project set up (existing), PostgreSQL add-on provisioned (new), SendGrid or Mailgun account with SMTP credentials.
**Estimated effort:** ~3-4 sessions across 5 phases.

## Open Risks & Assumptions

- Blazor Server interactive mode has known friction with Identity's cookie auth flows — auth pages may need to render as static SSR (`@rendermode` null) while the wizard stays interactive
- Railway's `DATABASE_URL` format may need custom parsing (standard Npgsql connection string vs URI format)
- SendGrid/Mailgun free tier is assumed sufficient (100 emails/day) — if account creation is blocked or rate-limited, password reset won't work

## Success Criteria (Summary)

- New user can register, log in, fill wizard, classify, and see the note persisted in the database
- Unauthenticated access to the wizard redirects to login
- Password reset email is delivered and the reset flow works end-to-end
