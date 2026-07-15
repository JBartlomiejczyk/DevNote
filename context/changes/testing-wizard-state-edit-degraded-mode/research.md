---
date: 2026-07-15T11:50:23+02:00
researcher: Copilot
git_commit: 0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a
branch: master
repository: JBartlomiejczyk/DevNote
topic: "Rollout Phase 3: wizard state, edit reversion, and degraded helper mode"
tags: [research, codebase, blazor, wizard-state, note-lifecycle, helper-questions]
status: complete
last_updated: 2026-07-15
last_updated_by: Copilot
---

# Research: Wizard State, Edit Reversion, and Degraded Helper Mode

**Date**: 2026-07-15T11:50:23+02:00
**Researcher**: Copilot
**Git Commit**: 0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a
**Branch**: master
**Repository**: JBartlomiejczyk/DevNote

## Research Question

Ground rollout Phase 3 of `context/foundation/test-plan.md`: verify Risks #2,
#5, and #6, locate their real failure paths, correct the proposed response
guidance, identify existing coverage, and select the cheapest useful test layer.

## Summary

- **Risk #2 needs narrower wording.** Collapsing and reopening a wizard section
  preserves its value in the circuit-scoped state service. Leaving the page and
  returning initializes a new wizard component that explicitly resets state.
  A brief SignalR reconnect preserves the circuit; a full reload, server restart,
  or expired circuit loses the unsaved draft because no durable draft exists.
- **Risk #5 is best protected at the service layer first.** Opening a Completed
  note calls the Draft reversion path, but classification and all generated
  summary fields remain stored until successful reclassification overwrites
  them. Reclassification is user-triggered and can be abandoned. The notes list
  renders the retained classification even while the note is Draft.
- **Risk #6 is already non-blocking in the UI.** Helper failure sets section-local
  error state and empties suggestions; it does not disable classification or
  text entry. A per-circuit context-hash cache exists, but there is no TTL or
  coordinator-level in-flight deduplication.
- **bUnit is not installed.** Pure service tests give the cheapest initial signal.
  Component tests require adding bUnit. Coordinator failure/cache tests require
  extracting an `IHelperQuestionsService` boundary because the coordinator
  currently depends on the concrete service.

## Detailed Findings

### Risk #2: Wizard State Preservation

