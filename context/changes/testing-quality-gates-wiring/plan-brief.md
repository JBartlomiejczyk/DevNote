# Quality-Gates Wiring — Plan Brief

> Full plan: `context/changes/testing-quality-gates-wiring/plan.md`
> Research: `context/changes/testing-quality-gates-wiring/research.md`

## What & Why

Phases 1–3 built 37 tests but left two gaps: no PR gate in CI (a breaking change can
reach `master` silently) and no test step in the local post-edit hook (a test-breaking
edit is only caught after CI runs, not at edit time). Phase 4 closes both gaps and
documents the resulting gate system in `AGENTS.md`.

## Starting Point

CI has a single workflow (`deploy.yml`) that fires on push to `master` only — no PR
gate, no format check. The local hook chain (`.github/hooks/`) has two steps: auto-format
the edited file and build the project. Neither runs `dotnet test`. `AGENTS.md` describes
a pre-Phase-1 project with no test suite and no Blazor UI.

## Desired End State

Every PR to `master` is blocked by a green `CI / Format, Build, Test` check. Every
`.cs`/`.razor`/`.cshtml` edit in the agent loop triggers format → build → all 37 tests,
surfacing regressions before the next commit. `AGENTS.md` accurately describes the
post-Phase-3 stack, correct commands, and the complete gate system.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| CI structure | New `ci.yml`, leave `deploy.yml` untouched | Single-responsibility files; no conditional sprawl | Plan |
| Local hook test scope | All 37 tests (unit + component + integration) | Full regression surface; matches test-plan §5 intent | Plan |
| `--no-build` on test steps | Never | Phase 1 impl-review: clean CI agents skip test-project build when outputs absent | Research |
| Format hook behaviour vs CI | Local hook: reformat in place; CI: `--verify-no-changes` | Hook heals silently; CI proves correctness | Research |
| AGENTS.md scope | Comprehensive: fix stale lines + add Quality Gates section | Stale docs cause agents to operate on false assumptions | Plan |

## Scope

**In scope:**
- `.github/workflows/ci.yml` (new) — PR gate: format check → build → test
- `.github/hooks/post-edit-dotnet-test.ps1` (new) — test hook script
- `.github/hooks/post-edit-dotnet-format.json` — third entry (120 s timeout)
- `AGENTS.md` — comprehensive update

**Out of scope:**
- `deploy.yml` changes
- `.editorconfig` creation
- `global.json` SDK pinning
- Coverage upload to CI (`coverlet.collector` deferred)
- Git `pre-commit` hook
- Test-results artifact upload

## Architecture / Approach

Two independent enforcement layers, implemented in phase order so each is verified before
the next is added. Phase 1 creates the CI gate and confirms the format baseline is clean.
Phase 2 appends the test step to the existing local hook chain. Phase 3 documents both.
No shared state between layers — they are independent paths to the same outcome: a
regression cannot merge or be committed silently.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. CI quality-gate workflow | `ci.yml` blocks PRs on format/build/test failure | `dotnet format --verify-no-changes` may fail on existing files (pre-flight must run first) |
| 2. Local hook test step | All 37 tests fire after every `.cs` edit | Hook activation must be verified manually (not confirmed by in-repo config) |
| 3. AGENTS.md comprehensive update | Accurate docs for agents and developers | Stale content easy to miss; needs full read-through |

**Prerequisites:** All 37 tests pass on `master` (`dotnet test DevNote.Tests/DevNote.Tests.csproj` exits 0).  
**Estimated effort:** ~1 session; three small files to create/modify + documentation update.

## Open Risks & Assumptions

- The post-edit hook activation mechanism is not documented in any in-repo file (no `.vscode/settings.json`, no `.copilot*` config). The hooks are assumed active from a prior phase; Phase 2 includes manual verification that they fire.
- `dotnet format --verify-no-changes` uses Roslyn defaults (no `.editorconfig`). Pre-flight in Phase 1 confirms no existing file triggers a violation before the gate is wired.

## Success Criteria (Summary)

- Opening a PR with a deliberate format violation shows a failing `CI / Format, Build, Test` check; fixing it turns the check green.
- Making a `.cs` edit in the agent loop produces agent-console output ending with `37 passed, 0 failed`.
- `AGENTS.md` contains no stale lines and a correct Quality Gates section.
