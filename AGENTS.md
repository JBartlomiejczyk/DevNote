# Repository Guidelines

## Project Overview

DevNote is a web application that helps developers structure conversations with non-technical stakeholders. It provides an 8-section wizard for exploring business problems, classifies them (A/B/C), and generates structured summaries with next steps. The goal: surface the simplest viable solution path, not default to "build an app."

- **Stack**: ASP.NET Core 9 webapi (backend) + Blazor (UI, to be added)
- **PRD**: @context/foundation/prd.md
- **Tech stack decision**: @context/foundation/tech-stack.md

## Commands

```bash
dotnet restore          # restore packages
dotnet build            # compile
dotnet run              # start dev server (http://localhost:5275)
dotnet test             # run tests (test project not yet created)
dotnet list package --vulnerable  # security audit
```

## Architecture (target state)

The project is in early scaffold state. The target architecture:

- **API layer**: ASP.NET Core minimal APIs or controllers
- **UI layer**: Blazor Server or WASM (to be added)
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
2. Railway deployment requires a `Dockerfile` at repo root — not yet created.
3. Blazor UI is not scaffolded yet — the current template is API-only.

## Context Directory Structure

```
context/
  foundation/    # PRD, tech-stack decision, shape notes
  changes/       # bootstrap verification logs
  archive/       # completed changes (immutable)
```

Do not write to `context/archive/`. It is append-only via the workflow chain.
