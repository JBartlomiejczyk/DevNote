---
project: dev-note
researched_at: 2026-06-08
recommended_platform: Railway
runner_up: Fly.io
context_type: mvp
tech_stack:
  language: C#
  framework: ASP.NET Core 9
  runtime: .NET 9.0
---

## Recommendation

**Deploy on Railway.**

Railway is the strongest fit for DevNote's MVP: it supports .NET 9 via Docker with co-located PostgreSQL, Redis, and S3-compatible object storage on a single platform, has an official MCP server for agent-driven operations, and the developer already has hands-on experience with it. The DX-first workflow (push-to-deploy, one-command CLI, integrated logs) matches the "prioritize developer experience" constraint, and the single-region architecture aligns with the app's geographic needs.

## Platform Comparison

| Platform | CLI-first | Managed/Serverless | Agent-readable docs | Stable deploy API | MCP / Integration | Total |
|---|---|---|---|---|---|---|
| **Railway** | Pass | Pass | Pass | Pass | Pass | 5/5 |
| **Fly.io** | Pass | Pass | Pass | Pass | Fail | 4/5 |
| **Render** | Partial | Pass | Partial | Pass | Pass | 3.5/5 |

**Hard-filtered (dropped):**
- Vercel — no persistent process support (Q1 = persistent connections required)
- Netlify — no persistent process support (same)
- Cloudflare Workers — no .NET runtime support (tech stack hard constraint)

### Shortlisted Platforms

#### 1. Railway (Recommended)

Railway scores Pass on all five agent-friendly criteria. Its `railway` CLI covers the full deploy/logs/rollback loop. Documentation is published as markdown on GitHub with `llms.txt` and `llms-full.txt`. The official MCP server enables structured agent access to services, deployments, and logs. Co-located PostgreSQL, Redis, and S3-compatible Storage Buckets mean the entire data layer lives on one platform — matching the co-location preference. The developer's existing familiarity eliminates onboarding friction.

#### 2. Fly.io

Fly.io offers the lowest cost at low traffic (~$2–4/month with autostop/autostart) and excellent `flyctl` CLI with structured JSON output. Persistent processes and WebSockets are native. However, it lacks an official MCP server, relies on third-party extensions for Redis (Upstash) and object storage (Tigris), and has no permanent free tier — only a limited trial. The managed Postgres offering is solid but newer than Railway's.

#### 3. Render

Render provides a capable MCP server and managed PostgreSQL with PITR, but lacks native object storage entirely (must use external S3/R2). Its docs are not published as raw markdown on GitHub, making agent access harder. The CLI lacks a single-command rollback, and the free tier's 15-minute spin-down creates unacceptable cold starts (~1 min) for .NET apps. Baseline cost ($13/mo for Starter + Postgres) is higher than Railway or Fly.io for the same workload.

## Anti-Bias Cross-Check: Railway

### Devil's Advocate — Weaknesses

1. **No Railpack for .NET** — Must maintain a multi-stage Dockerfile manually. Any .NET SDK upgrade requires Dockerfile updates; there's no zero-config path.
2. **Short image retention window** — 72h on Hobby, 120h on Pro. Regressions discovered after the window cannot be rolled back via the platform.
3. **$5/month baseline on Hobby** — Unlike Fly.io's per-second billing that drops below $3/mo for idle apps, Railway charges $5 flat regardless of usage.
4. **No autostop/autostart** — Services run 24/7; for a low-traffic MVP, you pay for idle compute.
5. **15-minute max request duration** — Long AI/LLM calls with retries or chained upstream timeouts risk termination.

### Pre-Mortem — How This Could Fail

Six months after deploying DevNote on Railway, costs crept to $20–25/month as PostgreSQL, Redis, and object storage were added — manageable but higher than the initial $5/mo expectation for a product with 12 active users. The real disaster was operational: a bad migration shipped on a Friday, and by Monday the 72-hour rollback window had expired. The team had no artifact to revert to and spent 4 hours manually fixing production data. Meanwhile, the Dockerfile grew stale — a routine .NET 9 patch broke the multi-stage build, and because Railway has no buildpack path for .NET, the fix required understanding Docker layer caching. The MCP server couldn't trigger deploys or rollbacks, so the recovery loop required manual CLI work during the incident.

