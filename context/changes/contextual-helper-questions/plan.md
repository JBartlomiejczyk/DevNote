# Contextual helper questions Implementation Plan

## Overview

Implement AI-generated contextual helper questions (3-5) for each wizard section in both create and edit flows. Questions are generated on first section expansion, can be manually refreshed, and use only earlier sections as context.

## Current State Analysis

The app already has an 8-section Blazor wizard and an Azure OpenAI integration for final classification, but no per-section helper-question feature.

## Desired End State

When a user opens a wizard section, they see section-specific helper questions generated from prior section answers. The feature works in `/` and `/edit/{id}`, avoids unnecessary repeated LLM calls via session cache, and surfaces clear error + retry behavior when generation fails.

### Key Discoveries:

- Wizard and edit flows duplicate section rendering and both start with all sections expanded, which can cause request storms if not guarded (`Components\Pages\Wizard.razor:15`, `Components\Pages\Wizard.razor:92`, `Components\Pages\EditNote.razor:17`, `Components\Pages\EditNote.razor:93`)
- Existing OpenAI integration pattern is reusable (options binding, service DI, strict schema) (`Program.cs:61`, `Program.cs:63`, `Services\ClassificationService.cs:101`, `Services\ClassificationService.cs:140`)
- Section component currently has only text input and expand toggle hooks, making it the correct insertion point for helper question UI/state triggers (`Components\Shared\WizardSection.razor:4`, `Components\Shared\WizardSection.razor:9`, `Components\Shared\WizardSection.razor:36`)

## What We're NOT Doing

- Persisting helper questions in database entities
- Creating a separate HTTP endpoint for helper questions
- Adding a new automated test project in this slice
- Adding static fallback questions in degraded mode

## Implementation Approach

Add a dedicated helper-question generation service following the current `ClassificationService` style (Azure OpenAI + strict JSON schema), then integrate it into both wizard pages through `WizardSection` events. Use deterministic section ordering and derive context strictly from earlier sections. Add in-memory session cache keyed by `(section, context-hash)` to reduce cost and latency.

## Critical Implementation Details

### Timing & lifecycle

Do not generate helper questions on initial page render. Initialize all sections as collapsed in both create and edit flows, then trigger generation only on the first explicit expansion event for a section. Keep manual refresh available for subsequent regeneration.

### State sequencing

Cache lookup must happen before network call, and loading/error state must be tracked per section independently so one section failure does not block classification or other sections.

## Phase 1: Helper-question domain model and generation service

### Overview

Introduce the helper-question contract, deterministic section context extraction, and the AI generation service with strict validation.

### Changes Required:

#### 1. New helper-question models

**File**: `Models\HelperQuestionModels.cs` (new)

**Intent**: Define section identity, request/response contracts, and per-section UI state shape used by both wizard pages.

**Contract**: Add strongly typed models for section keys, generation request, generation result (3-5 questions), and state metadata (`isLoading`, `error`, `questions`, `lastContextHash`).

#### 2. Wizard context extraction helpers

**File**: `Models\WizardData.cs`

**Intent**: Add reusable logic to extract only earlier sections in fixed order for a requested section.

**Contract**: Add helper methods that return ordered prior-section snapshots and a stable context hash input string.

#### 3. Helper generation service

**File**: `Services\HelperQuestionsService.cs` (new)

**Intent**: Generate contextual helper questions through Azure OpenAI using strict JSON schema and enforce output constraints.

**Contract**: Expose `Task<HelperQuestionsResult> GenerateAsync(HelperQuestionsRequest request, CancellationToken ct = default)`; throw typed failures for config/schema/response violations; enforce exactly 3-5 non-empty questions.

#### 4. Service registration

**File**: `Program.cs`

**Intent**: Register helper service in DI following existing service wiring conventions.

**Contract**: Add scoped registration for `HelperQuestionsService` while reusing existing `AzureOpenAIOptions` config.

### Success Criteria:

#### Automated Verification:

- Solution builds successfully: `dotnet build`
- Helper service compiles with strict typed contracts and no additional warnings

#### Manual Verification:

- N/A (service-only phase)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 2: Wizard/Edit integration with per-section loading, cache, refresh, and error handling

### Overview

Integrate helper questions into `WizardSection` and both pages so generation happens on first section expansion, supports manual refresh, and uses session cache.

### Changes Required:

#### 1. Wizard section UI extension

**File**: `Components\Shared\WizardSection.razor`

**Intent**: Render helper questions directly below section description and expose callbacks for first expand and manual refresh.

**Contract**: Add parameters for questions, loading, error, and refresh callback; add display area + refresh button; emit first-expand event exactly once per section lifecycle.

#### 2. Create flow orchestration

**File**: `Components\Pages\Wizard.razor`

**Intent**: Wire per-section helper state, cache keying, and service calls for the create flow.

**Contract**: Initialize `expandedSections` to collapsed state, maintain section-indexed helper state dictionary, and delegate fetch orchestration to a shared coordinator method/service used by both pages; on first expand compute context from prior sections only, check cache, then call service; support refresh to bypass cached value for same section/context; keep classify behavior unchanged.

