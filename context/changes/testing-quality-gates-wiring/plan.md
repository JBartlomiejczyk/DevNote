# Quality-Gates Wiring Implementation Plan

## Overview

Wire the two-layer quality gate defined in `context/foundation/test-plan.md §5`: a new
`ci.yml` PR-triggered workflow (format → build → test) that blocks merges when any check
fails, and a third local post-edit hook that runs all 37 tests immediately after each
successful build — so regressions are caught at edit time, not after CI runs. Finish by
updating `AGENTS.md` comprehensively to reflect the post-Phase-3 project state.

## Current State Analysis

- **CI** (`deploy.yml`): triggers only on `push` to `master`; no PR gate; no format step;
  test step present but deploy always follows. No way to block a bad PR before it reaches
  master.
- **Local post-edit hooks** (`.github/hooks/`): two hooks fire on every `Write`/`Edit`
  tool use — (1) `post-edit-dotnet-format.ps1` reformats the edited file (10 s), (2)
  `post-edit-dotnet-typecheck.ps1` builds the whole project (30 s). Neither runs
  `dotnet test`. A test-breaking edit can be committed undetected.
- **Test project**: 37 `[Fact]` tests across 8 classes in `DevNote.Tests/` — unit (19),
  component/bUnit (12), integration/WAF (5), infra (1). Single command covers all:
  `dotnet test DevNote.Tests/DevNote.Tests.csproj`.
- **`AGENTS.md`**: describes a pre-Phase-1 project — "test project not yet created",
  "Blazor UI not scaffolded yet", no mention of quality gates or post-edit hooks.
- **No `.editorconfig`**: `dotnet format` uses Roslyn defaults. No generated-file
  exclusions exist; a pre-flight local run must confirm no existing file triggers a
  violation before the CI gate is wired.

## Desired End State

- Every PR to `master` is blocked by a GitHub Actions check (`CI / Format, Build, Test`)
  that must pass before merge: format → build → test, in that order.
- Every `Write`/`Edit` tool use on a `.cs`/`.razor`/`.cshtml` file triggers the full
  local chain: format (auto-fix) → build → **all 37 tests** — catching regressions
  before the next commit.
- `AGENTS.md` correctly describes the current stack, commands, quality gates, and hook
  behaviour; no stale lines remain.

**Verification**: open a PR with a deliberate format violation → `ci.yml` check fails.
Revert the violation → check passes. Make a `.cs` edit locally → hook chain fires and
`dotnet test` output is visible in the agent console.

### Key Discoveries

- Existing hooks in `.github/hooks/post-edit-dotnet-format.json` use the `PostToolUse`
  format — same mechanism stays; a third entry is appended (`post-edit-dotnet-test.ps1`,
  120 s timeout). `research.md §Architecture Insights`
- `dotnet test` must NOT use `--no-build` in CI (clean GitHub-hosted runners have no prior
  build output) — established by Phase 1 impl-review warning. Local hook can omit
  `--no-build` too; MSBuild incremental skips rebuild when hook 2 already succeeded.
  `testing-classification-summary-integrity/reviews/impl-review.md:28-29`
- `dotnet format --verify-no-changes` checks all `.cs`/`.razor`/`.cshtml` files by default;
  no `.editorconfig` means Roslyn defaults only. A pre-flight local run in Phase 1 is
  mandatory before committing the CI gate.
- The new `ci.yml` triggers on `pull_request` only; `deploy.yml` stays a pure push-to-master
  deploy script. No conditionals needed in either file.

## What We're NOT Doing

- No `--no-build` flag on any test step (CI or local hook) — established risk.
- No `.editorconfig` creation — out of scope; Roslyn defaults are sufficient for now.
- No `global.json` SDK pinning — out of scope for Phase 4.
- No coverage upload to CI — `coverlet.collector` is present but wiring it is deferred.
- No `--logger trx` / test-results artifact upload — solo project; text output is sufficient.
- No git `pre-commit` hook — all local gating uses the Copilot CLI post-edit mechanism.
- No changes to `deploy.yml` — it remains a push-to-master deploy-only script.

