---
date: 2026-07-22T11:07:59+02:00
researcher: Copilot
git_commit: 7bb38cd9a60df5ef0234013b99950939324f3f68
branch: master
repository: DevNote
topic: "Quality-gates wiring — CI YAML and local post-edit hook audit"
tags: [research, ci, github-actions, quality-gates, dotnet-format, post-edit-hook, testing]
status: complete
last_updated: 2026-07-22
last_updated_by: Copilot
---

# Research: Quality-gates wiring — CI YAML and local post-edit hook audit

**Date**: 2026-07-22T11:07:59+02:00  
**Researcher**: Copilot  
**Git Commit**: 7bb38cd9a60df5ef0234013b99950939324f3f68  
**Branch**: master  
**Repository**: JBartlomiejczyk/DevNote

## Research Question

Audit the current CI workflow, local post-edit hook configuration, and test project to
determine exactly what Phase 4 ("Quality-gates wiring") needs to add or change to enforce:
format + build + unit/component + integration tests before any PR merges, and a local
post-edit hook that catches regressions before the next commit.

## Summary

Three artifacts govern quality gates today: the CI deploy workflow, a pair of PowerShell
post-edit hooks, and the test project. Each is partially wired — the CI has a test step
but no format check and no PR trigger; the local hooks have a format step and a build step
but no test step; `dotnet test` runs all 37 tests in one sweep with no `--no-build` gap.
Phase 4 needs to (1) add a PR-triggered CI workflow with `dotnet format --verify-no-changes`,
(2) extend the local hook chain to run `dotnet test` after a successful build, and
(3) update `AGENTS.md` which still describes a state before Phase 1 existed. No `.editorconfig`
is present, no `global.json` pins the SDK, and the hook JSON registration mechanism is not
yet resolved by any in-repo config file — that activation path is the principal open question.

---

## Detailed Findings

### CI Workflow — `.github/workflows/deploy.yml`

**File**: `.github/workflows/deploy.yml`

Current trigger and job:

```yaml
on:
  push:
    branches: [master]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: "9.0.x" }
      - run: dotnet restore
      - run: dotnet build --configuration Release --no-restore
      - run: dotnet test DevNote.Tests/DevNote.Tests.csproj --configuration Release
      - run: npm i -g @railway/cli
      - run: railway up --service "${{ secrets.RAILWAY_SERVICE }}" --environment production
        env: { RAILWAY_TOKEN: "${{ secrets.RAILWAY_TOKEN }}" }
```

**Gaps**:

| Gap | Detail |
|-----|--------|
| No PR trigger | `on:` only fires on `push` to `master`. PRs are not gated at all. |
| No `dotnet format --verify-no-changes` | Format correctness is checked only by the local post-edit hook (which reformats, not verifies). A PR carrying a format violation passes CI. |
| Deploy always runs | The deploy steps run even if a test step were added to a PR — a PR CI check must not deploy. |
| No test results upload | `--logger trx --results-directory` is absent; failures are text-only and not surfaced as GitHub check annotations. |

**What already works**:
- `.NET 9.0.x` SDK matches the project target framework (`net9.0`).
- Build uses `--no-restore` (correct — restore runs first).
- Test step does NOT use `--no-build` (correct — impl-review `testing-classification-summary-integrity/reviews/impl-review.md:28-29` warned that `--no-build` can skip test execution on clean agents when test outputs are absent).
- `RAILWAY_TOKEN` and `RAILWAY_SERVICE` secrets already exist for the deploy step.

### Local Post-Edit Hook Chain

