# Wizard State, Edit Reversion, and Degraded Helper Mode - Plan Brief

> Full plan: `context/changes/testing-wizard-state-edit-degraded-mode/plan.md`
> Research: `context/changes/testing-wizard-state-edit-degraded-mode/research.md`

## What & Why

Rollout Phase 3 protects wizard answers, note reclassification, and helper
degradation at the cheapest useful layers. It also corrects a data-integrity
gap: a note reverted to Draft currently retains old classification and summary
data that may no longer match edited answers.

## Starting Point

xUnit, NSubstitute, EF Core InMemory, and an integration host already exist.
Wizard and helper state are circuit-local, helper failure is already
non-blocking, and note lifecycle behavior exists, but these areas lack focused
tests and bUnit is not installed.

## Desired End State

Accordion navigation preserves entered answers, while page reload durability
remains explicitly out of scope. Draft notes contain no generated output,
reclassification updates the same row with fresh data, sequential helper calls
use cache, and component tests prove helper failure does not block the wizard.

## Key Decisions Made

| Decision | Choice | Why | Source |
|----------|--------|-----|--------|
| Draft durability | Same-circuit accordion only | Matches FR-005 without adding browser/DB draft persistence | Research + Plan |
| Draft generated output | Clear classification, justification, and summary | Draft must not expose stale conclusions | Plan |
| Duplicate helper calls | Protect sequential cache hits only | UI guards make concurrent duplication an unsupported MVP edge | Research + Plan |
| Component depth | WizardSection plus focused Wizard/EditNote flows | Covers user-visible risks without browser e2e | Plan |
| Note persistence in component tests | Real NoteService + isolated EF InMemory | Exercises actual lifecycle without mocking internals | Plan |
| External test doubles | LLM interfaces only | Keeps cache, state, and persistence real | Research |

## Scope

**In scope:**

- Wizard state reset/load/cache unit tests
- Clean Completed -> Draft transition and same-row reclassification tests
- `IHelperQuestionsService` and coordinator cache/degradation tests
- bUnit 2.7.2 setup and focused component tests
- `IClassificationService` for deterministic page tests
- Test-plan risk-guidance backport and cookbook entries

**Out of scope:**

- Durable unsaved drafts across reload/new circuits
- Browser e2e, live Azure tests, or exact Polish text assertions
- Concurrent in-flight helper deduplication
- `INoteService`, database migrations, or Identity internals

## Architecture / Approach

Tests move outward by cost and signal: real service units first, then a
coordinator with only its LLM edge substituted, then bUnit pages with real
wizard state and NoteService. Production abstractions are limited to the two
external AI boundaries.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|-------|------------------|----------|
| 1. State and note lifecycle | Clean Draft invariant and circuit-state tests | Stale output or lost accordion state |
| 2. Helper orchestration | Injectable LLM edge and cache/degraded tests | Duplicate cost or blocked wizard |
| 3. Component coverage | bUnit page flows and rollout cookbook | UI wiring contradicts service behavior |

**Prerequisites:** Existing xUnit project and Phase 1/2 rollout tests remain
green.

**Estimated effort:** About 3 implementation sessions across 3 phases.

## Open Risks & Assumptions

- Page leave, reload, and new-circuit draft loss are accepted MVP behavior.
- Existing stale Draft rows are repaired when next opened; no data migration is
  planned.
- Current UI guards are sufficient against concurrent helper requests.
- bUnit 2.7.2 remains compatible with the project's .NET 9 test target.

## Success Criteria (Summary)

- Draft notes never retain or display stale generated output.
- Wizard and helper component behavior is covered without live Azure or browser
  automation.
- All focused tests, full suite, formatting, and build pass.