## Implementation Approach

Three independent phases, each verifiable before the next starts:

1. **CI first** — a passing `ci.yml` on `master` proves the format/build/test baseline is
   clean before the local hook chain is extended. If the format pre-flight reveals any
   violations, they are fixed in this phase so Phase 2 starts from a green state.
2. **Local hook** — add the test script and register it; verify by triggering a test edit.
3. **AGENTS.md** — update documentation last so it reflects the fully-wired state from
   Phases 1–2.

## Critical Implementation Details

**`dotnet format` pre-flight**: run `dotnet format --verify-no-changes --no-restore
--verbosity diagnostic` locally before creating `ci.yml`. If any file is reported as
needing changes (exit code 1), run `dotnet format --no-restore` to fix and commit the
clean baseline first. Wiring a CI gate that immediately fails on every PR is worse than
deferring the gate by one commit.

**Hook script pattern**: `post-edit-dotnet-test.ps1` must follow the exact stdin-JSON
parsing pattern of the two existing hook scripts (read `[Console]::In.ReadToEnd()`,
extract `tool_name` and `tool_input.file_path` with the camelCase fallback, skip
non-`.cs`/`.razor`/`.cshtml` extensions). Deviation from this pattern will silently
skip all invocations.

---

## Phase 1: CI quality-gate workflow

### Overview

Run `dotnet format --verify-no-changes` locally to confirm the existing codebase is
format-clean, then create `.github/workflows/ci.yml` that gates every PR with the same
three-step check (format → build → test).

### Changes Required

#### 1. Format pre-flight (local verification — no file change if clean)

**File**: run locally, no file produced unless violations found

**Intent**: confirm `dotnet format --verify-no-changes` exits 0 on the current codebase
before this becomes a required CI gate. If it exits 1, run `dotnet format --no-restore`
to fix violations and commit the clean baseline as part of this phase.

**Contract**: `dotnet format --verify-no-changes --no-restore --verbosity diagnostic`
at the repo root — exit 0 means no violations; exit 1 means at least one file needs
reformatting and must be fixed before proceeding.

#### 2. New CI workflow

**File**: `.github/workflows/ci.yml` (new)

**Intent**: create a PR-triggered workflow that enforces format, build, and test in
strict order — if format fails, build does not run; if build fails, test does not run.
The job name must match `CI / Format, Build, Test` so branch-protection rules can
reference it by exact string.

**Contract**: trigger `on: pull_request` with `branches: [master]`; single job
`ci` on `ubuntu-latest`; steps in this order:

```yaml
- uses: actions/checkout@v4
- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: "9.0.x"
- name: Restore
  run: dotnet restore
- name: Format check
  run: dotnet format --verify-no-changes --no-restore --verbosity minimal
- name: Build
  run: dotnet build --configuration Release --no-restore
- name: Test
  run: dotnet test DevNote.Tests/DevNote.Tests.csproj --configuration Release --no-restore
```

No deploy steps. No `--no-build` on the test step. `--no-restore` on build/test because
the `Restore` step already ran. `--configuration Release` matches what `deploy.yml` uses.

### Success Criteria

#### Automated Verification

- `dotnet format --verify-no-changes --no-restore --verbosity minimal` exits 0 locally
- `ci.yml` file exists at `.github/workflows/ci.yml`
- `dotnet build --configuration Release --no-restore` exits 0 locally
- `dotnet test DevNote.Tests/DevNote.Tests.csproj --configuration Release --no-restore` exits 0 locally (37 tests pass)

#### Manual Verification

- Push the branch, open a PR to `master` — GitHub Actions shows a `CI / Format, Build, Test` check running
- All three steps (Format check, Build, Test) show green in the GitHub Actions UI

---

## Phase 2: Local hook — test step

### Overview

Create a third hook script that runs all 37 tests, and register it in the existing hook
configuration JSON as a 120 s `PostToolUse` entry.

### Changes Required

#### 1. New test hook script

