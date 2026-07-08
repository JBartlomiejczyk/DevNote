# Testing classification-summary integrity — Plan Brief

> Full plan: `context/changes/testing-classification-summary-integrity/plan.md`
> Research: `context/changes/testing-classification-summary-integrity/research.md`

## What & Why

We are implementing Phase 1 of the quality rollout: first xUnit harness plus strict integrity enforcement for classification/summary parsing. The goal is to eliminate silent trust in malformed or partial LLM output so developers never see (or persist) blank-but-valid summaries.

## Starting Point

Current parsing lives in `ClassificationService` and includes permissive behavior (unknown class fallback to `B`, null-to-empty summary coercion). The repo has no test project yet, and CI currently runs restore/build without a test gate.

## Desired End State

`DevNote.Tests` exists and runs in CI. Classification response policy is isolated in a validator/mapper component with strict fail-fast rules: only A/B/C accepted, and all 11 required summary fields must be non-empty. Invalid responses are surfaced as errors and never silently rendered/persisted as valid-looking output.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Parser testability strategy | Extract validator/mapper component | Gives high-signal unit coverage with lower coupling than exposing internals only | Plan |
| Unknown classification behavior | Fail-fast invalid response | Prevents silent business-level misclassification under model drift | Plan |
| Summary completeness policy | All 11 summary fields required and non-empty | Matches PRD contract and blocks blank-but-valid output risk | Plan |
| Phase 1 test scope | Validator+policy tests plus mapping smoke checks | Covers Risk #1 thoroughly without expanding into later-phase suites | Plan |
| CI gate timing | Add required `dotnet test` in this phase | Enforces the new baseline immediately for PR safety | Plan |

## Scope

**In scope:**
- Bootstrap `DevNote.Tests` xUnit project for .NET 9
- Extract and enforce strict response validation/mapping policy
- Add unit tests for malformed/partial/empty response cases
- Add `dotnet test` to CI workflow as required gate

**Out of scope:**
- Integration/bUnit/e2e rollout
- Auth/ownership risk coverage
- Broader wizard UX redesign

## Architecture / Approach

Keep transport orchestration in `ClassificationService`, but move response-policy logic into a dedicated validator/mapper component with explicit contract errors. Unit tests target this component directly, while existing page-level error handling remains the user-facing surface for invalid responses.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Bootstrap test harness and isolate response policy | First xUnit project + validator seam in runtime | Over-scoping extraction beyond Phase 1 |
| 2. Enforce integrity policy and cover Risk #1 with unit tests | Strict fail-fast behavior + complete unit suite for malformed/partial responses | Policy too strict causing temporary increase in visible errors |
| 3. Activate CI quality gate for Phase 1 | Required `dotnet test` in workflow | Pipeline instability from newly introduced tests |

**Prerequisites:** Existing change + research artifacts; access to update workflow and test project structure  
**Estimated effort:** ~2-3 sessions across 3 phases

## Open Risks & Assumptions

- Assumes strict non-empty enforcement for all 11 summary fields is acceptable product behavior.
- Assumes invalid-response surfacing can reuse current generic error banner for this phase.
- Assumes CI environment is ready for test execution without additional infrastructure dependencies.

## Success Criteria (Summary)

- Invalid/malformed/partial model responses no longer produce silently trusted output.
- Unit test suite exists and protects parser/validator integrity policy.
- CI blocks merges when unit tests fail.
