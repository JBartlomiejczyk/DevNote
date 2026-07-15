# Wizard State, Edit Reversion, and Degraded Helper Mode Implementation Plan

## Overview

Complete rollout Phase 3 from `context/foundation/test-plan.md` with focused
regression coverage for wizard state, note reclassification, and helper-question
degradation. The rollout adds one data-integrity correction: reverting a note to
Draft clears its prior classification and generated summary so stale output
cannot be presented as current.

## Current State Analysis

The application already uses xUnit, NSubstitute, EF Core InMemory, and
`WebApplicationFactory`, but it has no bUnit package or component tests
(`DevNote.Tests/DevNote.Tests.csproj:10-18`). Wizard answers and helper results
live in a circuit-scoped `WizardStateService`; accordion collapse/re-expand
preserves them, while returning to the new-note page explicitly resets them
(`Services/WizardStateService.cs:5-54`, `Components/Pages/Wizard.razor:143-147`).

Opening an existing note calls `RevertToDraftAsync` before loading its eight
wizard fields. The current transition changes only status and timestamp, leaving
classification and all generated summary fields stale
(`Components/Pages/EditNote.razor:143-166`, `Services/NoteService.cs:109-121`).
Helper failures are already translated into section-local UI state and do not
disable classification, but the concrete LLM dependency prevents isolated
coordinator tests (`Services/HelperQuestionsCoordinator.cs:5-96`).

## Desired End State

- Wizard state and cache reset/load invariants are covered by service tests.
- Accordion expand, edit, collapse, and re-expand retains the entered value;
  page navigation and full-circuit draft persistence remain explicitly out of
  scope.
- Every Draft note has no classification, justification, or generated summary.
  Successful reclassification updates the original row with fresh output.
- Sequential identical helper requests use the per-circuit cache, force refresh
  bypasses it, and helper failure leaves the wizard usable.
- bUnit covers the reusable section component and focused Wizard/EditNote flows
  without calling Azure OpenAI or mocking internal state and persistence logic.
- The Phase 3 cookbook entries and research corrections are reflected in
  `context/foundation/test-plan.md`.

### Key Discoveries

- Blazor scoped services live for a circuit, not across a new circuit or reload
  (`Program.cs:60`, `Services/WizardStateService.cs:5-54`).
- `WizardSection` correctly uses `@onchange`; tests must preserve the accepted
  high-latency rule in `context/foundation/lessons.md:5-10`.
- `RevertToDraftAsync` is called on every EditNote load but writes only for a
  Completed note (`Components/Pages/EditNote.razor:163`,
  `Services/NoteService.cs:115-120`).
- The notes list displays any non-null classification regardless of status, so
  clearing generated data is the authoritative fix
  (`Components/Pages/Notes.razor:37-49`).
- Current bUnit documentation uses `BunitContext`, `Render<T>()`,
  `AddAuthorization()`, and built-in JS interop. The current NuGet version
  verified on 2026-07-15 is `bunit` 2.7.2.

## What We're NOT Doing

- Persisting an unsaved new-note draft across page navigation, browser reload,
  server restart, or a new Blazor circuit.
- Adding browser e2e coverage; deterministic service and component tests provide
  the required signal more cheaply.
- Adding coordinator-level concurrent in-flight deduplication. Current
  first-expand and disabled-refresh guards are sufficient for MVP; this rollout
  protects sequential cache hits.
- Testing exact Polish LLM output, Identity internals, or live Azure OpenAI.
- Introducing `INoteService`; component tests use the real service with an
  isolated EF Core InMemory database.
- Changing the database schema or adding a migration.

## Implementation Approach

Work from the cheapest boundaries outward. First establish service-level
invariants and correct the Draft transition. Next expose only the helper LLM
edge through a narrow interface and test coordinator behavior. Finally add
bUnit, expose the classification LLM edge through the same pattern, and verify
the user-visible component interactions with real wizard state and note
persistence.

Tests derive their oracles from the PRD and rollout risks: Draft means generated
output is absent; reclassification replaces every generated field; helper
failure leaves classification available; accordion re-rendering retains the
entered answer. Do not copy expected values from production mapping logic.

## Critical Implementation Details

### State sequencing

`EditNote.OnInitializedAsync` loads a tracked note, reverts it, then calls
`State.LoadFromNote(note)`. Clearing generated fields during reversion must not
clear the eight wizard input fields that `LoadFromNote` needs. Successful
classification must still occur before `UpdateNoteAsync`; a classification
failure therefore leaves a clean Draft rather than a falsely Completed note.