**File**: `.github/hooks/post-edit-dotnet-test.ps1` (new)

**Intent**: run the full test suite after a `.cs`/`.razor`/`.cshtml` file edit,
completing the local three-step chain (format → build → **test**). The script must
follow the exact stdin-JSON parsing pattern of the two existing hook scripts so the
Copilot CLI runtime feeds it the tool payload correctly.

**Contract**: same stdin-parsing boilerplate as `post-edit-dotnet-typecheck.ps1`
(read `[Console]::In.ReadToEnd()`, extract tool name with camelCase fallback, check
extension in `@(".cs", ".razor", ".cshtml")`). The test command:

```powershell
& dotnet test DevNote.Tests/DevNote.Tests.csproj --no-restore --verbosity minimal
exit $LASTEXITCODE
```

`--no-restore` is safe here — the agent loop restores on session start; the hook fires
only during an active coding session where packages are always present. No `--no-build`
to avoid any risk of stale outputs if hook 2 was skipped or failed silently.

#### 2. Register third hook entry

**File**: `.github/hooks/post-edit-dotnet-format.json`

**Intent**: append the test hook as a third entry in `PostToolUse`, ensuring it fires
after the two existing hooks (format, then build, then test — in array order).

**Contract**: add to the `PostToolUse` array after the existing two entries:

```json
{
  "type": "command",
  "matcher": "Write|Edit",
  "command": "powershell -NoProfile -ExecutionPolicy Bypass -File .github\\hooks\\post-edit-dotnet-test.ps1",
  "timeout": 120
}
```

### Success Criteria

#### Automated Verification

- `post-edit-dotnet-test.ps1` exists at `.github/hooks/post-edit-dotnet-test.ps1`
- `post-edit-dotnet-format.json` contains three entries in `PostToolUse`
- Running the test script manually exits 0: `Get-Content nul | dotnet test DevNote.Tests/DevNote.Tests.csproj --no-restore --verbosity minimal` (or just run `dotnet test` directly to confirm 37/37 pass)

#### Manual Verification

- Make a trivial edit to any `.cs` file → observe the agent console showing `post-edit-dotnet-test.ps1` output and `37 passed` at the end of the hook chain
- Confirm the hook chain order: format output first, build output second, test output third

---

## Phase 3: AGENTS.md comprehensive update

### Overview

Rewrite `AGENTS.md` to reflect the post-Phase-3 state: correct stack description,
working test command, removed false traps, and a new Quality Gates section that documents
both the CI workflow and the local hook chain.

### Changes Required

#### 1. Correct stack description

**File**: `AGENTS.md`

**Intent**: update the Stack bullet to reflect that Blazor Server is implemented (not
"to be added"), and that the UI layer is live.

**Contract**: change `ASP.NET Core 9 webapi (backend) + Blazor (UI, to be added)` to
`ASP.NET Core 9 webapi + Blazor Server (UI)`.

#### 2. Fix the Commands section

**File**: `AGENTS.md`

**Intent**: replace the stale `dotnet test  # run tests (test project not yet created)`
line with the correct project-scoped command, and add the format-verify command that
agents should run before committing.

**Contract**: replace the `dotnet test` line with:
```
dotnet test DevNote.Tests/DevNote.Tests.csproj   # run all 37 tests (unit, component, integration)
dotnet format --verify-no-changes --no-restore   # verify format before committing
```

#### 3. Remove stale Project-Specific Traps

**File**: `AGENTS.md`

**Intent**: remove the two traps that no longer apply so agents do not waste time
avoiding non-existent problems.

