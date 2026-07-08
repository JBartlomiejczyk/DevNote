# Testing classification-summary integrity Implementation Plan

## Overview

Implement Phase 1 of the test rollout by introducing the first xUnit test harness and enforcing strict classification/summary integrity at the LLM parse boundary. The goal is to prevent silent trust in malformed or partial model output and enforce Risk #1 from `test-plan.md`.

## Current State Analysis

Classification and summary generation are centralized in `ClassificationService`, then persisted by `NoteService` and shown in `Result` UI. The codebase has no test project yet, and CI currently runs restore/build without `dotnet test`.

## Desired End State

The codebase has a working `DevNote.Tests` project with unit tests covering parser/validator integrity policies. Unknown classification values and empty required summary fields are treated as invalid responses (fail-fast), surfaced as errors, and never silently persisted as valid-looking output. CI enforces unit tests on push.

### Key Discoveries:

- Unknown classification currently defaults to `B`, which can mask drift (`Services/ClassificationService.cs:168-175`).
- Parsed text fields currently coerce null to empty strings (`Services/ClassificationService.cs:180-191`).
- Wizard/Edit classify paths catch broad exceptions and show generic error (`Components/Pages/Wizard.razor:192-195`, `Components/Pages/EditNote.razor:205-208`).
- No test project exists yet (`dev-note.csproj:1-20`), and workflow lacks test gate (`.github/workflows/deploy.yml:19-24`).
- Team quality contract explicitly prioritizes this risk in Phase 1 (`context/foundation/test-plan.md:83-86`).

## What We're NOT Doing

- No integration tests, bUnit tests, or e2e tests in this change (reserved for later phases).
- No redesign of wizard UX beyond existing error-surfacing behavior.
- No broad auth/ownership test rollout (covered by separate planned phase).

## Implementation Approach

Extract classification response validation/mapping into a dedicated component to isolate integrity policy from transport concerns. Use contract-first unit tests to drive behavior: strict A/B/C acceptance, strict non-empty summary fields, and explicit invalid-response failures. Wire tests into CI with `dotnet test` as required Phase 1 gate.

## Critical Implementation Details

### State sequencing

Validation must happen before persistence mapping. Any invalid response path must terminate before `NoteService.CreateNoteAsync` / `UpdateNoteAsync` to avoid storing incomplete summary data.

## Phase 1: Bootstrap test harness and isolate response policy

### Overview

Create the first test project and extract classification response policy to a dedicated, unit-testable component.

### Changes Required:

#### 1. Test project bootstrap

**File**: `DevNote.Tests/DevNote.Tests.csproj` (new), solution/project wiring files

**Intent**: Introduce the first .NET test project for this repository and align with .NET 9 conventions.

**Contract**: Test project references app project and includes standard xUnit runner stack required by `dotnet test`.

#### 2. Classification response validator/mapper component

**File**: `Services/ClassificationResponseValidator.cs` (new) and `Services/ClassificationService.cs`

**Intent**: Move parse+integrity policy out of `ClassificationService` so behavior is explicit and testable in isolation.

**Contract**: New component accepts model JSON response and returns validated `ClassificationResult` or throws domain-specific invalid-response exception.

#### 3. Dependency registration

**File**: `Program.cs`

**Intent**: Register new validator/mapper service and keep current runtime wiring consistent.

**Contract**: DI registration resolves the validator for `ClassificationService` without changing endpoint or page contracts.

### Success Criteria:

#### Automated Verification:

- `dotnet restore` succeeds with new test project dependencies.
- `dotnet build` succeeds for app + tests.
- `dotnet test` executes and discovers test project.

#### Manual Verification:

- Repository structure clearly includes `DevNote.Tests` and maintainers can run tests locally with one command.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Enforce integrity policy and cover Risk #1 with unit tests

### Overview

Implement fail-fast response policy and add unit tests for malformed/partial/empty response scenarios.

### Changes Required:

#### 1. Fail-fast classification policy

**File**: `Services/ClassificationResponseValidator.cs`, `Services/ClassificationService.cs`

**Intent**: Reject unknown classification values instead of silently defaulting to `B`.

**Contract**: Allowed classification values are exactly `A`, `B`, `C`; all others raise invalid-response exception.

#### 2. Required summary completeness policy

**File**: `Services/ClassificationResponseValidator.cs`

**Intent**: Enforce PRD-complete summary output quality by rejecting empty required fields.

**Contract**: All 11 summary fields are required and must be non-empty after trimming.