### Test isolation

Each component test that uses `NoteService` must create a unique EF Core
InMemory database and dispose its context. Configure bUnit authorization with a
`ClaimTypes.NameIdentifier` matching the seeded note. Use bUnit's fake JS
runtime for the scroll call; do not start the web server or call Azure.

## Phase 1: State and Note Lifecycle Contracts

### Overview

Protect the circuit-local wizard state and establish the Draft data invariant at
the lowest-cost service layer. Correct the stale-output defect before component
tests encode the lifecycle.

### Changes Required

#### 1. Draft transition and note lifecycle tests

**Files**:
- `Services/NoteService.cs`
- `DevNote.Tests/Services/NoteServiceTests.cs`

**Intent**: Make Draft the authoritative unclassified state. Cover the complete
Completed -> Draft -> Completed lifecycle on one persisted note.

**Contract**: `RevertToDraftAsync(Guid noteId, string userId)` keeps its
ownership guard and clears `Classification`, `Justification`, and all 11
generated summary fields whenever the note contains stale generated output. It
sets `Status` to `Draft` and updates `UpdatedAt` in the same save. A clean Draft
is a no-op. Tests use independently constructed old and new values, assert the
intermediate Draft state, and prove reclassification updates the same ID without
creating another row.

#### 2. Wizard state service tests

**File**: `DevNote.Tests/Services/WizardStateServiceTests.cs`

**Intent**: Pin the state-restoration boundary that actually exists without
pretending circuit-local state is durable storage.

**Contract**: Cover `Reset` clearing all eight fields and helper cache,
`LoadFromNote` mapping all eight stored inputs into a fresh `WizardData`, and
`LoadFromNote` clearing prior helper cache entries. Do not assert page reload or
new-circuit survival.

### Success Criteria

#### Automated Verification

- `NoteServiceTests` prove Completed-to-Draft clearing, clean-Draft no-op, fresh
  field replacement, same note ID, and unchanged row count.
- `WizardStateServiceTests` prove reset/load mapping and cache invalidation.
- Phase-focused tests pass:
  `dotnet test DevNote.Tests\DevNote.Tests.csproj --filter "FullyQualifiedName~NoteServiceTests|FullyQualifiedName~WizardStateServiceTests"`.
- The full suite passes: `dotnet test`.
- The solution builds: `dotnet build`.

#### Manual Verification

- Open a Completed note, return to the notes list without reclassifying, and
  confirm it is shown as Draft with no A/B/C badge.

**Implementation Note**: Pause after automated verification for the manual check
before proceeding.

---

## Phase 2: Helper Orchestration Boundary

### Overview

Make the helper LLM call substitutable at its true external boundary and protect
sequential caching, force refresh, and degraded UI state without a live Azure
request.

### Changes Required

#### 1. Injectable helper service contract

**Files**:
- `Services/IHelperQuestionsService.cs`
- `Services/HelperQuestionsService.cs`
- `Services/HelperQuestionsCoordinator.cs`
- `Program.cs`

**Intent**: Substitute only the LLM edge while leaving coordinator, wizard
state, cache, and hashing behavior real.

**Contract**: Add `IHelperQuestionsService.GenerateAsync(
HelperQuestionsRequest request, CancellationToken ct = default)`.
`HelperQuestionsService` implements it. Register
`IHelperQuestionsService -> HelperQuestionsService` as scoped and make
`HelperQuestionsCoordinator` depend on the interface. Preserve existing runtime
lifetime and behavior.

#### 2. Coordinator behavior tests

**File**: `DevNote.Tests/Services/HelperQuestionsCoordinatorTests.cs`

**Intent**: Catch cost and degraded-mode regressions through observable results
and UI-state output.

**Contract**: With real `WizardStateService` and a substituted
`IHelperQuestionsService`, cover:

- identical section, prior context, count, and locale returns the cached result
  and invokes generation once;
- changed prior context creates a cache miss;
- `forceRefresh: true` invokes generation again;
- generation failure leaves `Questions` empty, sets the user-facing error, and
  always resets `IsLoading` to false.

Concurrent in-flight calls are not part of this contract.

### Success Criteria

#### Automated Verification

- Coordinator tests pass without Azure configuration or network access:
  `dotnet test DevNote.Tests\DevNote.Tests.csproj --filter "FullyQualifiedName~HelperQuestionsCoordinatorTests"`.
- Existing helper generation behavior compiles against the interface.
- The full suite passes: `dotnet test`.
- The solution builds: `dotnet build`.

