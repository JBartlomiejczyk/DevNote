# Deploy Skeleton — Plan Brief

> Full plan: `context/changes/deploy-skeleton/plan.md`

## What & Why

Add a Dockerfile, health check endpoint, and GitHub Actions CI/CD workflow so DevNote auto-deploys to Railway on every push to `master` — gated by a successful build. This is the foundational infrastructure slice (F-01) that enables production deployment for real-world market-feedback validation.

## Starting Point

A bare ASP.NET Core 9 minimal API scaffold with a single `/weatherforecast` sample endpoint. No Dockerfile, no CI/CD, no Railway configuration, no health checks. Single `master` branch on GitHub.

## Desired End State

Pushing code to `master` triggers a GitHub Actions workflow that builds the app and deploys it to Railway. The app runs in a lean Docker container, exposes a `/healthz` endpoint for Railway's health probes, and is publicly accessible at its Railway-assigned URL.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
|---|---|---|
| Deploy trigger | GitHub Actions gates deploy | Prevents broken code from reaching production; infra research flagged auto-deploy as medium-high risk |
| Health check | Dedicated `/healthz` endpoint | More reliable than TCP probe; prevents restart loops during .NET startup |
| Branch strategy | Deploy from `master` | Simplest for solo dev MVP; no users yet to justify staging |
| CI scope | Build + restore only (no tests) | No test project exists yet; honest signal rather than misleading green check |

## Scope

**In scope:**
- Multi-stage Dockerfile (sdk:9.0 build → aspnet:9.0 runtime)
- `.dockerignore` for build context optimization
- `/healthz` GET endpoint
- PORT environment variable handling for Railway
- GitHub Actions workflow (restore → build → deploy via Railway CLI)

**Out of scope:**
- Database provisioning (PostgreSQL, Redis)
- Preview/staging environments
- Test step in CI (no tests exist)
- Custom domain configuration
- Removing `/weatherforecast` sample endpoint

## Architecture / Approach

```
push to master → GitHub Actions → dotnet restore → dotnet build → railway up → Railway builds Docker image → health check passes → live
```

Railway terminates TLS at the edge; the container runs HTTP-only on the PORT Railway injects.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Dockerfile + .dockerignore | Containerized app with optimized build layers | None — well-known pattern |
| 2. Health endpoint + PORT binding | Railway-ready app that passes health probes | HTTPS redirect loops if not removed |
| 3. GitHub Actions workflow | Automated CI-gated deploy pipeline | Requires manual Railway project setup + secrets |

**Prerequisites:** Railway project created, GitHub Secrets configured (`RAILWAY_TOKEN`, `RAILWAY_SERVICE`), Railway service variable `ASPNETCORE_URLS=http://+:${PORT}` set.
**Estimated effort:** ~1 session across 3 phases.

## Open Risks & Assumptions

- Railway project must be manually created before Phase 3 can deploy (one-time setup)
- `RAILWAY_HEALTHCHECK_TIMEOUT=30` should be set to avoid restart loops during .NET cold start
- No test gate in CI until test project is added in a future slice

## Success Criteria (Summary)

- Push to `master` triggers build → deploy automatically
- App is publicly accessible at Railway URL
- `/healthz` returns HTTP 200, confirming healthy deployment