The wizard is a single-page accordion. All eight fields bind directly to
`WizardStateService.Data`; `WizardSection` removes collapsed content from the DOM
and restores its textarea from the bound value when expanded again
([Wizard.razor:19-89](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Pages/Wizard.razor#L19-L89),
[WizardSection.razor:9-17](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Shared/WizardSection.razor#L9-L17)).
The input uses `@onchange`, satisfying the accepted high-latency Blazor Server
rule; a regression to `@oninput` would reintroduce SignalR round-trip races
([lessons.md:5-10](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/context/foundation/lessons.md#L5-L10)).

The state service is scoped, which in Blazor Server means per circuit
([Program.cs:60](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Program.cs#L60)).
Accordion collapse/re-expand and a brief reconnect retain the same service.
However, `Wizard.OnInitialized` unconditionally calls `Reset`
([Wizard.razor:143-147](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Pages/Wizard.razor#L143-L147)).
Consequently, page navigation away and back loses the current draft even if the
circuit survives. Full reload or unrecoverable circuit loss also loses it because
the state has no browser or database persistence
([WizardStateService.cs:5-32](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Services/WizardStateService.cs#L5-L32)).

`LoadFromNote` is a separate durable-state boundary: it creates a fresh
`WizardData` from all eight stored inputs and clears helper-question cache
entries. This correctly prevents one edited note's fields from leaking into the
next edit session.

**Cheapest useful protection**

1. Pure unit tests for `Reset`, `LoadFromNote`, and cache clearing.
2. bUnit tests for expand -> edit -> collapse -> re-expand, asserting the value
   is restored after unmount/re-render.
3. Treat page-navigation and full-circuit draft survival as a product decision,
   not an assertion of current behavior. Current code intentionally resets or
   lacks persistence, so a test demanding survival would specify an unimplemented
   feature.

**Correction to test-plan guidance:** distinguish accordion navigation
(preserved), same-page re-render (preserved), page leave/return (reset), brief
reconnect (preserved), and new circuit/full reload (lost).

### Risk #5: Completed -> Draft -> Reclassified

The edit route loads a note through an ownership-filtered query, calls
`RevertToDraftAsync`, then maps its eight wizard fields into the state service
([EditNote.razor:143-166](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Pages/EditNote.razor#L143-L166)).
The component calls reversion for both Draft and Completed notes; the service
only writes when the current status is Completed
([NoteService.cs:109-121](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Services/NoteService.cs#L109-L121)).

Reversion changes only `Status` and `UpdatedAt`. It leaves classification,
justification, and all generated summary fields unchanged. Successful
reclassification first obtains a fresh result and then overwrites the same note,
including all inputs and generated fields, before setting it Completed
([EditNote.razor:191-218](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Pages/EditNote.razor#L191-L218),
[NoteService.cs:73-107](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Services/NoteService.cs#L73-L107)).
If the user abandons editing or classification fails, the note remains Draft
with its previous generated data in storage. The notes list displays a
classification badge whenever `Classification` is non-null, regardless of
Draft status, making that retained value visible
([Notes.razor:33-45](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Pages/Notes.razor#L33-L45)).

**Cheapest useful protection**

1. Extend `NoteServiceTests` with the successful Completed -> Draft transition
   and already-Draft no-op.
2. Test the full service sequence against one seeded row: revert, assert the
   intermediate Draft state, update with independently constructed new inputs
   and result, then assert the same ID, one row, Completed status, and every
   generated field replaced.
3. Add a component test only for behavior the service test cannot prove:
   opening Edit invokes reversion/load, successful classification updates rather
   than creates, and failure leaves the note Draft while showing retry UI.

HTTP integration adds little signal here because the state transition happens
inside an interactive Blazor component. Existing service tests cover only
wrong-owner paths
([NoteServiceTests.cs:35-81](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/DevNote.Tests/Services/NoteServiceTests.cs#L35-L81)).

**Correction to test-plan guidance:** the call to reversion is unconditional,
while its write is conditional. Reclassification is separately user-triggered.
Use service unit + component tests rather than service unit + HTTP integration.
The plan should explicitly decide whether retaining and displaying stale
classification on a Draft note is intended behavior or a defect.

### Risk #6: Helper Failure, Caching, and Cost

Expanding a section triggers helper generation once per component lifetime.
Refresh bypasses cache and is disabled while that section is loading
([WizardSection.razor:24-27](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Shared/WizardSection.razor#L24-L27),
[WizardSection.razor:69-87](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Shared/WizardSection.razor#L69-L87)).
The coordinator owns loading/error UI state. On any generation exception it
sets the Polish error message, empties questions, and clears the loading flag
([HelperQuestionsCoordinator.cs:68-93](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Services/HelperQuestionsCoordinator.cs#L68-L93)).

The wizard's classify button depends only on wizard content and classification
loading, not helper loading or helper errors. Textareas also remain enabled.
Helper failure therefore does not block progression
([Wizard.razor:99-111](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Components/Pages/Wizard.razor#L99-L111)).

The coordinator caches by section key plus a SHA-256 hash of prior answers,
question count, and locale
([HelperQuestionsCoordinator.cs:25-48](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Services/HelperQuestionsCoordinator.cs#L25-L48),
[HelperQuestionsService.cs:221-229](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Services/HelperQuestionsService.cs#L221-L229)).
The dictionary is per circuit, has no TTL, and is cleared by reset or note load.
It avoids sequential duplicate requests. There is no coordinator-level guard
against two concurrent uncached calls with the same key; current UI controls
make that unlikely but do not establish service-level idempotency.

The coordinator depends on the concrete `HelperQuestionsService`, and that
service creates `AzureOpenAIClient` inline. Testing live generation would
therefore require a real external endpoint
([HelperQuestionsService.cs:91-92](https://github.com/JBartlomiejczyk/DevNote/blob/0fcdce67f0a67a93ef4c3aa43bbd3ee3db4f6b4a/Services/HelperQuestionsService.cs#L91-L92)).

**Cheapest useful protection**

1. Pure unit tests for prior-context construction, deterministic context hashes,
   and `WizardStateService` cache hit/clear behavior.
2. Extract `IHelperQuestionsService` and substitute only that external boundary
   in coordinator tests. Assert cache hits avoid a second generation call,
   force-refresh calls again, and failure produces error + empty questions +
   `IsLoading == false`.
3. A focused bUnit assertion can prove helper error/loading state never disables
   classification; do not use browser e2e for this risk.

**Correction to test-plan guidance:** the cache does not deduplicate concurrent
in-flight calls. Coordinator tests require an injectable service boundary. Add
the non-blocking classify-button behavior to the protection oracle.

## Code References

- `Components\Pages\Wizard.razor:19-111,143-207` - field binding, classify
  availability, reset, helper loading, and classification flow.
- `Components\Pages\EditNote.razor:143-218` - edit load, Draft reversion, and
  classify-then-update ordering.
- `Components\Shared\WizardSection.razor:9-87` - conditional rendering,
  `@onchange`, helper states, refresh, and first-expand guard.
- `Services\WizardStateService.cs:5-54` - circuit-local wizard data and cache.
- `Services\NoteService.cs:68-121` - ownership load, update, and Draft reversion.
- `Services\HelperQuestionsCoordinator.cs:25-96` - cache lookup, generation,
  UI-state error translation, and cache-key construction.
- `Services\HelperQuestionsService.cs:91-92,221-229` - inline Azure client and
  deterministic context hashing.
- `DevNote.Tests\DevNote.Tests.csproj:10-18` - xUnit, NSubstitute, integration
  packages; bUnit is absent.

## Architecture Insights

- Blazor's scoped lifetime is a circuit lifetime. It is appropriate for
  accordion navigation but is not durable draft storage.
- `WizardStateService` owns two responsibilities: current form data and
  helper-result cache. Both intentionally reset together.
- `NoteService` is the strongest current test seam for lifecycle behavior:
  it uses EF Core through the existing in-memory test pattern and requires no
  browser or HTTP host.
- UI orchestration has an explicit coordinator, but its concrete dependency
  prevents replacing the external LLM edge. Introducing the narrow interface is
  a testability improvement at the actual boundary, not an internal mock.
- Exact generated Polish wording remains negative space. Oracles should assert
  state, structure, replacement, invocation count, and progression availability.

## Historical Context

- `context\changes\wizard-classification-summary\plan.md` introduced the wizard,
  circuit-scoped state, helper generation, and classification flow.
- `context\changes\auth-and-note-persistence\plan.md` introduced persistence and
  the Draft/Completed lifecycle.
- `context\changes\note-management\plan.md` introduced the edit route and
  reclassification workflow.
- `context\changes\testing-classification-summary-integrity\research.md`
  established contract-derived test oracles for generated output.
- `context\changes\testing-auth-ownership-boundary\research.md` established the
  existing integration host and documented interactive redirect limitations.

## Related Research

- `context\changes\testing-classification-summary-integrity\research.md`
- `context\changes\testing-auth-ownership-boundary\research.md`

## Open Questions

1. Should an unsaved new-note draft survive page navigation, full reload, or a
   new Blazor circuit? Current behavior does not.
2. Should reverting a Completed note clear classification/generated summaries,
   or should the notes list merely hide classification while status is Draft?
3. Is coordinator-level in-flight deduplication required, or are current
   first-expand and disabled-refresh UI guards sufficient for MVP?
