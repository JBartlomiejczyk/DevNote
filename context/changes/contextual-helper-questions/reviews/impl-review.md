<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Contextual helper questions Implementation Plan

- **Plan**: `context/changes/contextual-helper-questions/plan.md`
- **Scope**: Full plan (Phases 1-3)
- **Date**: 2026-06-30
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 5 warnings, 0 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

## Findings

### F1 — Missing guard for empty LLM response content

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `Services/HelperQuestionsService.cs:112`
- **Detail**: `completion.Value.Content[0].Text` assumed non-empty content and could fail before controlled error handling.
- **Fix**: Add explicit guard for empty `Content`/`Text` and throw `HelperQuestionsResponseException`.
- **Decision**: FIXED

### F2 — eval-based JS interop in Wizard flow

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `Components/Pages/Wizard.razor:234`
- **Detail**: Wizard used `JS.InvokeVoidAsync("eval", ...)` while EditNote used `devNote.scrollToResultAnchor`.
- **Fix**: Replace eval call with `devNote.scrollToResultAnchor`.
- **Decision**: FIXED

### F3 — Raw exception message exposed to end user

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `Components/Pages/Wizard.razor:223-226`
- **Detail**: Wizard surfaced `ex.Message` directly, unlike EditNote's generic user-safe message.
- **Fix**: Align Wizard with generic error message and keep detail in logs.
- **Decision**: FIXED

### F4 — Shared orchestration intent not fully realized (duplication remains)

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Pattern Consistency
- **Location**: `Components/Pages/Wizard.razor:159-201`, `Components/Pages/EditNote.razor:178-220`
- **Detail**: Helper-state orchestration logic was duplicated across Wizard/Edit despite plan-review requirement for shared path.
- **Fix A ⭐ Recommended**: Extract shared page-level orchestration utility/service and use it in both pages.
  - Strength: Enforces behavior parity and reduces drift risk.
  - Tradeoff: Small refactor touching both page components.
  - Confidence: MEDIUM — extraction path is clear, but lifecycle details needed verification.
  - Blind spot: None significant.
- **Fix B**: Keep duplication and document as accepted tradeoff.
  - Strength: No immediate refactor risk.
  - Tradeoff: Maintains future drift risk.
  - Confidence: HIGH — documentation-only.
  - Blind spot: Future contributors can still diverge behavior.
- **Decision**: FIXED (Fix A)

### F5 — Extra unplanned files included in feature commits

- **Severity**: ⚠️ WARNING
- **Impact**: 🔬 HIGH — architectural stakes; think carefully before deciding
- **Dimension**: Scope Discipline
- **Location**: commit range for `contextual-helper-questions`
- **Detail**: Feature commits included unrelated files (`.github/**`, `docker-compose.yml`, `wwwroot/js/app.js`, `context/changes/note-management/**`).
- **Fix A ⭐ Recommended**: Create follow-up cleanup commit reverting unrelated paths to pre-feature state.
  - Strength: Restores clean feature scope.
  - Tradeoff: Requires careful path-level revert and verification.
  - Confidence: MEDIUM — feasible, but must be verified path-by-path.
  - Blind spot: Some files may have hidden coupling expectations.
- **Fix B**: Keep as-is and document scope addendum.
  - Strength: Avoids additional churn.
  - Tradeoff: Scope-discipline debt remains in history.
  - Confidence: HIGH — docs-only path.
  - Blind spot: Audit trail still noisy.
- **Decision**: FIXED (Fix A)
