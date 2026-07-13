# Auth & Ownership Boundary — Implementation Plan

## Overview

Implement rollout Phase 2 of the test plan: fix the active data-exposure vulnerability (`/admin/db-check`), establish WebApplicationFactory integration test infrastructure, and write integration + service-level tests that prove (a) unauthenticated requests to data routes are denied and (b) a user cannot access another user's note via the service layer.

## Current State Analysis

The authorization architecture is correct at the Blazor routing layer — a global `FallbackPolicy` requires authentication on every undecorated route. `NoteService` enforces ownership on every method. However:

- `GET /admin/db-check` (`Program.cs:98-103`) is publicly accessible and returns all user emails and all note records. This is an active data-exposure vulnerability.
- No integration test infrastructure exists yet (`Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`, NSubstitute are all absent from `DevNote.Tests.csproj`).
- `Program` (top-level statements) generates an `internal` class — the test project cannot reference it in `WebApplicationFactory<Program>` without a `public partial class Program {}` declaration.

## Desired End State

After this plan:
- `/admin/db-check` is removed from the app.
- Running `dotnet test` executes 9+ new tests covering Risk #3 and Risk #4 with zero failures.
- A reusable `DevNoteWebApplicationFactory` is in place for future phases (Phase 3 wizard/bUnit, Phase 4 CI gates).
- `dotnet build` and `dotnet test` both pass in CI.

### Key Discoveries

- `NoteService` constructor: `(ApplicationDbContext db)` only — no ILogger, no other dependencies. Direct instantiation with an InMemory DbContext is straightforward. (`Services/NoteService.cs:11`)
- `AzureOpenAIClient` is constructed inline inside `ClassifyAsync` (`Services/ClassificationService.cs:138`) — nothing in DI to fake for these tests. Auth-boundary tests never trigger classification calls.
- Auto-migration block in `Program.cs:78-83` runs when environment is not "Development". The test factory must set `UseEnvironment("Development")` to bypass it — InMemory DB auto-creates schema on first use.
- `IEmailSender<ApplicationUser>` is registered but only called during password-reset / confirmation flows — none of which are triggered by Phase 2–4 tests. No fake is needed in the factory for this phase; NSubstitute is installed now for future phases.
- EF InMemory does not enforce FK constraints, so service tests can seed `ConversationNote` rows with arbitrary `UserId` strings without creating real `ApplicationUser` records.

## What We're NOT Doing

- Not fixing the `DisableAntiforgery()` on `POST /api/auth/logout` — noted as a future item.
- Not adding a global EF query filter (`HasQueryFilter`) on `ConversationNote` — defence-in-depth improvement deferred.
- Not rotating the hardcoded Azure OpenAI API key in `appsettings.Development.json` — user will handle separately.
- Not testing the Blazor client-side redirect for non-owner access to `/edit/<guid>` — deferred to Phase 3 (bUnit).
- Not adding a `SeedUserAsync` helper to the factory — not needed until Phase 3 wizard tests require authenticated HTTP clients.

## Implementation Approach

Four phases in strict order:
1. Eliminate the vulnerability and add packages — gates that must pass before any test code is added.
2. Stand up the test host — a prerequisite for HTTP-level integration tests.
3. Write Risk #4 tests (anonymous access) — uses the test host.
4. Write Risk #3 tests (IDOR) — uses EF InMemory directly, no HTTP layer needed.

## Critical Implementation Details

**`public partial class Program {}`** — Top-level statements in C# generate an `internal class Program`. `WebApplicationFactory<Program>` in a separate assembly cannot reference it without this one-line addition at the bottom of `Program.cs`. This is the canonical pattern; it has no runtime effect.

**`UseEnvironment("Development")` in the factory** — The `Program.cs` auto-migration block runs when `!app.Environment.IsDevelopment()`. With EF InMemory, `MigrateAsync()` would throw or be a no-op depending on EF Core version. Setting `Development` environment in the factory bypasses the block entirely. InMemory DB creates its schema automatically on first `SaveChanges`/query.