### Unknown Unknowns

- Railway's health check defaults may not match ASP.NET Core's startup time — if the app takes >5s to warm up (EF Core migrations, DI container), Railway may restart it in a loop. Configure `RAILWAY_HEALTHCHECK_TIMEOUT`.
- Private networking uses IPv6 internally — some .NET HTTP clients may not resolve IPv6 unless Kestrel is explicitly configured for dual-stack binding.
- Railway's GitHub integration triggers deploy on every push to the connected branch — there's no built-in "deploy only after CI passes" gate without wiring GitHub Actions to the Railway API.
- `railway down` removes the latest deployment but doesn't restore the previous one — it's a delete, not a rollback. True rollback requires `railway redeploy` with a deployment ID from `railway deployment list`.

## Operational Story

- **Preview deploys**: Railway creates a preview environment per PR automatically (Pro plan) or via manual environment creation (Hobby). Preview URLs follow the pattern `<service>-<env>.up.railway.app`. No access protection by default — add auth middleware or restrict via environment variables.
- **Secrets**: Environment variables stored in Railway's project-level vault, scoped per environment (production/staging/preview). Readable via `railway variables list`. Rotation: update via CLI or dashboard; redeploy triggers automatically. GitHub Secrets not required — Railway manages its own vault.
- **Rollback**: `railway deployment list` → find target deployment ID → `railway redeploy <deployment-id>`. Typical time-to-revert: ~30–60s (image pull + health check). Caveat: database migrations do not roll back automatically — maintain down-migrations.
- **Approval**: Human-only actions: delete a project, drop a database, change billing plan, rotate the project token. Agent-safe actions: deploy, redeploy, read logs, read/write environment variables, create preview environments.
- **Logs**: `railway logs` (live stream), `railway logs --build` (build output). MCP server exposes `get_service_logs` tool for structured log queries. JSON output via `--json` flag.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| Rollback window expires before regression is caught | Devil's advocate | Medium | High | Pin CI to run smoke tests on deploy; keep manual DB backups outside Railway's retention window |
| Monthly cost exceeds expectations as services are added | Pre-mortem | Medium | Low | Set Railway spend alerts; review monthly cost vs. traffic ratio |
| Dockerfile breaks on .NET patch upgrade | Pre-mortem | Medium | Medium | Pin SDK version in Dockerfile; test builds in CI before deploy |
| Health check timeout kills slow-starting app | Unknown unknowns | Medium | Medium | Set `RAILWAY_HEALTHCHECK_TIMEOUT=30` in env vars; optimize startup path |
| IPv6 private networking issues with .NET HTTP clients | Unknown unknowns | Low | Medium | Configure Kestrel dual-stack; test internal service calls during integration testing |
| Auto-deploy without CI gate ships broken code | Unknown unknowns | Medium | High | Wire GitHub Actions: run tests → on success call `railway redeploy` via API; disconnect auto-deploy |
| 15-min request timeout kills long LLM classification calls | Devil's advocate | Low | Medium | Implement client-side timeout + retry; consider background job pattern for classification |

## Getting Started

1. **Install the Railway CLI**: `npm i -g @railway/cli` (or `scoop install railway` on Windows)
2. **Login**: `railway login`
3. **Create project**: `railway init` (in the DevNote repo root)
4. **Add a Dockerfile** to the repo root — multi-stage build using `mcr.microsoft.com/dotnet/sdk:9.0` and `mcr.microsoft.com/dotnet/aspnet:9.0`. Set `ENV ASPNETCORE_URLS=http://+:${PORT}`.
5. **Provision PostgreSQL**: `railway add --database postgres`
6. **Deploy**: `railway up` (or connect GitHub repo for push-to-deploy)
7. **Verify**: `railway logs` to confirm the app started and health checks pass

## Out of Scope

The following were not evaluated in this research:
- Docker image configuration (Dockerfile writing)
- CI/CD pipeline setup (GitHub Actions workflow)
- Production-scale architecture (multi-region, HA, DR)
