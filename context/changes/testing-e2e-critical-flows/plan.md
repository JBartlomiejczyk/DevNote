# E2E Critical Flows Implementation Plan

## Overview

Phase 5 of the test rollout. Adds a Playwright for .NET E2E test project (`DevNote.E2eTests`) that verifies risks #2, #3, #4, and #5 in a real Chromium browser against the hosted Blazor Server app. No E2E infrastructure exists yet — everything is new. Tests run against an in-process real Kestrel server with InMemory DB and mocked LLM services; no Azure OpenAI key and no real Postgres are needed.

## Current State Analysis

- `DevNote.Tests` has xUnit + bUnit + WebApplicationFactory integration tests; no Playwright reference exists
- CI runs only `dotnet test DevNote.Tests/DevNote.Tests.csproj`; no E2E job
- All pages use `@rendermode InteractiveServer` — Blazor Server circuit over SignalR/WebSocket; `TestServer` (in-memory transport) cannot serve Playwright because it cannot bind a real socket for WebSocket upgrade
- `IClassificationService` and `IHelperQuestionsService` make server-side HTTP calls to Azure OpenAI — **these cannot be intercepted via `page.RouteAsync`** (which intercepts browser-originated traffic); they must be replaced in DI with stubs
- Auth is cookie-based: `LoginPath = "/login"`, `FallbackPolicy = RequireAuthenticatedUser`; login form uses `GetByLabel("Email")` + `GetByLabel("Hasło")` locators
- `WizardStateService` is `AddScoped` — state lives per Blazor circuit; same-circuit accordion collapse-then-re-expand is the Risk #2 test scope; cross-circuit preservation is accepted negative space (test plan §7)
- `EditNote.razor` `OnInitializedAsync` calls `RevertToDraftAsync` immediately on load — the intermediate Draft state is observable in `/notes` list before re-classification
- Notes list shows status as Polish text "Szkic" / "Ukończona" and classification badge text "A"/"B"/"C"

## Desired End State

`DevNote.E2eTests/DevNote.E2eTests.csproj` exists with four test files (one per risk pair). `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj` passes all tests locally with Chromium installed. CI has a separate optional `e2e` job (non-blocking on failure). The test plan §3 Phase 5 status is updated to `planned`.

### Key Discoveries