#### 3. Error surfacing consistency

**File**: `Components/Pages/Wizard.razor`, `Components/Pages/EditNote.razor`

**Intent**: Keep existing user-facing error pattern while ensuring invalid response paths are surfaced, not silently rendered.

**Contract**: Invalid-response exceptions flow into existing error banner path; no partial summary render on invalid response.

#### 4. Risk #1 unit suite

**File**: `DevNote.Tests/Services/ClassificationResponseValidatorTests.cs` (new), related test files

**Intent**: Cover high-signal scenarios for contract integrity and prevent regressions.

**Contract**: Tests include happy path, unknown classification, malformed JSON, missing required field, empty required field, and mapping smoke checks for persistence-bound fields.

### Success Criteria:

#### Automated Verification:

- Unit tests pass for all required integrity scenarios via `dotnet test`.
- Build passes via `dotnet build`.
- No silent fallback behavior remains for unknown classification.

#### Manual Verification:

- In local run, triggering an invalid response path shows error banner and does not show a blank-but-valid summary.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Activate CI quality gate for Phase 1

### Overview

Enforce the new unit baseline in CI by adding `dotnet test` as a required workflow step.

### Changes Required:

#### 1. CI workflow update

**File**: `.github/workflows/deploy.yml`

**Intent**: Ensure pull/push pipeline executes tests, not only build.

**Contract**: Workflow runs restore, build, and `dotnet test` in deterministic order; failing tests fail the job.

#### 2. Documentation alignment

**File**: `context/foundation/test-plan.md` (Phase 1 cookbook/progress-aligned note if needed), optional project command docs

**Intent**: Keep rollout artifacts consistent with introduced gate.

**Contract**: Documentation reflects that unit gate is active after Phase 1 implementation.

### Success Criteria:

#### Automated Verification:

- CI workflow executes `dotnet test` successfully on current branch.
- Local command parity: `dotnet restore && dotnet build && dotnet test` (PowerShell sequential equivalent) passes.

#### Manual Verification:

- Verify in GitHub Actions UI that failing tests block pipeline as expected.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

## Testing Strategy

### Unit Tests:

- Response validator accepts valid A/B/C + 11-field payload and maps deterministically.
- Reject unknown classification token.
- Reject malformed JSON and missing required properties.
- Reject trimmed-empty required summary fields.
- Verify mapping contract used by persistence layer remains complete.

### Integration Tests:

- Not part of this phase (deferred to auth/ownership and wizard-state phases in test rollout).

### Manual Testing Steps:

1. Run app locally and verify normal classification result still renders.
2. Simulate invalid response path (test double or controlled input) and confirm error banner appears.
3. Confirm no incomplete summary is rendered/persisted when validation fails.

## Performance Considerations

Validation is local JSON parsing and string checks; impact is negligible versus LLM roundtrip latency. The stricter policy may increase visible failures under model drift, which is intentional safety behavior.

## Migration Notes

No database schema migration is required. This change is behavioral (validation policy + tests + CI gate).

## References

- Related research: `context/changes/testing-classification-summary-integrity/research.md`
- Quality contract: `context/foundation/test-plan.md`
- Existing parser path: `Services/ClassificationService.cs:163-193`
- Existing CI workflow: `.github/workflows/deploy.yml:19-24`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Bootstrap test harness and isolate response policy

#### Automated

- [x] 1.1 `dotnet restore` succeeds with new test project dependencies
- [x] 1.2 `dotnet build` succeeds for app + tests
- [x] 1.3 `dotnet test` executes and discovers test project

#### Manual

- [x] 1.4 Repository structure includes `DevNote.Tests` and tests run locally with one command

### Phase 2: Enforce integrity policy and cover Risk #1 with unit tests

#### Automated

- [ ] 2.1 Unit tests pass for required integrity scenarios via `dotnet test`
- [ ] 2.2 Build passes via `dotnet build`
- [ ] 2.3 Unknown classification no longer has silent fallback behavior

#### Manual

- [ ] 2.4 Invalid response path shows error banner and does not render blank-but-valid summary

### Phase 3: Activate CI quality gate for Phase 1

#### Automated

- [ ] 3.1 CI workflow executes `dotnet test` successfully on current branch
- [ ] 3.2 Local command parity (`dotnet restore`, `dotnet build`, `dotnet test`) passes

#### Manual

- [ ] 3.3 Failing tests block pipeline in GitHub Actions as expected
