<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Auth & Ownership Boundary

- **Plan**: context/changes/testing-auth-ownership-boundary/plan.md
- **Scope**: All 4 phases
- **Date**: 2026-07-14
- **Verdict**: APPROVED
- **Findings**: 0 critical, 0 warnings, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Success criteria: `dotnet test` → 15/15 passing (5 original + 1 factory smoke + 5 anonymous-access + 4 IDOR service).

## Findings

### F1 — Factory InMemory DB is per-factory, not per-test

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; narrowly scoped
- **Dimension**: Architecture
- **Location**: DevNote.Tests/Infrastructure/DevNoteWebApplicationFactory.cs:21
- **Detail**: The `Guid.NewGuid()` DB name is evaluated once when `ConfigureWebHost` runs (factory construction), so every test sharing the factory via `IClassFixture` shares ONE InMemory database. Harmless for this change — the anonymous-access tests never seed or mutate data. But it's a latent trap for any future phase that seeds authenticated data through the factory: tests would bleed state into each other. `NoteServiceTests` (Phase 4) correctly sidesteps this by using its own per-test DbContext with a fresh Guid-keyed DB.
- **Fix**: Leave as-is for this change. When a future phase needs data-seeding HTTP tests, switch to a per-test DB (fixture Dispose reset or respawn strategy). Candidate lesson.
- **Decision**: SKIPPED

### F2 — Extra Location assertion on GetEditNotePage test

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: DevNote.Tests/Integration/AnonymousAccessTests.cs:44
- **Detail**: The plan specified only "expect 302" for this test, but the implementation also asserts the `Location` header contains `/login`. This is a strictly stronger, safe assertion consistent with every other redirect test in the file.
- **Fix**: None needed — keep it. Beneficial consistency.
- **Decision**: SKIPPED

## Notes

- Plan adaptation (documented, not drift): `GetAdminDbCheck_AfterRemoval_Returns404` was renamed to `GetAdminDbCheck_AfterRemoval_RedirectsToLogin` and asserts `302 → /login`. Rationale: the global `FallbackPolicy` intercepts unauthenticated requests before routing can return `404` for the now-removed endpoint. This is a stronger security assertion than the original `404`.
- Scope guardrails all respected: `DisableAntiforgery()` on `/api/auth/logout` left in place, no EF global query filter added, Azure OpenAI key not rotated, no `SeedUserAsync` helper — all matching the plan's "What We're NOT Doing" list.