- `ClassificationResult` has 13 fields total: `Classification` (enum A/B/C), `Justification`, and 11 summary fields — the `FakeClassificationService` stub must populate all of them non-empty
- `HelperQuestionsResult` has `Questions: IReadOnlyList<string>`, `ContextHash`, `GeneratedAt` — the `FakeHelperQuestionsService` returns an empty list immediately so sections render without loading state blocking tests
- The accordion section header button text equals the section `Title` parameter (Polish); e.g., `GetByRole(AriaRole.Button, new() { Name = "Problem" })` — matches the `<button class="wizard-section-header">` text
- The textarea placeholder equals the section `Description` parameter (Polish); e.g., `GetByPlaceholder("Opisz problem biznesowy, który chcesz rozwiązać")` targets the Problem textarea when that section is expanded
- `WebApplicationFactory.EnsureServer()` casts `IServer` to `TestServer` internally — the `PlaywrightWebApplicationFactory` must return a TestServer-backed host from `CreateHost` (for WAF's internal use) while ALSO starting a separate Kestrel host on an ephemeral port for Playwright

## What We're NOT Doing

- `page.RouteAsync` for LLM call interception — server-side .NET calls, not browser-side; DI stub is the right layer
- Playwright vision/screenshot tests — all four risks are functional-behavioral, not visual
- Cross-circuit (page-reload, tab-close) state preservation — accepted negative space, test plan §7
- Testing .NET Identity internals (password hashing, token plumbing) — trust the framework
- Testcontainers Postgres — InMemory DB is sufficient and matches Phases 1–3 convention
- Real Azure OpenAI in CI — mocked via DI stub

## Implementation Approach

A new `DevNote.E2eTests` xUnit project contains all Playwright tests. The `PlaywrightWebApplicationFactory` extends `WebApplicationFactory<Program>`, swaps services in `ConfigureWebHost`, and starts a real Kestrel host on an ephemeral loopback port in `CreateHost` (alongside the TestServer host that WAF needs internally). `UserHelper` seeds users via `UserManager` and performs real browser login. `E2eTestBase` manages per-test Playwright browser context isolation.

## Critical Implementation Details

**Two-host WebApplicationFactory for Blazor Server**: `PlaywrightWebApplicationFactory.CreateHost` must:
1. Call `base.CreateHost(builder)` to get the TestServer-backed host that `WebApplicationFactory.EnsureServer()` requires internally
2. Build a **second, independent** Kestrel host (a new `IHostBuilder` calling `Host.CreateDefaultBuilder()`, re-applying the same service overrides from `ConfigureWebHost`, adding `webHostBuilder.UseKestrel(o => o.Listen(IPAddress.Loopback, 0))`)
3. Start the Kestrel host and extract `IServerAddressesFeature.Addresses.First()` as `ServerAddress`
4. Return the TestServer host so WAF internals don't throw; store the Kestrel host in a field for cleanup

**Service lifetimes for stubs**: Register `FakeClassificationService` and `FakeHelperQuestionsService` with `AddScoped` (matching Program.cs registrations) so they get a new instance per Blazor circuit.

**Browser context isolation**: Each test method gets a fresh `IBrowserContext` created from a shared `IBrowser`. This ensures authentication cookies from one test cannot leak to another.

---

## Phase 1: E2E project scaffold + test infrastructure

### Overview

Creates the `DevNote.E2eTests` project and all shared test infrastructure: factory, stubs, user helper, and base class. No test scenarios yet — just the plumbing that all four risk phases depend on.

### Changes Required

#### 1. Project file

**File**: `DevNote.E2eTests/DevNote.E2eTests.csproj`

**Intent**: New xUnit test project for Playwright E2E tests, referencing the main app project and all required test/browser packages.

**Contract**: `net9.0`, `IsPackable=false`, `IsTestProject=true`. Package references: `Microsoft.Playwright` (latest stable for .NET 9), `Microsoft.NET.Test.Sdk` (17.12.0), `xunit` (2.9.2), `xunit.runner.visualstudio` (2.8.2), `Microsoft.AspNetCore.Mvc.Testing` (9.0.0), `Microsoft.EntityFrameworkCore.InMemory` (9.0.0), `NSubstitute` (5.3.0), `coverlet.collector` (6.0.2). `ProjectReference` to `../dev-note.csproj`.

#### 2. PlaywrightWebApplicationFactory

**File**: `DevNote.E2eTests/Infrastructure/PlaywrightWebApplicationFactory.cs`

**Intent**: Starts the full ASP.NET Core + Blazor Server app on a real local HTTP port and exposes the address and a service scope factory for data seeding.

**Contract**:
- Inherits `WebApplicationFactory<Program>`, implements `IAsyncLifetime`
- `ConfigureWebHost` override: remove the Npgsql `DbContextOptions<ApplicationDbContext>` descriptor; register `UseInMemoryDatabase($"E2eTestDb_{Guid.NewGuid()}")`. Remove `IClassificationService` and `IHelperQuestionsService` descriptors; register `FakeClassificationService` (as `IClassificationService`) and `FakeHelperQuestionsService` (as `IHelperQuestionsService`), both `AddScoped`. Set environment to "Development" (`builder.UseEnvironment("Development")`).
- `CreateHost(IHostBuilder builder)` override: call `base.CreateHost(builder)` for the TestServer host; build a parallel Kestrel host on `IPAddress.Loopback` port 0 with the same service overrides; start it; extract `IServerAddressesFeature.Addresses.First()` and assign to `ServerAddress`. Return the TestServer host.
- `ServerAddress: Uri` — public property; the Kestrel base address for Playwright navigation
- `ScopeFactory: IServiceScopeFactory` — sourced from the Kestrel host's services; used by `UserHelper`
- `InitializeAsync`: call `EnsureServer()` then verify `ServerAddress` is set
- `DisposeAsync`: stop and dispose the Kestrel host; call `base.DisposeAsync()`

#### 3. FakeClassificationService

**File**: `DevNote.E2eTests/Infrastructure/FakeClassificationService.cs`

**Intent**: Returns a deterministic `ClassificationResult` without any HTTP call so wizard classification completes instantly and deterministically in tests.

**Contract**: Implements `IClassificationService`. `ClassifyAsync` returns a `ClassificationResult` with `Classification = Classification.B`, `Justification = "[test-justification]"`, and all 11 summary fields set to `"[test]"`. Returns immediately. No mutable state.

#### 4. FakeHelperQuestionsService

**File**: `DevNote.E2eTests/Infrastructure/FakeHelperQuestionsService.cs`

**Intent**: Returns empty helper questions instantly so wizard section expansion never enters a loading state during tests.

**Contract**: Implements `IHelperQuestionsService`. Returns `new HelperQuestionsResult { Questions = Array.Empty<string>(), ContextHash = "test" }`. No delay.

#### 5. UserHelper

**File**: `DevNote.E2eTests/Infrastructure/UserHelper.cs`

**Intent**: Provides test-setup operations: seeding users into the InMemory DB and performing browser-level login via the real `/login` form.

**Contract** — three static async methods:
- `CreateUserAsync(IServiceScopeFactory, string email, string password) → Task<string>`: resolves `UserManager<ApplicationUser>` from a new scope, creates `new ApplicationUser { UserName = email, Email = email }`, calls `UserManager.CreateAsync(user, password)`, throws on failure, returns `user.Id`
- `SeedCompletedNoteAsync(IServiceScopeFactory, string userId, Guid noteId) → Task`: resolves `ApplicationDbContext` from a new scope, inserts a `ConversationNote` with `Id = noteId`, `UserId = userId`, `Status = NoteStatus.Completed`, `Classification = Classification.B`, `Title = "Test note"`, all wizard fields and all 11 summary fields set to `"[seed]"`, `CreatedAt = DateTimeOffset.UtcNow`, `UpdatedAt = DateTimeOffset.UtcNow`. Calls `SaveChangesAsync`.
- `LoginAsync(IPage page, Uri baseUri, string email, string password) → Task`: navigates to `$"{baseUri}login"`, fills `page.GetByLabel("Email")` with `email`, fills `page.GetByLabel("Hasło")` with `password`, clicks `page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" })`, awaits `page.WaitForURLAsync("**/")`.

#### 6. E2eTestBase

**File**: `DevNote.E2eTests/Infrastructure/E2eTestBase.cs`

**Intent**: Per-test Playwright lifecycle: fresh browser context and page per test, shared server and browser across a test class.

**Contract**:
- Abstract class implementing `IAsyncLifetime`
- Constructor receives `PlaywrightWebApplicationFactory factory` (stored in a field)
- `InitializeAsync`: calls `Playwright.CreateAsync()`, opens headless Chromium, creates a fresh `IBrowserContext` (`browser.NewContextAsync()`), creates `IPage` (`context.NewPageAsync()`). Assigns to `protected IPage Page`.
- `DisposeAsync`: closes page, context, browser; calls `playwright.Dispose()`
- `protected Uri BaseUri => _factory.ServerAddress`
- `protected IServiceScopeFactory ScopeFactory => _factory.ScopeFactory`

#### 7. Playwright browser installation note

After building `DevNote.E2eTests`, run once to install the Chromium binary:
```
pwsh DevNote.E2eTests/bin/Debug/net9.0/playwright.ps1 install chromium
```

### Success Criteria

#### Automated Verification

- `dotnet build DevNote.E2eTests/DevNote.E2eTests.csproj` — zero errors
- `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj` — 0 tests, no compilation failures
- `pwsh DevNote.E2eTests/bin/Debug/net9.0/playwright.ps1 install chromium` — exits 0

#### Manual Verification

- `DevNote.E2eTests.dll` and `playwright.ps1` both present in `bin/Debug/net9.0/`
- A quick sanity test (empty `[Fact]` constructing and disposing the factory) passes without `InvalidCastException` or `NullReferenceException` — proving the Kestrel host starts and `ServerAddress` is set

---

## Phase 2: Risk #4 + Risk #3 — Auth/ownership browser tests

### Overview

Proves that the real ASP.NET Core auth middleware and Blazor ownership check fire in the hosted browser context: unauthenticated visitors are redirected to `/login` (Risk #4), and an authenticated user cannot open another user's note (Risk #3).

### Changes Required

#### 1. UnauthenticatedRedirectTests

**File**: `DevNote.E2eTests/Tests/UnauthenticatedRedirectTests.cs`

**Intent**: Verify that three data-bearing page URLs redirect an unauthenticated browser to `/login`.

**Contract**: `IClassFixture<PlaywrightWebApplicationFactory>`, extends `E2eTestBase`. Three `[Fact]` tests:

- `WizardPage_Unauthenticated_RedirectsToLoginPage`: navigate to `{BaseUri}`, call `page.WaitForURLAsync("**/login")`, assert `page.GetByRole(AriaRole.Heading, new() { Name = "Logowanie" }).IsVisibleAsync()` is true
- `NotesPage_Unauthenticated_RedirectsToLoginPage`: navigate to `{BaseUri}notes`, same assertions
- `EditNotePage_Unauthenticated_RedirectsToLoginPage`: navigate to `{BaseUri}edit/{Guid.NewGuid()}`, same assertions — auth middleware fires before ownership, so any GUID works

No user creation needed. Each test uses the fresh `IBrowserContext` from `InitializeAsync` which carries no cookies.

#### 2. CrossUserNoteAccessTests

**File**: `DevNote.E2eTests/Tests/CrossUserNoteAccessTests.cs`

**Intent**: Verify that user B navigating to user A's note is redirected to the notes list, not shown user A's content.

**Contract**: `IClassFixture<PlaywrightWebApplicationFactory>`, extends `E2eTestBase`. One `[Fact]`:

`EditNotePage_OtherUsersNote_RedirectsToNotesList`:
- `var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`
- Create user A: `var userAId = await UserHelper.CreateUserAsync(ScopeFactory, $"user-a-{ts}@test.com", "Test1234!")`
- Create user B: `await UserHelper.CreateUserAsync(ScopeFactory, $"user-b-{ts}@test.com", "Test1234!")`
- Seed user A's note: `var noteId = Guid.NewGuid(); await UserHelper.SeedCompletedNoteAsync(ScopeFactory, userAId, noteId)`
- Login as user B: `await UserHelper.LoginAsync(Page, BaseUri, $"user-b-{ts}@test.com", "Test1234!")`
- Navigate to user A's note: `await Page.GotoAsync($"{BaseUri}edit/{noteId}")`
- Assert redirect: `await Page.WaitForURLAsync("**/notes")`
- Assert edit heading is NOT visible: `await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Edytuj notatkę" })).Not.ToBeVisibleAsync()`
- Assert notes list heading IS visible: `await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Moje notatki" })).ToBeVisibleAsync()`

Use timestamp suffix in email addresses so parallel runs don't collide in the InMemory DB.

### Success Criteria

#### Automated Verification

- `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj --filter "FullyQualifiedName~UnauthenticatedRedirectTests"` — 3 tests pass
- `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj --filter "FullyQualifiedName~CrossUserNoteAccessTests"` — 1 test passes

#### Manual Verification

- Run with `--logger "console;verbosity=detailed"` — browser navigation steps visible in output
- Positive check: login as user A and confirm the note IS accessible (manual, then nothing to revert)

---

## Phase 3: Risk #2 — Wizard back-navigation preserves data (re-planned hybrid)

### Overview

Moves the strict value-persistence proof to deterministic bUnit/component tests and keeps browser coverage as a lightweight E2E smoke. This preserves Risk #2 confidence while avoiding flaky Blazor circuit timing as a hard gate.

### Changes Required

#### 1. Re-anchor component-level persistence proof

**Files**:
- `DevNote.Tests/Components/WizardSectionTests.cs`
- `DevNote.Tests/Components/WizardTests.cs`

**Intent**: Make Risk #2's pass/fail source of truth deterministic in the component layer and aligned with the current accordion markup.

**Contract**:
- Update stale accordion selectors to match current `WizardSection` structure (`summary.wizard-section-header` instead of `button.wizard-section-header`).
- Keep (or reintroduce) an explicit test that: fill field → collapse section → re-expand section → previously entered value remains.
- Keep helper-question and classify-enable coverage green after selector migration (no behavioral broadening in this phase).

#### 2. Keep E2E as smoke signal only

**File**: `DevNote.E2eTests/Tests/WizardBackNavigationTests.cs`

**Intent**: Preserve browser-level signal for Risk #2 without making brittle value-retention assertions depend on circuit timing.

**Contract**: One `[Fact]` that validates smoke behavior only:
- authenticated user can open wizard,
- expand/collapse section headers,
- enter values into the first two sections,
- perform collapse/re-expand flow without redirect, crash, or JS/runtime error.

Do not assert strict value restoration in this E2E test; that assertion belongs to component tests in this re-plan.

### Success Criteria

#### Automated Verification

- `dotnet test DevNote.Tests/DevNote.Tests.csproj --filter "FullyQualifiedName~WizardSectionTests|FullyQualifiedName~WizardTests"` — component wizard tests pass with updated selectors and persistence assertion
- `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj --filter "FullyQualifiedName~WizardBackNavigationTests"` — smoke test passes

#### Manual Verification

- Run E2E smoke with `Headless = false` — accordion visibly opens/closes and no runtime error appears
- Regression check on component proof: temporarily call `State.Reset()` from section-toggle path in `WizardSection.razor`; persistence-focused bUnit test goes red; revert

---

## Phase 4: Risk #5 — Edit-revert-reclassify end-to-end

### Overview

Proves the full browser round-trip for editing a Completed note: the edit page load reverts to Draft (visible in the notes list), and after re-classification the note is Completed again with a fresh classification — no stale carry-over.

### Changes Required

#### 1. EditRevertReclassifyTests

**File**: `DevNote.E2eTests/Tests/EditRevertReclassifyTests.cs`

**Intent**: Open a seeded Completed note in edit mode, observe the intermediate Draft state in the notes list, re-classify, and observe the final Completed state.

**Contract**: `IClassFixture<PlaywrightWebApplicationFactory>`, extends `E2eTestBase`. One `[Fact]`:

`EditNote_CompletedNote_ShowsDraftAfterLoad_ThenCompletedAfterReclassify`:

**Setup**:
- `var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`
- Create user: `var userId = await UserHelper.CreateUserAsync(ScopeFactory, $"editor-{ts}@test.com", "Test1234!")`
- Seed Completed note: `var noteId = Guid.NewGuid(); await UserHelper.SeedCompletedNoteAsync(ScopeFactory, userId, noteId)`
- Login: `await UserHelper.LoginAsync(Page, BaseUri, $"editor-{ts}@test.com", "Test1234!")`

**Assert initial state in /notes**:
- Navigate to `{BaseUri}notes`; wait for heading
- Assert "Ukończona" visible: `await Expect(Page.GetByText("Ukończona")).ToBeVisibleAsync()`
- Assert "B" badge visible: `await Expect(Page.GetByText("B")).ToBeVisibleAsync()`

**Navigate to edit (triggers RevertToDraftAsync)**:
- Navigate to `{BaseUri}edit/{noteId}`; wait for edit heading

**Assert intermediate Draft in /notes**:
- Navigate to `{BaseUri}notes`; wait for heading
- Assert "Szkic" visible: `await Expect(Page.GetByText("Szkic")).ToBeVisibleAsync()`
- Assert "Ukończona" NOT visible: `await Expect(Page.GetByText("Ukończona")).Not.ToBeVisibleAsync()`
- Assert "B" badge NOT visible: `await Expect(Page.GetByText("B")).Not.ToBeVisibleAsync()`

**Re-classify**:
- Navigate back to `{BaseUri}edit/{noteId}`; wait for edit heading
- Expand Problem section and update its value (proves it's editable in this state)
- Click Klasyfikuj: `await Page.GetByRole(AriaRole.Button, new() { Name = "Klasyfikuj" }).ClickAsync()`
- Wait for success message: `await Page.GetByText("Notatka zaktualizowana.").WaitForAsync()`

**Assert final Completed state in /notes**:
- Navigate to `{BaseUri}notes`; wait for heading
- Assert "Ukończona" visible again: `await Expect(Page.GetByText("Ukończona")).ToBeVisibleAsync()`
- Assert "B" badge visible: `await Expect(Page.GetByText("B")).ToBeVisibleAsync()` (`FakeClassificationService` always returns B; badge confirms no stale state and that UI updated)

### Success Criteria

#### Automated Verification

- `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj --filter "FullyQualifiedName~EditRevertReclassifyTests"` — 1 test passes

#### Manual Verification

- Run with `Headless = false` — notes list status badge changes Draft → Completed through the browser
- Regression check: test is red when `RevertToDraftAsync` is commented out in `EditNote.razor` (then revert)

---

## Phase 5: CI wiring

### Overview

Adds an optional E2E job to `ci.yml` that installs Chromium and runs the E2E project. Non-blocking on failure (`continue-on-error: true`) per test plan §5.

### Changes Required

#### 1. ci.yml — add e2e job

**File**: `.github/workflows/ci.yml`

**Intent**: Run the E2E test suite on every PR as a non-blocking optional signal after the main CI job passes.

**Contract**: Add a second job `e2e` to the existing workflow. The existing job is named `ci`. New job definition:

```yaml
e2e:
  name: E2E Tests (optional)
  runs-on: ubuntu-latest
  needs: [ci]
  continue-on-error: true
  steps:
    - uses: actions/checkout@v4
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: "9.0.x"
    - name: Restore
      run: dotnet restore
    - name: Build E2E project
      run: dotnet build DevNote.E2eTests/DevNote.E2eTests.csproj --configuration Release --no-restore
    - name: Install Playwright browsers
      run: pwsh DevNote.E2eTests/bin/Release/net9.0/playwright.ps1 install --with-deps chromium
    - name: E2E Tests
      run: dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj --configuration Release --no-build
```

### Success Criteria

#### Automated Verification

- `ci.yml` parses without YAML syntax errors
- On a test PR, the `e2e` job appears in GitHub Actions UI

#### Manual Verification

- `ci.yml` inspection confirms: `continue-on-error: true`, `needs: [ci]`, `playwright.ps1 install --with-deps chromium` before the test run
- Existing `ci` job (format/build/test) is unchanged

---

## Testing Strategy

### Automated (all phases)

- `dotnet build DevNote.E2eTests/DevNote.E2eTests.csproj` — zero errors
- `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj` — E2E suite passes (3 unauthenticated + 1 cross-user + 1 back-navigation smoke + 1 edit-revert-reclassify)
- `dotnet test DevNote.Tests/DevNote.Tests.csproj --filter "FullyQualifiedName~WizardSectionTests|FullyQualifiedName~WizardTests"` — deterministic Risk #2 persistence proof passes in component layer
- Per-risk filter commands in each phase's success criteria

### Manual (smoke)

1. Install Chromium once: `pwsh DevNote.E2eTests/bin/Debug/net9.0/playwright.ps1 install chromium`
2. Run full suite: `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj` — all green
3. Run Phase 3 E2E smoke and Phase 4 E2E test with `Headless = false` to watch browser navigation

## References

- Test plan Phase 5: `context/foundation/test-plan.md` §3, §4, §7
- Existing WebApplicationFactory pattern: `DevNote.Tests/Infrastructure/DevNoteWebApplicationFactory.cs`
- Blazor Server accordion: `Components/Shared/WizardSection.razor`
- Edit lifecycle: `Components/Pages/EditNote.razor`
- Classification model: `Models/ClassificationResult.cs`, `Models/HelperQuestionModels.cs`

---

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: E2E project scaffold + test infrastructure

#### Automated

- [x] 1.1 `dotnet build DevNote.E2eTests/DevNote.E2eTests.csproj` — zero errors — ce0dacf
- [x] 1.2 `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj` — 0 tests, no compilation failures — ce0dacf
- [x] 1.3 `pwsh DevNote.E2eTests/bin/Debug/net9.0/playwright.ps1 install chromium` — exits 0 — ce0dacf

#### Manual

- [x] 1.4 `playwright.ps1` present in output directory — ce0dacf
- [x] 1.5 Sanity test: empty `[Fact]` constructing and disposing the factory passes without `InvalidCastException` — ce0dacf

### Phase 2: Risk #4 + Risk #3 — Auth/ownership browser tests

#### Automated

- [x] 2.1 `dotnet test ... --filter "FullyQualifiedName~UnauthenticatedRedirectTests"` — 3 tests pass — c8b84aa
- [x] 2.2 `dotnet test ... --filter "FullyQualifiedName~CrossUserNoteAccessTests"` — 1 test passes — c8b84aa

#### Manual

- [ ] 2.3 Test output with `--logger "console;verbosity=detailed"` shows browser navigation steps
- [ ] 2.4 Positive case verified: user A can access their own note

### Phase 3: Risk #2 — Wizard back-navigation preserves data

#### Automated

- [x] 3.1 `dotnet test ...DevNote.Tests... --filter "FullyQualifiedName~WizardSectionTests|FullyQualifiedName~WizardTests"` — component persistence proof passes
- [x] 3.2 `dotnet test ... --filter "FullyQualifiedName~WizardBackNavigationTests"` — E2E smoke passes

#### Manual

- [ ] 3.3 `Headless = false` run shows accordion animate with no runtime errors through expand/collapse flow
- [ ] 3.4 Regression check: component persistence test is red when `State.Reset()` is introduced on section toggle (then revert)

### Phase 4: Risk #5 — Edit-revert-reclassify end-to-end

#### Automated

- [ ] 4.1 `dotnet test ... --filter "FullyQualifiedName~EditRevertReclassifyTests"` — 1 test passes

#### Manual

- [ ] 4.2 `Headless = false` run shows Draft badge then Completed badge in notes list
- [ ] 4.3 Regression check: test is red when `RevertToDraftAsync` is commented out (then revert)

### Phase 5: CI wiring

#### Automated

- [ ] 5.1 `ci.yml` parses without YAML syntax errors
- [ ] 5.2 `e2e` job appears in GitHub Actions UI on a test PR

#### Manual

- [ ] 5.3 `ci.yml` inspection confirms `continue-on-error: true` and `needs: [ci]`
- [ ] 5.4 Existing `ci` job (format/build/test) is unchanged