#### 3. Edit flow orchestration

**File**: `Components\Pages\EditNote.razor`

**Intent**: Mirror create-flow helper behavior in edit mode after note data is loaded.

**Contract**: Initialize `expandedSections` to collapsed state and call the same shared helper-question coordinator used by `Wizard.razor` (no copy-paste orchestration path) for parity in `/edit/{id}`.

#### 4. Session cache support

**File**: `Services\WizardStateService.cs`

**Intent**: Store helper-question responses per section/context for current circuit session.

**Contract**: Add in-memory cache API keyed by `(sectionKey, contextHash)` with explicit clear paths for both `State.Reset()` and note-load transitions in edit flow (`LoadFromNote` / `NoteId` change), while keeping clean separation from classification result data.

### Success Criteria:

#### Automated Verification:

- Solution builds successfully after UI + service integration: `dotnet build`

#### Manual Verification:

- In `/`, first explicit expansion of a section shows loading and then 3-5 helper questions
- Re-expanding the same section with unchanged context serves cached questions without a visible second generation delay
- Clicking refresh regenerates helper questions for that section
- In `/edit/{id}`, behavior matches `/` after existing note content is loaded
- On helper-generation failure, section shows clear error and retry path, while classification button/flow still works
- Switching to a different note in `/edit/{id}` does not reuse stale helper-question cache from a previously opened note

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 3: Styling, regression validation, and completion hardening

### Overview

Polish helper-question UI and verify no regressions in existing classification/note flows.

### Changes Required:

#### 1. Helper-question styling

**File**: `wwwroot\css\app.css`

**Intent**: Add clear, lightweight styles for helper question list, loading text, error block, and refresh action.

**Contract**: Introduce styles namespaced to wizard section helper blocks without disturbing existing note/result styles.

#### 2. Final flow verification alignment

**File**: `Components\Pages\Wizard.razor`, `Components\Pages\EditNote.razor`, `Components\Shared\WizardSection.razor`

**Intent**: Ensure helper-question UX coexists with classify/save/update behavior.

**Contract**: Preserve existing `OnClassify` logic and result rendering while helper interactions remain section-local.

### Success Criteria:

#### Automated Verification:

- Solution builds successfully: `dotnet build`

#### Manual Verification:

- Helper-question list is readable and visually aligned in both flows
- Existing classification result rendering still works in `/` and `/edit/{id}`
- No regressions in note save/update success messages and navigation

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Testing Strategy

### Unit Tests:

- Not added in this slice (no existing test project in repo)

### Integration Tests:

- Not added in this slice

### Manual Testing Steps:

1. Open `/`, expand sections one by one, verify helper generation behavior and refresh
2. Complete classification in `/`, verify result panel still renders correctly
3. Open `/edit/{id}` for an existing note, verify helper behavior parity and successful update path
4. Simulate helper generation failure and verify section-level error + retry

## Performance Considerations

- Session cache prevents repeated calls for unchanged `(section, context)` inputs
- Expand-triggered generation avoids startup request burst from default expanded sections

## Migration Notes

- No database migration required

## References

- PRD: `context/foundation/prd.md`
- Roadmap slice: `context/foundation/roadmap.md`
- Existing pattern: `Services\ClassificationService.cs:101`
- Existing wizard create flow: `Components\Pages\Wizard.razor:15`
- Existing wizard edit flow: `Components\Pages\EditNote.razor:17`
- Existing section component: `Components\Shared\WizardSection.razor:9`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Helper-question domain model and generation service

#### Automated

- [x] 1.1 Solution builds successfully: `dotnet build`
- [x] 1.2 Helper service compiles with strict typed contracts and no additional warnings

#### Manual

- [ ] 1.3 N/A (service-only phase)

### Phase 2: Wizard/Edit integration with per-section loading, cache, refresh, and error handling

#### Automated

- [ ] 2.1 Solution builds successfully after UI + service integration: `dotnet build`

#### Manual

- [ ] 2.2 In `/`, first explicit expansion of a section shows loading and then 3-5 helper questions
- [ ] 2.3 Re-expanding the same section with unchanged context serves cached questions without a visible second generation delay
- [ ] 2.4 Clicking refresh regenerates helper questions for that section
- [ ] 2.5 In `/edit/{id}`, behavior matches `/` after existing note content is loaded
- [ ] 2.6 On helper-generation failure, section shows clear error and retry path, while classification button/flow still works
- [ ] 2.7 Switching to a different note in `/edit/{id}` does not reuse stale helper-question cache from a previously opened note

### Phase 3: Styling, regression validation, and completion hardening

#### Automated

- [ ] 3.1 Solution builds successfully: `dotnet build`

#### Manual

- [ ] 3.2 Helper-question list is readable and visually aligned in both flows
- [ ] 3.3 Existing classification result rendering still works in `/` and `/edit/{id}`
- [ ] 3.4 No regressions in note save/update success messages and navigation