**Per-test InMemory DB isolation** — Pass a unique `databaseName` (`Guid.NewGuid().ToString()`) to `UseInMemoryDatabase` in both the factory and the service-level tests. Sharing a name across test classes causes cross-test state bleed since InMemory databases are process-global singletons keyed by name.

---

## Phase 1: Security Fix + Package Preparation

### Overview

Remove the data-exposure endpoint, expose `Program` to the test project, and add the three packages the test infrastructure requires. Nothing in this phase adds test code — it only creates the preconditions every later phase depends on.

### Changes Required

#### 1. Remove `/admin/db-check` endpoint

**File**: `Program.cs`

**Intent**: Delete lines 98–103 entirely. The endpoint served no production purpose and is an active mass data-exposure risk. No replacement is needed.

**Contract**: After removal, a GET request to `/admin/db-check` must return `404 Not Found`.

#### 2. Expose `Program` class to the test project

**File**: `Program.cs`

**Intent**: Append a `public partial class Program {}` declaration at the end of the file (after `app.Run()`). This makes the auto-generated `Program` class visible to `WebApplicationFactory<Program>` in the test assembly.

**Contract**: The declaration must be at file scope, after `app.Run();`, with no namespace wrapper. No runtime behavior changes.

#### 3. Add test infrastructure packages

**File**: `DevNote.Tests/DevNote.Tests.csproj`

**Intent**: Add three `<PackageReference>` entries required for the integration test infrastructure:
- `Microsoft.AspNetCore.Mvc.Testing` — provides `WebApplicationFactory<TEntryPoint>`
- `Microsoft.EntityFrameworkCore.InMemory` — provides `UseInMemoryDatabase` for the test host
- `NSubstitute` — mocking library for future phases (installed now, used from Phase 3 onward)

**Contract**: Use the same major version family as the existing packages (`.NET 9`-compatible). After `dotnet restore`, all three resolve without version conflicts.

### Success Criteria

#### Automated Verification

- `dotnet restore` exits 0 with all three new packages resolved
- `dotnet build` exits 0 for both `dev-note` and `DevNote.Tests` projects
- `dotnet test` exits 0 (existing 5 tests still pass)

#### Manual Verification

- GET `/admin/db-check` returns 404 when the app is running locally

**Pause here for manual confirmation before proceeding to Phase 2.**

---

## Phase 2: WebApplicationFactory Test Host

### Overview

Create the `DevNoteWebApplicationFactory` — a custom `WebApplicationFactory<Program>` that the integration tests in Phase 3 consume. It replaces PostgreSQL with EF InMemory, bypasses the auto-migration block, and validates that the app starts cleanly in a test context.

### Changes Required

#### 1. Create the test factory

**File**: `DevNote.Tests/Infrastructure/DevNoteWebApplicationFactory.cs`

**Intent**: Subclass `WebApplicationFactory<Program>`. Override `ConfigureWebHost` to: (a) set environment to `"Development"` to skip the `MigrateAsync` startup block; (b) remove the registered `DbContextOptions<ApplicationDbContext>` and replace it with an InMemory provider using a unique database name.

**Contract**:
```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseEnvironment("Development");
    builder.ConfigureServices(services =>
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
        if (descriptor is not null) services.Remove(descriptor);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
    });
}
```

#### 2. Create a factory smoke test

**File**: `DevNote.Tests/Infrastructure/DevNoteWebApplicationFactoryTests.cs`

**Intent**: A single `[Fact]` that creates the factory, calls `CreateClient()`, and makes a GET to `/healthz`. This confirms the app starts cleanly in the test host — if startup fails, this test surfaces the error before the real test suites run.

**Contract**: Test method name: `Factory_Starts_And_HealthzReturns200`. Assert `response.StatusCode == HttpStatusCode.OK`.

### Success Criteria

#### Automated Verification

- `dotnet test` exits 0 with the new smoke test passing

#### Manual Verification

- The smoke test passes with no warnings about missing configuration

**Pause here before Phase 3.**

---

## Phase 3: Risk #4 — Anonymous-Access Integration Tests

### Overview