**Contract**: remove trap item 3 ("Blazor UI is not scaffolded yet") entirely. Update
trap item 2 to reflect that the `Dockerfile` exists ("Railway deployment requires a
`Dockerfile` at repo root — ✓ already created").

#### 4. Add Quality Gates section

**File**: `AGENTS.md`

**Intent**: document the two-layer gate system so every agent working in this repo
understands what runs automatically and what it catches — eliminating the need to
re-discover this from `.github/` files.

**Contract**: add a new `## Quality Gates` section after `## Commands`. Contents:

```markdown
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
```

### Success Criteria

#### Automated Verification

- `AGENTS.md` no longer contains the string "test project not yet created"
- `AGENTS.md` no longer contains the string "Blazor UI is not scaffolded yet"
- `AGENTS.md` contains "Quality Gates" heading
- `AGENTS.md` contains `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual Verification

- Read the final `AGENTS.md` end-to-end — every section describes the current state accurately
- Quality Gates table matches what exists in `.github/hooks/` and `.github/workflows/`

---

## Testing Strategy

Phase 4 *is* the testing strategy for the whole project — it does not have its own test
coverage layer. Verification is gate-level: the gates must fire and pass/fail correctly.

### Gate Verification

- **Format gate (CI)**: commit a file with a trailing space or wrong indentation, open a PR
  → `Format check` step fails. Fix and push → step passes.
- **Build gate (CI + local)**: introduce a compile error → CI `Build` step fails; local
  hook 2 output shows build failure.
- **Test gate (CI + local)**: break a test (change a return value in a service) → CI `Test`
  step fails; local hook 3 output shows failed test count.
- **Full local chain**: make a `.cs` edit → observe all three hooks fire in sequence in
  the agent console, ending with `37 passed, 0 failed`.

## References

- Research: `context/changes/testing-quality-gates-wiring/research.md`
- Test plan §5 (Quality Gates table): `context/foundation/test-plan.md`
- Existing hook scripts: `.github/hooks/post-edit-dotnet-format.ps1`, `post-edit-dotnet-typecheck.ps1`
- Existing hook config: `.github/hooks/post-edit-dotnet-format.json`
- Existing CI workflow: `.github/workflows/deploy.yml`
- Phase 1 impl-review (no `--no-build` warning): `context/changes/testing-classification-summary-integrity/reviews/impl-review.md`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: CI quality-gate workflow

#### Automated

- [x] 1.1 `dotnet format --verify-no-changes --no-restore --verbosity minimal` exits 0 locally — f8a67b1
- [x] 1.2 `ci.yml` file exists at `.github/workflows/ci.yml` — f8a67b1
- [x] 1.3 `dotnet build --configuration Release --no-restore` exits 0 locally — f8a67b1
- [x] 1.4 `dotnet test DevNote.Tests/DevNote.Tests.csproj --configuration Release --no-restore` exits 0 locally (37 tests pass) — f8a67b1

#### Manual

- [x] 1.5 Push branch, open PR — GitHub Actions shows `CI / Format, Build, Test` check running — f8a67b1
- [x] 1.6 All three CI steps (Format check, Build, Test) show green in GitHub Actions UI — f8a67b1

### Phase 2: Local hook — test step

#### Automated

- [x] 2.1 `post-edit-dotnet-test.ps1` exists at `.github/hooks/post-edit-dotnet-test.ps1` — 71e843e
- [x] 2.2 `post-edit-dotnet-format.json` contains three entries in `PostToolUse` — 71e843e
- [x] 2.3 `dotnet test DevNote.Tests/DevNote.Tests.csproj --no-restore --verbosity minimal` exits 0 (37 tests pass) — 71e843e

#### Manual

- [x] 2.4 Make a trivial edit to a `.cs` file — agent console shows hook chain output ending with `37 passed` — 71e843e
- [x] 2.5 Hook output order confirmed: format first, build second, test third — 71e843e

### Phase 3: AGENTS.md comprehensive update

#### Automated

- [x] 3.1 `AGENTS.md` no longer contains "test project not yet created"
- [x] 3.2 `AGENTS.md` no longer contains "Blazor UI is not scaffolded yet"
- [x] 3.3 `AGENTS.md` contains "Quality Gates" heading
- [x] 3.4 `AGENTS.md` contains `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual

- [x] 3.5 Full read of `AGENTS.md` — all sections describe current state accurately
- [x] 3.6 Quality Gates table matches what exists in `.github/hooks/` and `.github/workflows/`