**Hook config**: `.github/hooks/post-edit-dotnet-format.json`

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "type": "command",
        "matcher": "Write|Edit",
        "command": "powershell -NoProfile -ExecutionPolicy Bypass -File .github\\hooks\\post-edit-dotnet-format.ps1",
        "timeout": 10
      },
      {
        "type": "command",
        "matcher": "Write|Edit",
        "command": "powershell -NoProfile -ExecutionPolicy Bypass -File .github\\hooks\\post-edit-dotnet-typecheck.ps1",
        "timeout": 30
      }
    ]
  }
}
```

**Hook 1 — `post-edit-dotnet-format.ps1`** (10 s timeout):
- Reads tool payload from stdin, extracts `file_path`.
- Skips non-`.cs`/`.razor`/`.cshtml` files.
- Runs: `dotnet format --include "<relative-path>" --verbosity minimal --no-restore`
- **Behavior**: reformats the file in place (not `--verify-no-changes`). This is correct for
  a local hook — fix silently, never block. The CI gate should use `--verify-no-changes`.

**Hook 2 — `post-edit-dotnet-typecheck.ps1`** (30 s timeout):
- Same payload parsing, same file-type filter.
- Runs: `dotnet build --no-restore --nologo --verbosity minimal` against the whole project.
- **Gap**: builds but does NOT run `dotnet test`. A change that compiles but breaks a test
  is only caught when the developer manually runs tests or when CI runs after commit.

**Activation mechanism** — **open question**:
- The JSON config sits at `.github/hooks/post-edit-dotnet-format.json`.
- No `.vscode/settings.json`, no `.copilot*` config, and no reference to this file from
  `.github/.10x-cli-manifest.json` or `AGENTS.md` was found.
- The `PostToolUse` + `matcher: Write|Edit` format matches the Copilot CLI's workspace hook
  specification, but the path by which the runtime picks up this JSON is not documented in
  any in-repo file. Phase 4 must confirm (or establish) the activation path and document it
  in `AGENTS.md`.

**Git hooks**: `.git/hooks/` contains only the 14 default `.sample` files — **no active git
hooks are installed**. A `pre-commit` hook is not present and was not mentioned in the
Phase 4 change brief; all gate enforcement is through the Copilot CLI post-edit mechanism
and CI.

### Test Project Inventory

**File**: `DevNote.Tests/DevNote.Tests.csproj`

| Package | Version | Role |
|---------|---------|------|
| `xunit` | 2.9.2 | test runner |
| `bunit` | 2.7.2 | Blazor component testing |
| `Microsoft.AspNetCore.Mvc.Testing` | 9.0.0 | WebApplicationFactory integration host |
| `Microsoft.EntityFrameworkCore.InMemory` | 9.0.0 | in-memory DB for tests |
| `NSubstitute` | 5.3.0 | test doubles |
| `coverlet.collector` | 6.0.2 | coverage (not wired to CI yet) |

**Test count**: 37 `[Fact]` tests, 0 `[Theory]` tests, across 8 classes.

| Directory | Class | Tests |
|-----------|-------|-------|
| `Services/` | `ClassificationResponseValidatorTests` | 5 |
| `Services/` | `HelperQuestionsCoordinatorTests` | 5 |
| `Services/` | `NoteServiceTests` | 7 |
| `Services/` | `WizardStateServiceTests` | 2 |
| `Components/` | `WizardSectionTests` | 7 |
| `Components/` | `WizardTests` | 2 |
| `Components/` | `EditNoteTests` | 3 |
| `Integration/` | `AnonymousAccessTests` | 5 |
| `Infrastructure/` | `DevNoteWebApplicationFactoryTests` | 1 |
| **Total** | | **37** |

**Single run command** covers all categories:
```
dotnet test DevNote.Tests/DevNote.Tests.csproj
```

No `--filter` is needed to distinguish unit from integration for gating purposes — both
must pass. Selective filters documented in `test-plan.md §6` are for development-loop speed
only, not for the gate definition.

**No `.runsettings` or `xunit.runner.json`** — test runner uses defaults. This is
sufficient for the gate; no config file additions are required by Phase 4.

### Format and Build Tooling

| Artifact | Status |
|----------|--------|
| `.editorconfig` | **Absent** — `dotnet format` will use Roslyn defaults. No style rules are enforced beyond the formatter's built-in defaults. Phase 4 does not need to create one, but it should be noted. |
| `global.json` | **Absent** — SDK version is not pinned. The CI `actions/setup-dotnet@v4` pins `9.0.x` which is loose; minor SDK updates are silent. |
| `.gitignore` | No hook/script entries — the hooks directory is tracked normally. |
| `Dockerfile` | Uses `sdk:9.0-alpine` for build, `aspnet:9.0-alpine` for runtime. Test project is excluded from the publish output (only the `*.csproj` in root is copied). No test execution in Docker build — this is correct. |

### AGENTS.md Staleness

**File**: `AGENTS.md`

The file was generated during bootstrapping and has not been updated since Phase 1:

| Stale line | Current reality |
|-----------|-----------------|
| `dotnet test  # run tests (test project not yet created)` | 37 tests exist in `DevNote.Tests/DevNote.Tests.csproj` |
| `Stack: ASP.NET Core 9 webapi (backend) + Blazor (UI, to be added)` | Blazor Server is scaffolded and shipped |
| `Project-Specific Traps: Blazor UI is not scaffolded yet` | No longer true |
| No mention of quality gates or post-edit hooks | Phase 4 should add a "Quality Gates" section |

Phase 4 must update `AGENTS.md` as part of its success criteria; otherwise agents reading it
operate with a fundamentally incorrect picture of the project.

---

## Code References

- `.github/workflows/deploy.yml` — sole CI workflow; push-to-master only; no format gate; no PR trigger
- `.github/hooks/post-edit-dotnet-format.json` — hook registration file; PostToolUse on Write|Edit; two hooks wired
- `.github/hooks/post-edit-dotnet-format.ps1` — formats edited file in place (10 s)
- `.github/hooks/post-edit-dotnet-typecheck.ps1` — builds whole project (30 s); no test run
- `DevNote.Tests/DevNote.Tests.csproj` — xUnit 2.9.2 + bUnit 2.7.2 + WebApplicationFactory; 37 Fact tests
- `DevNote.Tests/Infrastructure/DevNoteWebApplicationFactory.cs` — WAF test host
- `DevNote.Tests/Integration/AnonymousAccessTests.cs` — 5 integration tests using WAF
- `DevNote.Tests/Components/` — 12 bUnit component tests
- `DevNote.Tests/Services/` — 19 unit tests
- `AGENTS.md` — stale; describes pre-Phase-1 state

