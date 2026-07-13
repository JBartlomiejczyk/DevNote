---
date: 2026-07-13T13:21:00+02:00
researcher: Copilot
git_commit: 10845e6a3a13f0d9fc958db84e66a055dd4bfa4d
branch: master
repository: DevNote
topic: "Auth & ownership boundary — ground Risks #3 and #4 for Phase 2 integration tests"
tags: [research, auth, authorization, ownership, integration-testing, security]
status: complete
last_updated: 2026-07-13
last_updated_by: Copilot
---

# Research: Auth & Ownership Boundary (Phase 2)

**Date**: 2026-07-13T13:21:00+02:00  
**Researcher**: Copilot  
**Git Commit**: 10845e6a3a13f0d9fc958db84e66a055dd4bfa4d  
**Branch**: master  
**Repository**: DevNote

## Research Question

Ground rollout Phase 2 of `context/foundation/test-plan.md`.

Risks to verify: Risk #3, #4.  
Risk response guidance to verify, not blindly accept:
- **Risk #3**: prove a non-owner requesting a note id is denied (not-found/forbidden), not shown another user's content; challenge "being authenticated is enough"; avoid testing only the owner-can-read happy path.
- **Risk #4**: prove anonymous requests cannot reach data-returning routes and no route leaks user emails or notes without authorization; challenge "the global FallbackPolicy protects everything"; avoid asserting only that the login page is anonymously reachable.

## Summary

The authorization architecture is **secure by default at the Blazor routing layer** — a global `FallbackPolicy` requires authentication on every undecorated route, and `AuthorizeRouteView` in `Routes.razor` enforces this before Blazor components render. Ownership enforcement is **correct and complete in `NoteService`** — every method that accepts a `noteId` also requires and applies a `n.UserId == userId` predicate, so IDOR via the service is not currently possible.

However, two **critical security vulnerabilities** were discovered that are not covered by the test plan's current risk set:

1. **`GET /admin/db-check` is publicly accessible without authentication** and returns all user email addresses and all note metadata from the entire database. This is an active mass data exposure endpoint.
2. **An Azure OpenAI API key is hardcoded in `appsettings.Development.json`** and committed to source control.

Both require immediate action before integration tests are written — a test that asserts `/admin/db-check` requires auth will fail on the current codebase and alert to the gap, but the gap itself must be closed.

For the test plan's two tracked risks:
- **Risk #4 (anonymous access to data routes)** is real and confirmed at `/admin/db-check`. The FallbackPolicy correctly covers Blazor pages but does not cover minimal API endpoints that explicitly opt out.
- **Risk #3 (IDOR ownership)** is well-mitigated at the service level. The test must still exercise the integration path to confirm the ownership predicate is applied end-to-end, and must explicitly verify that a non-owner receives not-found/redirect, not the other user's note.

## Detailed Findings

### Auth Architecture

**Global FallbackPolicy** — `Program.cs:50-55`:
```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```
Secure default: every route that does not carry explicit auth metadata requires an authenticated user. ✅

**Middleware order** — `Program.cs:93-94`:
```
app.UseAuthentication();   // line 93
app.UseAuthorization();    // line 94
```
Order is correct. ✅

**`AuthorizeRouteView` in `Routes.razor`** — distinguishes unauthenticated (redirect to `/login`) from authenticated-but-forbidden (Polish access-denied message). Correct. ✅

**Cookie configuration** — `Program.cs:38-44`: 14-day sliding expiration, `HttpOnly`/`Secure` at ASP.NET Identity defaults.

**HTTPS redirect** — `Program.cs:85-88`: Only in Development. Production relies on Railway TLS termination. Acceptable if Railway terminates TLS — no app-level redirect needed.

---

### Blazor Page Auth Status

All note-related pages are protected by FallbackPolicy — **no page carries `@attribute [Authorize]` explicitly**. They rely entirely on the global policy + `AuthorizeRouteView`.

