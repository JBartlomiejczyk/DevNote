# Repository Guidelines

## Project Overview

DevNote is a web application that helps developers structure conversations with non-technical stakeholders. It provides an 8-section wizard for exploring business problems, classifies them (A/B/C), and generates structured summaries with next steps. The goal: surface the simplest viable solution path, not default to "build an app."

- **Stack**: ASP.NET Core 9 webapi + Blazor Server (UI)
- **PRD**: @context/foundation/prd.md
- **Tech stack decision**: @context/foundation/tech-stack.md

## Commands

```bash
dotnet restore          # restore packages
dotnet build            # compile
dotnet run              # start dev server (http://localhost:5275)
dotnet test DevNote.Tests/DevNote.Tests.csproj   # run all 37 tests (unit, component, integration)
dotnet format --verify-no-changes --no-restore   # verify format before committing
dotnet list package --vulnerable  # security audit
```

## Quality Gates

Two layers enforce quality in this project:

### Local (post-edit, agent loop)

Fires automatically after every `Write`/`Edit` tool use on `.cs`/`.razor`/`.cshtml` files.
Config: `.github/hooks/post-edit-dotnet-format.json`.

| Step | Script | Timeout | What it does |
|------|--------|---------|--------------|
| 1. Format | `post-edit-dotnet-format.ps1` | 10 s | Auto-fixes style in the edited file |
| 2. Build | `post-edit-dotnet-typecheck.ps1` | 30 s | Full project build — compile errors surface immediately |
| 3. Test | `post-edit-dotnet-test.ps1` | 120 s | All 37 tests — regressions caught before commit |

### CI (GitHub Actions, on PR)

Workflow: `.github/workflows/ci.yml` — triggers on every PR to `master`.

| Step | Command | Gate |
|------|---------|------|
| Format check | `dotnet format --verify-no-changes` | blocks if any file needs reformatting |
| Build | `dotnet build --configuration Release` | blocks if compile fails |
| Test | `dotnet test DevNote.Tests/DevNote.Tests.csproj` | blocks if any test fails |

`deploy.yml` (push-to-master) is separate — it runs after merge and deploys to Railway.

## Architecture (target state)

The project is in early scaffold state. The target architecture:- **API layer**: ASP.NET Core minimal APIs or controllers
- **UI layer**: Blazor Server
- **Auth**: ASP.NET Core Identity (email/password)
- **Data**: Entity Framework Core + PostgreSQL
- **AI/LLM integration**: HTTP client calls to external APIs (contextual questions, classification)
- **Deployment**: Railway via Dockerfile

## Conventions

- Project settings (target framework, nullable, implicit usings): see @dev-note.csproj
- Use **minimal API** style unless controller complexity warrants the switch
- Keep `context/` directory untouched — it holds project metadata (PRD, tech-stack, verification logs)

## Feature Scope

See @context/foundation/prd.md § Functional Requirements for the full feature list and priorities.

## Project-Specific Traps

1. The starter template includes a sample `/weatherforecast` endpoint in `Program.cs` — remove it when adding real endpoints.
2. Railway deployment requires a `Dockerfile` at repo root — ✓ already created.

## Context Directory Structure

```
context/
  foundation/    # PRD, tech-stack decision, shape notes
  changes/       # bootstrap verification logs
  archive/       # completed changes (immutable)
```

Do not write to `context/archive/`. It is append-only via the workflow chain.