Prove that unauthenticated HTTP requests to data-returning routes are denied. This directly tests the test plan Risk #4 response: "anonymous requests to any data-returning route are redirected/denied." Uses `DevNoteWebApplicationFactory` with a non-redirecting client.

### Changes Required

#### 1. Add anonymous access test class

**File**: `DevNote.Tests/Integration/AnonymousAccessTests.cs`

**Intent**: Five `[Fact]` tests using a `HttpClient` configured with `AllowAutoRedirect = false` (so we observe the raw `302`, not the final `/login` page). The factory is shared via `IClassFixture<DevNoteWebApplicationFactory>`.

**Contract**: Test method names follow the convention `<Scenario>_<Condition>_<ExpectedOutcome>`. Use `CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })` to capture redirect status codes directly.

Tests to implement:

| Test name | Request | Expected status | Expected `Location` header |
|-----------|---------|-----------------|---------------------------|
| `GetRootPage_Anonymous_RedirectsToLogin` | GET `/` | 302 | `/login` |
| `GetNotesPage_Anonymous_RedirectsToLogin` | GET `/notes` | 302 | `/login` |
| `GetEditNotePage_Anonymous_RedirectsToLogin` | GET `/edit/00000000-0000-0000-0000-000000000001` | 302 | `/login` |
| `GetHealthz_Anonymous_Returns200` | GET `/healthz` | 200 | — |
| `GetAdminDbCheck_AfterRemoval_Returns404` | GET `/admin/db-check` | 404 | — |

**Anti-pattern to avoid**: Do not assert only that `/login` is reachable anonymously. The tests must enumerate protected routes specifically.

### Success Criteria

#### Automated Verification

- `dotnet test` exits 0 with all 5 new tests passing (plus the Phase 2 smoke test and existing 5 unit tests)

#### Manual Verification

- Review the test output: confirm 5 new passing tests appear under `DevNote.Tests.Integration`
- Run `dotnet test --filter "FullyQualifiedName~AnonymousAccessTests"` in isolation to confirm no cross-test interference

**Pause here before Phase 4.**

---

## Phase 4: Risk #3 — IDOR Service Tests

### Overview

Prove that `NoteService` cannot return one user's note to a different user. Tests operate directly against `NoteService` instantiated with an EF InMemory `ApplicationDbContext` — no HTTP layer. This is the cheapest layer that exercises the ownership predicate end-to-end.

### Changes Required

#### 1. Add IDOR service test class

**File**: `DevNote.Tests/Services/NoteServiceTests.cs`

**Intent**: Four `[Fact]` tests. Each test creates a fresh InMemory `ApplicationDbContext`, seeds User A's note directly via EF, then calls `NoteService` methods with User B's id and asserts denial. Namespace: `DevNote.Tests.Services`.

**Contract**: Seed data directly via `db.ConversationNotes.Add(...)` + `await db.SaveChangesAsync()` (no need to create `ApplicationUser` rows — InMemory does not enforce FK constraints). Use two distinct userId strings: `"user-a"` and `"user-b"`. Create a `WizardData` helper or use `new WizardData()` with minimal non-null strings for the `CreateNoteAsync` call.

A minimal `ClassificationResult` is also needed for `CreateNoteAsync`; seed the note directly via EF rather than going through `CreateNoteAsync` to keep tests independent of the classification model.

Tests to implement:

| Test name | Call | Expected outcome |
|-----------|------|-----------------|
| `GetNoteAsync_WrongOwner_ReturnsNull` | `GetNoteAsync(note.Id, "user-b")` | `null` |
| `GetNoteAsync_CorrectOwner_ReturnsNote` | `GetNoteAsync(note.Id, "user-a")` | note with matching Id |
| `UpdateNoteAsync_WrongOwner_ThrowsInvalidOperation` | `UpdateNoteAsync(note.Id, "user-b", ...)` | `InvalidOperationException` |
| `RevertToDraftAsync_WrongOwner_ThrowsInvalidOperation` | `RevertToDraftAsync(note.Id, "user-b")` | `InvalidOperationException` |