| Page | Route | Explicit `[Authorize]` | Effective auth |
|------|-------|------------------------|----------------|
| `Wizard.razor` | `/` | none | 🔒 FallbackPolicy |
| `Notes.razor` | `/notes` | none | 🔒 FallbackPolicy |
| `EditNote.razor` | `/edit/{NoteId:guid}` | none | 🔒 FallbackPolicy |
| `Account/Login.razor` | `/login` | `[AllowAnonymous]` | 🌐 Public |
| `Account/Register.razor` | `/register` | `[AllowAnonymous]` | 🌐 Public |
| `Account/ForgotPassword.razor` | `/forgot-password` | `[AllowAnonymous]` | 🌐 Public |
| `Account/ResetPassword.razor` | `/reset-password` | `[AllowAnonymous]` | 🌐 Public |
| `Account/Logout.razor` | `/logout` | `[AllowAnonymous]` | 🌐 Public |

---

### Minimal API Endpoints

| Endpoint | Auth | Risk |
|----------|------|------|
| `GET /healthz` | `AllowAnonymous` | Low — returns `"Healthy"` only |
| **`GET /admin/db-check`** | **`AllowAnonymous`** | 🔴 **CRITICAL** — dumps all user emails + all note records |
| `POST /api/auth/logout` | `RequireAuthorization()` | Medium — antiforgery disabled (logout CSRF possible) |

**`/admin/db-check` — `Program.cs:98-103`:**
```csharp
app.MapGet("/admin/db-check", async (ApplicationDbContext db) =>
{
    var users = await db.Users.Select(u => new { u.Email }).ToListAsync();
    var notes = await db.ConversationNotes.Select(n => new { n.Id, n.Title, n.Status, n.UserId, n.CreatedAt }).ToListAsync();
    return Results.Ok(new { users, notes });
}).AllowAnonymous();
```
Any unauthenticated HTTP request receives every user email and every note record (id, title, status, userId, createdAt) across all users. This is a Risk #4 failure scenario that the test plan must cover.

---

### Note Ownership — NoteService

All public methods in `NoteService.cs` accept and enforce `userId`:

| Method | userId param | WHERE clause |
|--------|-------------|-------------|
| `CreateNoteAsync` | ✅ | Written to `note.UserId` at creation |
| `GetNotesForUserAsync` | ✅ | `.Where(n => n.UserId == userId)` |
| `GetNoteAsync` | ✅ | `n.Id == noteId && n.UserId == userId` |
| `UpdateNoteAsync` | ✅ | `n.Id == noteId && n.UserId == userId` (throws if no match) |
| `RevertToDraftAsync` | ✅ | `n.Id == noteId && n.UserId == userId` (throws if no match) |

No method fetches by id without a user filter. Service-level IDOR is not currently possible. ✅

**No global EF query filter** on `ConversationNote` — every correctness guarantee is manual per-method. Future methods without the predicate would silently expose cross-user data. Defence-in-depth gap (low severity for now).

---

### EditNote.razor — Ownership Check Path

`EditNote.razor` (`@page "/edit/{NoteId:guid}"`) — `OnInitializedAsync`:
1. `GetAuthenticationStateAsync()` → extracts `userId` from `ClaimTypes.NameIdentifier`
2. If `userId` is null → `Nav.NavigateTo("/notes")` (client-side Blazor redirect)
3. `NoteService.GetNoteAsync(NoteId, userId)` — returns `null` if not found **or** wrong owner
4. If `note` is null → `Nav.NavigateTo("/notes")`

Ownership is enforced at the service call. Wrong-owner returns `null` → client redirect to `/notes`. **No HTTP 403/404 is issued** — the response for `/edit/<any-guid>` is always `200 OK` at the HTTP layer. The Blazor runtime navigates away, but a raw HTTP client never sees an error status.

