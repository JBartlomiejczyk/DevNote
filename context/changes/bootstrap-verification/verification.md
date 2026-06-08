---
bootstrapped_at: 2026-06-08T12:00:00Z
starter_id: dotnet
starter_name: ".NET (ASP.NET Core webapi)"
project_name: dev-note
language_family: dotnet
package_manager: dotnet
cwd_strategy: subdir-then-move
bootstrapper_confidence: verified
phase_3_status: ok
audit_command: "dotnet list package --vulnerable --include-transitive"
---

## Hand-off

```yaml
starter_id: dotnet
package_manager: dotnet
project_name: dev-note
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: railway
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: true
  has_background_jobs: false
```

### Why this stack

ASP.NET Core webapi is the vetted .NET starter for DevNote's backend. It ships strong typing via C#, convention-based project structure, dependency injection, OpenAPI generation, and Entity Framework Core for data access — all agent-friendly qualities. Auth integrates via ASP.NET Core Identity, and AI/LLM calls for contextual helper questions and classification are straightforward HTTP client usage. The starter scaffolds the API layer; Blazor will be added manually for the wizard UI. Railway deployment works via Dockerfile. Bootstrapper confidence is verified — scaffolding has been tested end-to-end.

## Pre-scaffold verification

| Signal             | Value                              | Severity | Notes                              |
| ------------------ | ---------------------------------- | -------- | ---------------------------------- |
| npm package        | not run                            | —        | non-JS starter; npm check skipped  |
| GitHub repo        | not run                            | —        | docs_url is not a GitHub URL       |

No recency signals available for this starter. .NET SDK 9.0.101 detected locally.

## Scaffold log

**Resolved invocation**: `dotnet new webapi -n .bootstrap-scaffold --no-restore`
**Strategy**: subdir-then-move
**Exit code**: 0
**Files moved**: 6 (Program.cs, dev-note.csproj, appsettings.json, appsettings.Development.json, .bootstrap-scaffold.http, Properties/launchSettings.json)
**Conflicts (.scaffold siblings)**: none
**.gitignore handling**: absent in scaffold
**.bootstrap-scaffold cleanup**: deleted

## Post-scaffold audit

**Tool**: `dotnet list package --vulnerable --include-transitive`
**Summary**: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
**Direct vs transitive**: no vulnerabilities found in either category

Clean dependency tree. No advisories.

## Hints recorded but not acted on

| Hint                       | Value                              |
| -------------------------- | ---------------------------------- |
| bootstrapper_confidence    | verified                           |
| quality_override           | false                              |
| path_taken                 | standard                           |
| self_check_answers         | null                               |
| team_size                  | solo                               |
| deployment_target          | railway                            |
| ci_provider                | github-actions                     |
| ci_default_flow            | auto-deploy-on-merge               |
| has_auth                   | true                               |
| has_payments               | false                              |
| has_realtime               | false                              |
| has_ai                     | true                               |
| has_background_jobs        | false                              |

## Next steps

Next: a future skill will set up agent context (CLAUDE.md, AGENTS.md). For now, your project is scaffolded and verified — happy hacking.

Useful manual steps in the meantime:
- `git init` (if you have not already) to start your own repo history.
- Review any `.scaffold` siblings the conflict policy created and decide which version of each file to keep.
- Address audit findings per your project's risk tolerance — the full breakdown is in this log.
- Add Blazor for the wizard UI (the webapi template is API-only).
