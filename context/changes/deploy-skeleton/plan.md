# Deploy Skeleton Implementation Plan

## Overview

Set up a multi-stage Dockerfile, a health check endpoint, and a GitHub Actions CI/CD workflow so that the DevNote ASP.NET Core 9 app auto-deploys to Railway on every push to `master` — gated by a successful build step.

## Current State Analysis

- **App**: Minimal ASP.NET Core 9 web API (`Program.cs`) with a single `/weatherforecast` sample endpoint.
- **Build**: Standard `dotnet build`; no tests yet (test project absent).
- **Deploy infrastructure**: None — no Dockerfile, no CI workflow, no Railway project configuration.
- **Git**: Single `master` branch, remote at `https://github.com/JBartlomiejczyk/DevNote.git`.

### Key Discoveries:

- Railway injects a `PORT` environment variable at runtime; Kestrel must bind to `0.0.0.0:${PORT}`. Recommended approach: set `ASPNETCORE_URLS=http://+:${PORT}` as a Railway service variable rather than hard-coding in source.
- Railway health checks default to TCP probe. A dedicated `/healthz` endpoint provides a more reliable application-level signal and prevents restart loops during slow startups.
- `railway up` with a project-scoped `RAILWAY_TOKEN` is the documented CI deploy path. No Railway GitHub integration needed (avoids deploying broken code).
- Infrastructure research recommends setting `RAILWAY_HEALTHCHECK_TIMEOUT=30` to accommodate .NET startup time.

## Desired End State

After this plan is complete:
1. A push to `master` triggers a GitHub Actions workflow that restores, builds, and — on success — deploys the app to Railway using `railway up`.
2. The app runs in a multi-stage Docker container (`sdk:9.0` build → `aspnet:9.0` runtime).
3. Railway confirms the deployment is healthy via a `/healthz` GET endpoint returning 200.
4. The deployed app is accessible at its Railway-assigned public URL.

**Verification**: `curl https://<app>.up.railway.app/healthz` returns HTTP 200 with body `Healthy`.

## What We're NOT Doing

- Provisioning PostgreSQL, Redis, or object storage (future slices S-01/S-02).
- Setting up preview/staging environments or branch-based environments.
- Adding a test step to CI (no test project exists yet — added in S-01).
- Configuring a custom domain.
- Removing the `/weatherforecast` sample endpoint (separate cleanup task).

## Implementation Approach

Three phases in dependency order: containerize the app, make it health-check-ready for Railway, then wire CI/CD. Each phase is independently verifiable.

---

## Phase 1: Dockerfile + .dockerignore

### Overview

Create a multi-stage Dockerfile that compiles the app in a .NET 9 SDK image and produces a lean runtime image. Add `.dockerignore` to avoid bloating the build context.

### Changes Required:

#### 1. Dockerfile

**File**: `Dockerfile` (repo root, new)

**Intent**: Multi-stage build — restore + publish in SDK image, copy output to aspnet runtime image. Expose port 8080 as default; Railway overrides via `PORT` env var at runtime.

**Contract**: Two stages: `build` (from `mcr.microsoft.com/dotnet/sdk:9.0`) and `runtime` (from `mcr.microsoft.com/dotnet/aspnet:9.0`). Entrypoint: `dotnet dev-note.dll`. Layer ordering: copy `*.csproj` → `dotnet restore` → copy all → `dotnet publish`.

#### 2. .dockerignore

**File**: `.dockerignore` (repo root, new)

**Intent**: Exclude build artifacts, IDE folders, git history, and context directory from Docker build context to speed up builds and reduce image size.

**Contract**: Ignore `bin/`, `obj/`, `.git/`, `.github/`, `.vs/`, `.vscode/`, `context/`.

### Success Criteria:

#### Automated Verification:

- Docker image builds successfully: `docker build -t dev-note .`
- Container starts and responds: `docker run -d -p 8080:8080 -e ASPNETCORE_URLS=http://+:8080 dev-note` then `curl http://localhost:8080/weatherforecast` returns JSON

#### Manual Verification:

- Image size is reasonable (< 200 MB for runtime stage)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Health endpoint + PORT binding

### Overview

Add a minimal `/healthz` endpoint to `Program.cs` so Railway can verify the app is ready. Ensure the app respects the `PORT` environment variable Railway injects.

### Changes Required:

#### 1. Health endpoint

**File**: `Program.cs`

**Intent**: Add a `GET /healthz` endpoint that returns HTTP 200 with body `"Healthy"`. This is the simplest possible health check — no database or external dependency probes (those come later when dependencies exist).

**Contract**: `app.MapGet("/healthz", () => Results.Ok("Healthy"))` — placed before `app.Run()`.

#### 2. PORT binding fallback

**File**: `Program.cs`

**Intent**: When the `PORT` environment variable is set (Railway runtime), Kestrel should listen on `0.0.0.0:{PORT}`. In development, existing `launchSettings.json` URLs take precedence. The Railway service variable `ASPNETCORE_URLS=http://+:${PORT}` handles this externally, but a code-level fallback ensures the app works even if the service variable isn't set.

**Contract**: Before `builder.Build()`, read `PORT` env var; if set, call `builder.WebHost.UseUrls($"http://0.0.0.0:{port}")`. Skip if `ASPNETCORE_URLS` is already set (let it take precedence).

