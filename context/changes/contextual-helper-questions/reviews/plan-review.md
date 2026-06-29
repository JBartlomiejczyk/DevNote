<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Contextual helper questions Implementation Plan

- **Plan**: `context/changes/contextual-helper-questions/plan.md`
- **Mode**: Deep
- **Date**: 2026-06-29
- **Verdict**: SOUND
- **Findings**: 1 critical, 2 warnings, 0 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | PASS |
| Plan Completeness | PASS |

## Grounding
Grounding: 6/6 paths ✓, 3/3 symbols ✓, brief↔plan ✓

## Findings

### F1 — Trigger condition can prevent helper questions from appearing

- **Severity**: ❌ CRITICAL
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: End-State Alignment
- **Location**: Critical Implementation Details (Timing & lifecycle), Phase 2
- **Detail**: Plan required generation on first explicit expansion while both pages currently initialize sections as expanded. This could miss helper generation before user input.
- **Fix A ⭐ Recommended**: Start sections collapsed by default and keep first-expand trigger
  - Strength: Aligns trigger semantics with explicit user action and avoids startup call bursts.
  - Tradeoff: Changes default accordion UX from initially expanded to initially collapsed.
  - Confidence: HIGH — current create/edit pages both initialize all sections expanded.
  - Blind spot: User preference for initially expanded sections was not validated in this review.
- **Fix B**: Keep initial expanded UX but add guarded first-load path for visible sections
  - Strength: Preserves current UX while still meeting generation-on-open behavior.
  - Tradeoff: More lifecycle complexity and greater risk of accidental multi-call startup behavior.
  - Confidence: MEDIUM — feasible but needs careful render guard implementation.
  - Blind spot: Potential race behavior under rapid render/toggle was not verified.
- **Decision**: FIXED (Fix A)

### F2 — Cache invalidation gap in edit flow

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 2 — Session cache support
- **Detail**: Cache clear was tied only to `Reset()`, while edit flow loads note data via `LoadFromNote(...)`; this could leak stale helper cache across notes in a shared circuit.
- **Fix**: Clear helper-question cache on note-load transitions (`LoadFromNote` / note change), not only on reset.
- **Decision**: FIXED

### F3 — Reuse intent is stated, but no anti-drift mechanism is planned

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Architectural Fitness
- **Location**: Phase 2 — Create/Edit orchestration
- **Detail**: The plan requested parity between create/edit flows but did not define a concrete shared orchestration surface, risking copy-paste divergence.
- **Fix**: Require both pages to call a shared helper-question orchestration path instead of duplicating fetch orchestration.
- **Decision**: FIXED