**Anti-pattern to avoid**: Do not assert only the happy path (owner can read their own note). Every test that covers wrong-owner must use a GUID that belongs to a different user — not a random or non-existent GUID — to confirm the predicate distinguishes ownership from non-existence.

### Success Criteria

#### Automated Verification

- `dotnet test` exits 0 with all 4 new tests passing (total: 14 tests — 5 original + 1 smoke + 5 anonymous + 4 IDOR)
- `dotnet test --filter "FullyQualifiedName~NoteServiceTests"` runs in isolation with no failures

#### Manual Verification

- Confirm test output shows `DevNote.Tests.Services.NoteServiceTests` with 4 passing tests
- Confirm wrong-owner tests explicitly assert `null` / `InvalidOperationException`, not just that no exception is thrown

**Pause here — Phase 4 complete.**

---

## Testing Strategy

### Unit Tests (Phase 4)

- `GetNoteAsync` returns `null` for wrong-owner GUID (oracle: PRD §Access Control "each user sees only their own notes")
- `GetNoteAsync` returns the note for the correct owner
- `UpdateNoteAsync` throws `InvalidOperationException` for wrong-owner GUID
- `RevertToDraftAsync` throws `InvalidOperationException` for wrong-owner GUID

### Integration Tests (Phase 3)

- Anonymous GET to each data-bearing Blazor route → `302` to `/login`
- `GET /healthz` remains accessible anonymously → `200`
- `GET /admin/db-check` is gone → `404`

### Manual Testing Steps

1. Start the app locally (`dotnet run`), attempt `GET /admin/db-check` — confirm `404`
2. Run `dotnet test` — confirm 14 tests pass
3. Run `dotnet test --filter "FullyQualifiedName~NoteServiceTests"` — confirm 4 tests pass in isolation

## Migration Notes

Phase 1 removes `GET /admin/db-check` from `Program.cs`. This is a breaking change for anyone calling that endpoint — which should be no one in production. No data migration is required. CI will surface the deletion on the next PR.

## References

- Research: `context/changes/testing-auth-ownership-boundary/research.md`
- Test plan: `context/foundation/test-plan.md` §2 Risk #3, #4 and §3 Phase 2
- Phase 1 plan (conventions): `context/changes/testing-classification-summary-integrity/plan.md`
- Vulnerability: `Program.cs:98-103` (lines to delete in Phase 1)
- Ownership predicate: `Services/NoteService.cs:69-71`
- WebApplicationFactory docs note: `public partial class Program {}` pattern is required for top-level-statement entry points

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Security fix + package preparation

#### Automated

- [x] 1.1 `dotnet restore` exits 0 with all three new packages resolved — 7519b43
- [x] 1.2 `dotnet build` exits 0 for both `dev-note` and `DevNote.Tests` projects — 7519b43
- [x] 1.3 `dotnet test` exits 0 (existing 5 tests still pass) — 7519b43

#### Manual

- [x] 1.4 GET `/admin/db-check` returns 404 when the app is running locally — 7519b43

### Phase 2: WebApplicationFactory test host

#### Automated

- [x] 2.1 `dotnet test` exits 0 with the factory smoke test passing

#### Manual

- [x] 2.2 Smoke test passes with no warnings about missing configuration

### Phase 3: Risk #4 — Anonymous-access integration tests

#### Automated

- [ ] 3.1 `dotnet test` exits 0 with all 5 new anonymous-access tests passing
- [ ] 3.2 `dotnet test --filter "FullyQualifiedName~AnonymousAccessTests"` passes in isolation

#### Manual

- [ ] 3.3 Test output shows 5 passing tests under `DevNote.Tests.Integration`

### Phase 4: Risk #3 — IDOR service tests

#### Automated

- [ ] 4.1 `dotnet test` exits 0 with all 4 new IDOR tests passing (14 total)
- [ ] 4.2 `dotnet test --filter "FullyQualifiedName~NoteServiceTests"` passes in isolation

#### Manual

- [ ] 4.3 Test output shows 4 passing tests under `DevNote.Tests.Services` with wrong-owner assertions confirmed