---

## Phase 3: Blazor Component Coverage and Rollout Cookbook

### Overview

Add the component-test layer, verify the page-level behavior that service tests
cannot prove, and record the shipped patterns in the rollout guide.

### Changes Required

#### 1. bUnit test infrastructure

**Files**:
- `DevNote.Tests/DevNote.Tests.csproj`
- `DevNote.Tests/Components/` (new test area)

**Intent**: Add the smallest supported component-test surface for .NET 9 and
follow current bUnit APIs.

**Contract**: Reference `bunit` 2.7.2. Use xUnit test classes based on
`BunitContext`, `Render<T>()`, `AddAuthorization()` with an explicit
`ClaimTypes.NameIdentifier`, and bUnit JS interop. Keep each EF-backed test on a
unique InMemory database.

#### 2. Injectable classification service contract

**Files**:
- `Services/IClassificationService.cs`
- `Services/ClassificationService.cs`
- `Components/Pages/Wizard.razor`
- `Components/Pages/EditNote.razor`
- `Program.cs`

**Intent**: Allow page tests to control classification success and failure
without calling Azure.

**Contract**: Add `IClassificationService.ClassifyAsync(
WizardData data, CancellationToken ct = default)`.
`ClassificationService` implements it. Register the interface mapping as scoped
and inject the interface into Wizard and EditNote. Preserve the existing
classify-before-persist ordering.

#### 3. WizardSection component tests

**File**: `DevNote.Tests/Components/WizardSectionTests.cs`

**Intent**: Establish the canonical component-test pattern for wizard
navigation and helper presentation.

**Contract**: Cover expand -> edit -> collapse -> re-expand with the value
restored; `FirstExpanded` firing once across repeated toggles; refresh disabled
while helper loading; and mutually exclusive loading, error, question-list, and
empty states. Interact through rendered controls and callbacks, not private
fields. Preserve `@onchange`; do not change to `@oninput`.

#### 4. Focused Wizard and EditNote tests

**Files**:
- `DevNote.Tests/Components/WizardTests.cs`
- `DevNote.Tests/Components/EditNoteTests.cs`

**Intent**: Prove the cross-component behavior behind Risks #2, #5, and #6
without duplicating service-level assertions.

**Contract**:

- Wizard: content enables classification; a helper generation failure renders
  the helper error but leaves the classify button enabled and textarea usable.
- EditNote load: an authenticated owner receives all eight prefilled fields,
  and the persisted note is reverted to a clean Draft.
- EditNote success: controlled classification updates the original row, renders
  the success/result surface, and leaves one note with fresh generated fields.
- EditNote failure: classification failure renders retry UI and leaves the note
  as a clean Draft.

Use real `WizardStateService`, `HelperQuestionsCoordinator`, and `NoteService`;
substitute only `IHelperQuestionsService` and `IClassificationService`.

#### 5. Rollout guide backport and cookbook

**File**: `context/foundation/test-plan.md`

**Intent**: Make the strategy reflect grounded behavior and give future agents
working examples from the shipped suite.

**Contract**:

- Narrow Risk #2 guidance to same-circuit accordion/re-render preservation;
  document page leave/new circuit loss as accepted negative space.
- State that Risk #5 Draft reversion clears generated output before
  reclassification.
- Clarify that Risk #6 protects sequential context-hash cache hits, not
  concurrent in-flight deduplication.
- Fill cookbook sections 6.3, 6.4, and 6.5 with actual test locations, run
  commands, mocking boundaries, and reference tests delivered by this phase.
- Do not change Phase 3 status here; `/10x-test-plan` reconciles rollout status
  from the completed `## Progress` section.

### Success Criteria

#### Automated Verification

- Component tests pass:
  `dotnet test DevNote.Tests\DevNote.Tests.csproj --filter "FullyQualifiedName~DevNote.Tests.Components"`.
- No component test performs a live Azure call or starts a browser/server.
- `dotnet format --verify-no-changes` passes.
- The full suite passes: `dotnet test`.
- The solution builds: `dotnet build`.
- `context/foundation/test-plan.md` names the shipped component, helper, and note
  lifecycle reference tests and contains the three grounded guidance updates.

#### Manual Verification

- Enter text, collapse and reopen several wizard sections, and confirm every
  answer remains unchanged.
- With helper generation unavailable, confirm the helper error is visible while
  text entry and classification remain available.
- Edit a Completed note, confirm it becomes Draft with no classification badge,
  then reclassify and confirm the result reflects the edited answers.

