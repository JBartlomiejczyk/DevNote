# Contextual helper questions — Plan Brief

> Full plan: `context/changes/contextual-helper-questions/plan.md`

## What & Why

We are adding AI-generated helper questions (3-5) to each wizard section to guide developers while they capture stakeholder conversations. The goal is to improve input quality during note creation/editing, not only at final classification time.

## Starting Point

The app already has two wizard flows (`/` and `/edit/{id}`), reusable section components, and an Azure OpenAI classification service with strict schema parsing. There is currently no per-section guidance feature and both flows initialize all sections expanded, which can create request storms if implemented naively.

## Desired End State

When a user opens a section, they receive contextual helper questions derived from earlier sections in the fixed wizard order. The behavior is consistent in create and edit flows, supports manual refresh, and uses session cache to limit duplicate calls. If generation fails, the section shows error + retry while the main classification flow remains unaffected.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Scope | Wizard + edit only, no DB persistence | Matches S-04 value while avoiding unnecessary schema/migration scope |
| Trigger strategy | First section expand + manual refresh | Prevents startup call burst and gives user control over regeneration |
| Context rule | Only earlier sections in fixed 8-step order | Aligns with PRD wording (“previous sections”) and keeps behavior deterministic |
| Failure behavior | No static fallback; show section error + retry | Keeps behavior explicit and simpler for MVP while preserving user agency |
| Cost control | Session cache by section + context hash | Reduces repeated LLM calls with low implementation complexity |
| Response contract | Strict JSON schema with required 3-5 questions | Ensures predictable rendering and guards UI from malformed model output |
| Testing baseline | `dotnet build` + manual verification in both flows | Fits current repo reality (no test project) while covering key risks |

## Scope

**In scope:**
- Helper question service and typed models
- Wizard/Edit integration with per-section state
- Session-level cache
- Refresh, loading, and error UI states
- Regression-safe coexistence with classification flow

**Out of scope:**
- Persisting helper questions in DB
- New dedicated HTTP endpoint
- Adding a new test project in this change
- Static fallback question catalog

## Architecture / Approach

Use an internal scoped service (`HelperQuestionsService`) following existing `ClassificationService` OpenAI patterns. `WizardSection` gets helper-question UI and callbacks. `Wizard.razor` and `EditNote.razor` orchestrate section events, context extraction, cache lookups, and retries. `WizardStateService` stores per-session helper cache.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Model + service | Contracts, schema validation, generation logic, DI wiring | Schema/model mismatch causing parse failures |
| 2. UI integration | Expand-triggered generation, refresh, cache, section-level error state in both flows | Logic drift between create/edit implementations |
| 3. Polish + regression checks | Styles and final behavior verification with no classification regressions | Hidden UX regressions in existing wizard/save flow |

**Prerequisites:** S-01 already implemented; Azure OpenAI config available.
**Estimated effort:** ~2-3 sessions across 3 phases.

## Open Risks & Assumptions

- Duplicate logic in create/edit pages may increase maintenance overhead unless kept aligned carefully
- Strict schema can surface more user-visible errors if model occasionally returns invalid shape
- Session cache controls cost per session, but not across users/sessions

## Success Criteria (Summary)

- Users see 3-5 contextual helper questions per section in both `/` and `/edit/{id}`
- Generation is not triggered in a way that causes bulk startup request storms
- Existing classify/save/update flows continue to work without regressions