#### 3. Remove HTTPS redirection for production container

**File**: `Program.cs`

**Intent**: Railway terminates TLS at the edge. The container runs HTTP-only. `UseHttpsRedirection()` causes redirect loops behind Railway's reverse proxy. Remove it or guard behind `IsDevelopment()`.

**Contract**: Wrap `app.UseHttpsRedirection()` inside `if (app.Environment.IsDevelopment())` block, or remove entirely.

### Success Criteria:

#### Automated Verification:

- `dotnet build` succeeds
- `docker build -t dev-note .` succeeds
- Container responds to health check: `docker run -d -p 8080:8080 -e PORT=8080 dev-note` then `curl http://localhost:8080/healthz` returns 200 and `"Healthy"`

#### Manual Verification:

- `dotnet run` locally still works on http://localhost:5275 (launchSettings.json takes precedence)
- `/healthz` responds when app is accessed locally

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: GitHub Actions workflow

### Overview

Create a CI workflow that runs on push to `master`: restores packages, builds in Release mode, and — if build passes — deploys to Railway via CLI.

### Changes Required:

#### 1. CI/CD workflow file

**File**: `.github/workflows/deploy.yml` (new)

**Intent**: Single job that restores, builds, then deploys to Railway. Uses `actions/checkout@v4`, `actions/setup-dotnet@v4` (dotnet 9.0.x), installs Railway CLI via npm, and runs `railway up`.

**Contract**: Trigger: `push` to `master`. Steps: checkout → setup .NET 9 → `dotnet restore` → `dotnet build --configuration Release --no-restore` → install Railway CLI (`npm i -g @railway/cli`) → `railway up` with env vars `RAILWAY_TOKEN` and `RAILWAY_SERVICE` from GitHub Secrets.

```yaml
# Non-obvious: railway up needs --service and --environment flags when
# the repo is not linked via Railway's GitHub integration.
env:
  RAILWAY_TOKEN: ${{ secrets.RAILWAY_TOKEN }}
run: railway up --service "${{ secrets.RAILWAY_SERVICE }}" --environment production
```

### Success Criteria:

#### Automated Verification:

- Workflow file passes YAML lint (valid GitHub Actions syntax)
- Push to `master` triggers the workflow in GitHub Actions UI
- `dotnet restore` and `dotnet build` steps pass

#### Manual Verification:

- Railway deployment completes successfully (visible in Railway dashboard)
- App is accessible at Railway public URL
- `curl https://<app>.up.railway.app/healthz` returns 200

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None yet (test project absent — added in S-01)

### Integration Tests:

- Docker build + run + curl `/healthz` (Phase 1+2 combined verification)

### Manual Testing Steps:

1. Build Docker image locally and verify it starts
2. Confirm `/healthz` returns 200 in the container
3. Push to master and watch GitHub Actions run
4. Confirm Railway dashboard shows successful deployment
5. Curl the Railway public URL `/healthz`

## Performance Considerations

- Multi-stage Dockerfile keeps runtime image small (~100-150 MB vs ~800 MB with SDK)
- `.dockerignore` prevents unnecessary context upload to Docker daemon
- Railway health check timeout should be set to 30s via service variable `RAILWAY_HEALTHCHECK_TIMEOUT=30` to accommodate .NET cold start

## Migration Notes

No existing infrastructure to migrate from. First-time setup requires:
1. Create Railway project (`railway init` or via dashboard)
2. Add GitHub Secrets: `RAILWAY_TOKEN` (project token from Railway), `RAILWAY_SERVICE` (service name or ID)
3. Set Railway service variable: `ASPNETCORE_URLS=http://+:${PORT}`
4. Set Railway service variable: `RAILWAY_HEALTHCHECK_TIMEOUT=30`

## References

- Infrastructure research: `context/foundation/infrastructure.md`
- Tech stack: `context/foundation/tech-stack.md`
- Roadmap: `context/foundation/roadmap.md` (F-01)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Dockerfile + .dockerignore

#### Automated

- [x] 1.1 Docker image builds successfully — 9ca3991
- [x] 1.2 Container starts and responds to /weatherforecast — 9ca3991

#### Manual

- [x] 1.3 Image size is reasonable (< 200 MB) — 9ca3991

### Phase 2: Health endpoint + PORT binding

#### Automated

- [x] 2.1 dotnet build succeeds — 6bb6f10
- [x] 2.2 Docker build succeeds — 6bb6f10
- [x] 2.3 Container responds to /healthz with 200 — 6bb6f10

#### Manual

- [x] 2.4 dotnet run locally still works on http://localhost:5275 — 6bb6f10
- [x] 2.5 /healthz responds locally — 6bb6f10

### Phase 3: GitHub Actions workflow

#### Automated

- [x] 3.1 Workflow file passes YAML lint — 0e94842
- [x] 3.2 Push triggers workflow in GitHub Actions — 0e94842
- [x] 3.3 dotnet restore and build steps pass — 0e94842

#### Manual

- [x] 3.4 Railway deployment completes successfully — 0e94842
- [x] 3.5 App accessible at Railway public URL — 0e94842
- [x] 3.6 /healthz returns 200 at public URL — 0e94842
