# Auth & Ownership Boundary — Plan Brief

> Full plan: `context/changes/testing-auth-ownership-boundary/plan.md`
> Research: `context/changes/testing-auth-ownership-boundary/research.md`

## What & Why

Fix an active data-exposure vulnerability (`GET /admin/db-check` dumps all user emails and notes to anonymous callers), then write the integration and service-level tests that prove Risk #3 (IDOR) and Risk #4 (unauthenticated data access) from the test plan are protected. This is rollout Phase 2 of `context/foundation/test-plan.md`.

## Starting Point

A passing test suite of 5 unit tests exists from Phase 1. No integration test infrastructure (`WebApplicationFactory`, EF InMemory, NSubstitute) is present. The `/admin/db-check` endpoint is live and unauthenticated. `NoteService` ownership enforcement is already correct at the service level — the tests are about proving it, not fixing it.

## Desired End State

`/admin/db-check` is removed. `dotnet test` runs 14 tests (5 original + 1 factory smoke + 5 anonymous-access + 4 IDOR), all passing. A reusable `DevNoteWebApplicationFactory` is in place for subsequent phases. Auth and ownership boundaries are covered by a suite that will catch regressions automatically in CI.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|----------|--------|------------------|--------|
| What to do with `/admin/db-check` | Remove entirely | No production use case; eliminating it is zero-maintenance and closes the attack surface permanently | Plan |
| IDOR test layer | Service-level unit tests only (Phase 2) | The Blazor page returns HTTP 200 + client-side redirect for wrong-owner, not HTTP 403 — the service predicate is the cheapest verifiable boundary | Research |
| WebApplicationFactory DB | EF InMemory | Fast startup, no infrastructure, sufficient for HTTP-level auth-boundary tests | Plan |
| Mocking library | NSubstitute (added now, used from Phase 3) | Standardise on one library before Phase 3 needs it; not wired up in Phase 2 factory | Plan |
| Logout antiforgery (`DisableAntiforgery`) | Defer | Low severity; would require Blazor form changes beyond this phase's scope | Plan |
| API key in `appsettings.Development.json` | Leave as-is for now | User will rotate separately | Plan |

## Scope

**In scope:**
- Remove `GET /admin/db-check` from `Program.cs`
- Add `public partial class Program {}` to enable `WebApplicationFactory<Program>`
- Add `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`, `NSubstitute` to `DevNote.Tests.csproj`
- `DevNoteWebApplicationFactory` with InMemory DB override and `Development` environment
- 5 anonymous-access integration tests (Risk #4)
- 4 IDOR service-level unit tests (Risk #3)

**Out of scope:**
- Fixing `DisableAntiforgery()` on the logout endpoint
- Adding a global EF query filter on `ConversationNote`
- Blazor component-level IDOR redirect tests (Phase 3)
- Rotating the hardcoded Azure OpenAI key

## Architecture / Approach

Phase 1 fixes the vulnerability and adds packages. Phase 2 stands up `DevNoteWebApplicationFactory` — a `WebApplicationFactory<Program>` subclass that replaces PostgreSQL with EF InMemory and sets `UseEnvironment("Development")` to bypass the startup `MigrateAsync` block. Phase 3 uses the factory's `HttpClient` (with `AllowAutoRedirect = false`) to assert that anonymous requests to data routes get `302` to `/login` and that `/admin/db-check` returns `404`. Phase 4 skips the HTTP layer entirely: it instantiates `NoteService` directly with an InMemory `ApplicationDbContext`, seeds notes, and asserts cross-user access returns `null` or throws.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|-------|-----------------|----------|
| 1. Security fix + packages | Vulnerability removed; build passes with new packages | `public partial class Program {}` omission breaks Phase 2 |
| 2. WebApplicationFactory test host | Factory starts app cleanly with InMemory DB | `MigrateAsync` called on InMemory DB if environment not overridden |
| 3. Anonymous-access integration tests | 5 tests covering Risk #4 routes | Using `AllowAutoRedirect = true` would miss the 302 assertion |
| 4. IDOR service tests | 4 tests covering Risk #3 ownership predicate | Testing only the owner-success path; must also assert non-owner denial |

**Prerequisites:** Phase 1 test project and xUnit runner from rollout Phase 1 (`testing-classification-summary-integrity`) must already pass.  
**Estimated effort:** ~1–2 sessions across 4 phases.

## Open Risks & Assumptions

- The hardcoded Azure OpenAI key in `appsettings.Development.json` is assumed to be rotated or non-sensitive; if live, it must be rotated before this branch is merged to a public repo.
- `DisableAntiforgery()` on the logout endpoint remains unfixed — logout CSRF is possible but low-severity; noted for a future change.
- EF InMemory does not enforce FK constraints — IDOR service tests rely on EF's LINQ predicates, which are correct, but the test setup is slightly less realistic than a real Postgres DB.

## Success Criteria (Summary)

- `dotnet test` passes with 14 tests: 5 original + 1 factory smoke + 5 anonymous-access + 4 IDOR
- GET `/admin/db-check` returns `404` (endpoint removed)
- A wrong-owner GUID passed to `NoteService.GetNoteAsync` returns `null` (asserted by a passing test)
