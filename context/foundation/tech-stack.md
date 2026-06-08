---
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
---

## Why this stack

ASP.NET Core webapi is the vetted .NET starter for DevNote's backend. It ships strong typing via C#, convention-based project structure, dependency injection, OpenAPI generation, and Entity Framework Core for data access — all agent-friendly qualities. Auth integrates via ASP.NET Core Identity, and AI/LLM calls for contextual helper questions and classification are straightforward HTTP client usage. The starter scaffolds the API layer; Blazor will be added manually for the wizard UI. Railway deployment works via Dockerfile. Bootstrapper confidence is verified — scaffolding has been tested end-to-end.