**Implementation Note**: Pause after automated verification for the manual
checks before closing the rollout phase.

---

## Testing Strategy

### Unit Tests

- Treat `WizardStateService` and `NoteService` as real units; no substitutes.
- Use independent old/new values for every generated field to avoid tautological
  mapping assertions.
- Substitute only LLM-facing interfaces. Assert coordinator result, UI state,
  and invocation count rather than internal method order.
- Do not pin literal hashes or exact Polish generated text.

### Component Tests

- Use bUnit semantic queries and rendered control interaction.
- Keep page collaborators real when inexpensive: wizard state, coordinator,
  NoteService, and isolated EF InMemory.
- Use test authorization with an explicit owner claim.
- Assert user-visible states and persisted outcomes, never private component
  fields.

### Integration Tests

- Existing `WebApplicationFactory` auth tests remain unchanged. Interactive
  Blazor behavior is covered more directly by bUnit; no new HTTP integration
  test is required for this rollout.

### Manual Testing Steps

1. Verify accordion state preservation with several populated sections.
2. Verify helper degradation leaves the wizard usable.
3. Verify an abandoned edit leaves a clean Draft.
4. Verify reclassification replaces the old summary on the same note.

## Performance Considerations

The helper cache remains circuit-local and has no TTL. It avoids sequential
duplicate calls for identical section/context/count/locale inputs. The rollout
does not add locks or in-flight request registries because current UI guards
make concurrent duplicate calls an unsupported edge case for MVP.

## Migration Notes

No schema migration is required. Existing Draft rows created by older behavior
are repaired when opened because `RevertToDraftAsync` enforces the clean-Draft
invariant even when stale generated data is present. Rollback restores the prior
service behavior and interface registrations; no stored schema changes need
reversal.

## References

- Related research:
  `context/changes/testing-wizard-state-edit-degraded-mode/research.md`
- Rollout strategy: `context/foundation/test-plan.md:61-70,81-94,142-170`
- Product requirements: `context/foundation/prd.md:58-68,106-116,124-139`
- Accepted Blazor input rule: `context/foundation/lessons.md:5-10`
- Wizard state: `Services/WizardStateService.cs:5-54`
- Note lifecycle: `Services/NoteService.cs:73-121`
- Helper orchestration: `Services/HelperQuestionsCoordinator.cs:18-96`
- Wizard component: `Components/Pages/Wizard.razor:16-206`
- Edit component: `Components/Pages/EditNote.razor:17-219`
- Reusable section: `Components/Shared/WizardSection.razor:3-92`
- Existing test conventions: `DevNote.Tests/Services/NoteServiceTests.cs:9-81`
- bUnit documentation checked 2026-07-15:
  `https://bunit.dev/docs/getting-started/writing-tests.html`,
  `https://bunit.dev/docs/test-doubles/auth.html`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: State and Note Lifecycle Contracts

#### Automated

- [x] 1.1 Note lifecycle tests prove clean Draft reversion and fresh same-row reclassification — c0e9c3f
- [x] 1.2 Wizard state tests prove reset, load mapping, and cache invalidation — c0e9c3f
- [x] 1.3 Phase-focused tests, full suite, and build pass — c0e9c3f

#### Manual

- [x] 1.4 Abandoned edit appears as Draft without an A/B/C badge — c0e9c3f

### Phase 2: Helper Orchestration Boundary

#### Automated

- [x] 2.1 Helper service interface is wired without runtime behavior change — e1a55c8
- [x] 2.2 Coordinator tests prove sequential cache, refresh, context miss, and degraded state — e1a55c8
- [x] 2.3 Phase-focused tests, full suite, and build pass — e1a55c8

### Phase 3: Blazor Component Coverage and Rollout Cookbook

#### Automated

- [x] 3.1 bUnit and classification interface support deterministic page tests — 8d84a6b
- [x] 3.2 WizardSection tests prove navigation, binding, and helper presentation — 8d84a6b
- [x] 3.3 Wizard and EditNote tests prove non-blocking degradation and edit lifecycle — 8d84a6b
- [x] 3.4 Test-plan guidance and cookbook reflect the shipped patterns — 8d84a6b
- [x] 3.5 Format, component tests, full suite, and build pass — 8d84a6b

#### Manual

- [x] 3.6 Wizard answers survive accordion navigation — 8d84a6b
- [x] 3.7 Helper failure leaves text entry and classification available — 8d84a6b
- [x] 3.8 Edit reversion and reclassification show only current output — 8d84a6b