This matters for tests: a `WebApplicationFactory` HTTP-level test against `/edit/<guid>` will receive a `200` for both authorized-owner and unauthorized-non-owner requests (the auth redirect is a Blazor in-page action). Integration tests for IDOR on this route must evaluate the rendered Blazor output or work at the service level for ownership assertions.

---

### Test Infrastructure — Current State

The Phase 1 test project (`DevNote.Tests`) established:

| | Status |
|--|--------|
| Test framework | xUnit 2.9.2 ✅ |
| Test runner | `Microsoft.NET.Test.Sdk` 17.12.0 ✅ |
| Mocking library | **None — not yet added** ❌ |
| `Microsoft.AspNetCore.Mvc.Testing` | **Not present** ❌ |
| `Microsoft.EntityFrameworkCore.InMemory` | **Not present** ❌ |

Conventions established in Phase 1:
- Namespace mirrors production: `DevNote.Tests.<Area>`
- Test class name: `<ProductionClass>Tests`
- Method name: `<Method>_<Condition>_<ExpectedOutcome>`
- No AAA comment markers — blank-line separation implied
- Direct instantiation (`new`) for dependency-free classes; no mock framework introduced yet
- `dotnet test` is the CI gate command

Phase 2 must add `Microsoft.AspNetCore.Mvc.Testing` and either `Microsoft.EntityFrameworkCore.InMemory` or a Testcontainers PostgreSQL package to `DevNote.Tests.csproj`.

---

### Corrections to Test Plan Risk Response Guidance

After grounding, the following adjustments to §2 guidance are warranted:

**Risk #4** — The test plan guidance challenged "The global FallbackPolicy protects everything." Research confirms this challenge is valid: `/admin/db-check` is a minimal API endpoint that explicitly overrides the FallbackPolicy with `.AllowAnonymous()` and exposes all user data. The FallbackPolicy does NOT protect endpoints that opt out. The integration test must enumerate this endpoint explicitly and assert it requires auth (after the endpoint is fixed).

**Risk #3 (IDOR)** — The "cheapest layer" listed as `integration (WebApplicationFactory)` requires a nuance: because the ownership check produces a Blazor client-side redirect (not an HTTP 403), a WebApplicationFactory test against the Blazor page URL will not observe an HTTP-level denial. The cheapest verifiable layer for ownership is **service-level unit test** (assert `GetNoteAsync` returns `null` for wrong-owner GUID) **plus** an integration test that confirms the UI navigates away — however, the UI navigation test requires either bUnit or Playwright (deferred). For Phase 2, the service-level unit test and an integration test on `/admin/db-check` are the two actionable assertions.

## Code References

- `Program.cs:50-55` — FallbackPolicy configuration
- `Program.cs:85-88` — HTTPS redirect (Development only)
- `Program.cs:93-94` — Middleware order (UseAuthentication / UseAuthorization)
- `Program.cs:96` — `GET /healthz` AllowAnonymous (safe)
- `Program.cs:98-103` — **`GET /admin/db-check` AllowAnonymous — CRITICAL data exposure**
- `Program.cs:105-109` — `POST /api/auth/logout` RequireAuthorization + DisableAntiforgery
- `Components/Pages/Account/Login.razor:2` — `[AllowAnonymous]`
- `Components/Pages/Account/Register.razor:2` — `[AllowAnonymous]`
- `Components/Pages/Account/Logout.razor:2` — `[AllowAnonymous]`
- `Components/Pages/EditNote.razor` — IDOR ownership path, client-side redirect on null note
- `Components/Pages/Notes.razor` — list page, no `[Authorize]` (relies on FallbackPolicy)
- `Components/Pages/Wizard.razor` — AI classification runs before userId auth check (resource abuse gap)
- `Services/NoteService.cs:69-71` — `GetNoteAsync` — `n.Id == noteId && n.UserId == userId` ✅
- `Services/NoteService.cs:76-78` — `UpdateNoteAsync` — same predicate + throw ✅
- `Services/NoteService.cs:111-113` — `RevertToDraftAsync` — same predicate + throw ✅
- `Data/ApplicationDbContext.cs` — no global query filter on `ConversationNote`
- `appsettings.Development.json:14` — **Hardcoded Azure OpenAI API key — CRITICAL**
- `DevNote.Tests/DevNote.Tests.csproj` — missing `Microsoft.AspNetCore.Mvc.Testing`