---

## Architecture Insights

**Two-layer gate design** (test-plan §5 intent):
1. **Local (post-edit)**: catches regressions immediately after each file write, before any
   commit. Currently: format (auto-fix) + build. Missing: test run.
2. **CI (PR gate)**: enforces format correctness, build, and all tests on every PR before
   merge. Currently: only exists on push-to-master, not PR. Missing: format step, PR trigger.

**The gap between layers**: a developer (or agent) who edits a `.cs` file today gets
immediate format-and-build feedback but can commit a test-breaking change that only surfaces
when the PR lands in CI — and CI doesn't even have a PR gate, so the breaking change reaches
`master` silently until the next `dotnet test` manual run.

**Format hook behavior vs CI behavior**: the local hook runs `dotnet format --include <file>`
(reformats in place) while CI should run `dotnet format --verify-no-changes` (fails if any
file needs formatting). These are intentionally different: the hook heals, the CI gate proves.

**Test run location in hook**: adding `dotnet test` to the post-edit hook will extend the
chain to approximately 60–120 s per edit on a first run (WebApplicationFactory cold-start
costs ~ 2–5 s; the 37 tests complete quickly thereafter). A dedicated third hook entry with
a 120 s timeout is safer than extending the existing 30 s typecheck hook.

**No `--no-build` in CI test step** (`.github/workflows/deploy.yml:26`): this is intentional
and must be preserved. A separate `dotnet build` step followed by `dotnet test --no-build`
would skip test project build on clean GitHub-hosted runners, as flagged in
`testing-classification-summary-integrity/reviews/impl-review.md:28-29`.

---

## Historical Context (from prior changes)

- `context/changes/testing-classification-summary-integrity/research.md` — Pre-Phase-1 state:
  CI had no `dotnet test` step at all; no test project existed.
- `context/changes/testing-classification-summary-integrity/plan.md` — Phase 3 of that rollout
  added `dotnet test` to `deploy.yml` (commit `7b07f61`). This is the origin of the current
  test step.
- `context/changes/testing-classification-summary-integrity/reviews/impl-review.md:28-29` —
  Warning: using `--no-build` on the test step can silently skip tests on clean CI agents.
  The current workflow correctly omits `--no-build`.
- `context/changes/testing-wizard-state-edit-degraded-mode/plan.md:328` — First mention of
  `dotnet format --verify-no-changes` as a Phase 3 success criterion. This is the source of
  the format-gate requirement that Phase 4 now formalises in CI.
- `context/changes/deploy-skeleton/plan.md` — Original CI workflow creation (commit `0e94842`).
  Established `deploy.yml` with push-to-master trigger and Railway deploy. The PR-gate gap
  has existed since bootstrap.

---

## Related Research

- `context/changes/testing-classification-summary-integrity/research.md` — Phase 1 audit
- `context/changes/testing-auth-ownership-boundary/research.md` — Phase 2 audit
- `context/changes/testing-wizard-state-edit-degraded-mode/research.md` — Phase 3 audit

---

## Open Questions

1. **Hook activation path**: How does the Copilot CLI runtime discover and load
   `.github/hooks/post-edit-dotnet-format.json`? No in-repo config file (`.vscode/settings.json`,
   `.copilot*`, manifest) references it. Phase 4 plan must verify the activation mechanism
   before claiming the hook is "wired" — if the JSON is not loaded, both hook scripts are dead
   letters. The plan should add explicit verification steps (e.g., confirm a test edit triggers
   the hook) and document the activation path in `AGENTS.md`.

2. **Test hook timeout**: Integration tests start a `WebApplicationFactory` host. A 120 s
   timeout is suggested for the test hook entry. Actual cold-start time on the developer
   machine should be measured during Phase 4 implementation to confirm or adjust.

3. **CI format check scope**: `dotnet format --verify-no-changes` without `--include` checks
   all `.cs`/`.razor`/`.cshtml` files. If there are generated files (e.g., EF migrations) that
   do not conform to the formatter's rules, the CI step will fail on every PR. Phase 4 should
   do a trial run locally to confirm no existing file triggers a format violation before wiring
   this as a required gate.

4. **Separate PR workflow vs. conditional deploy.yml**: Two valid approaches exist —
   (a) add `on: pull_request` + conditional `if: github.event_name == 'push'` on deploy steps
   inside the existing `deploy.yml`, or (b) create a new `ci.yml` with only format/build/test.
   Option (b) is cleaner (single-responsibility), avoids conditional sprawl, and keeps
   `deploy.yml` as a pure deploy script. The plan should decide and document.

5. **`global.json` and SDK pinning**: No `global.json` means the developer's local SDK may
   drift from the CI `9.0.x` pin. This is out of Phase 4's scope but worth noting; a
   `global.json` with `"rollForward": "latestMinor"` would align environments.
