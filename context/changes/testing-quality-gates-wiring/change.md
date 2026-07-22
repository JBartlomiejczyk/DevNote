---
change_id: testing-quality-gates-wiring
title: Wire quality gates for CI and local pre-commit enforcement
status: implementing
created: 2026-07-22
updated: 2026-07-22
archived_at: null
---

## Notes

Phase 4 of context/foundation/test-plan.md — "Quality-gates wiring".
Risks covered: cross-cutting (no specific §2 risk number — this phase locks the floor built by Phases 1–3).
Test types planned: gates (CI YAML + local post-edit hook).
Risk response intent:
- Cross-cutting: prove that format (`dotnet format --verify-no-changes`), build (`dotnet build`), unit + component tests (`dotnet test DevNote.Tests\DevNote.Tests.csproj`), and integration tests all run and must pass before a PR merges; no regression slips through a gap between local and CI environments.
- Local hook: prove that a single agent-loop or developer edit that breaks the build or any test is caught before the next commit, not after CI runs.