## Architecture Insights

1. **Security model: deny-by-default at routing, explicit opt-in for public routes.** This is the right pattern. The FallbackPolicy + `AuthorizeRouteView` combination means every new Blazor page is automatically protected without needing `[Authorize]`. The risk is the inverse: minimal API endpoints bypass the Blazor pipeline entirely and can accidentally expose data via `.AllowAnonymous()`.

2. **Ownership enforcement is a service concern, not a page concern.** Pages obtain `userId` from `AuthenticationStateProvider` and pass it to service methods. The service enforces the `UserId == userId` predicate. This is the correct separation. The integration test should test the service boundary, not the HTTP/Blazor boundary, for IDOR assertions.

3. **WebApplicationFactory tests will see 200 OK for all Blazor routes** when the app is running correctly — Blazor Server renders the shell and navigates in-page. HTTP-level auth redirects only fire for routes that fail the FallbackPolicy before the Blazor pipeline starts. For `/edit/<guid>`, the FallbackPolicy check fires first (authenticated user required) — a truly anonymous request gets a `302` to `/login`. An *authenticated non-owner* gets `200` + Blazor redirect to `/notes`. This distinction is important for test assertions.

4. **Missing packages blockers for Phase 2:**
   - `Microsoft.AspNetCore.Mvc.Testing` — required for `WebApplicationFactory<Program>`
   - `Microsoft.EntityFrameworkCore.InMemory` — needed to override PostgreSQL in test host
   - A mocking library (NSubstitute recommended for .NET) — needed to fake `IAzureOpenAIClient` / `IEmailSender` so integration tests don't require live external services

## Historical Context

- `context/changes/testing-classification-summary-integrity/plan.md` — Phase 1 plan; explicitly defers integration tests; establishes xUnit as test framework; `dotnet test` as CI gate
- `context/changes/auth-and-note-persistence/plan.md` — implemented Identity auth + note persistence; designed `NoteService` ownership predicate
- `context/changes/note-management/plan.md` — note CRUD + EditNote page; the `NavigateTo("/notes")` ownership redirect pattern originated here
- `context/foundation/lessons.md` — one lesson: never use `@oninput` for text fields in Blazor Server on high-latency connections; not relevant to this phase

## Open Questions

1. **`/admin/db-check` — fix before or during Phase 2?** The endpoint must be removed or secured before meaningful Risk #4 integration tests can pass. Should this be a prerequisite sub-phase of Phase 2 or a separate hotfix change?

2. **Hardcoded API key in `appsettings.Development.json`** — is the key already rotated (it may be a dev-only throwaway)? If the key is live, it must be rotated immediately and removed from git history.

3. **IDOR test strategy — service-level vs. HTTP-level?** Given the Blazor-redirect-not-HTTP-403 architecture, the highest-signal test for Risk #3 is a service-level unit test asserting `GetNoteAsync(noteId, wrongUserId)` returns `null`. Should Phase 2 add this as a unit test or wait for the bUnit phase (Phase 3) to test the full EditNote redirect path?

4. **`WebApplicationFactory` database strategy** — InMemory provider (fast, no infrastructure) vs. Testcontainers PostgreSQL (matches production, slower CI). Decision needed before Phase 2 plan is written.

5. **`POST /api/auth/logout` antiforgery disabled** — acceptable trade-off or should it be fixed in Phase 2? Low priority but worth a note in the plan.
