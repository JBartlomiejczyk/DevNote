# E2E Critical Flows — Plan Brief

> Full plan: `context/changes/testing-e2e-critical-flows/plan.md`

## What & Why

Phase 5 of the test rollout adds a Playwright for .NET E2E project (`DevNote.E2eTests`) that verifies risks #2, #3, #4, and #5 in a real Chromium browser. The prior phases (unit, bUnit, integration) proved correctness at each layer in isolation; this phase proves the full Blazor Server user journey works in an actual browser circuit — auth redirects, ownership enforcement, accordion state preservation, and the edit→revert→reclassify lifecycle.

## Starting Point

`DevNote.Tests` already has xUnit + bUnit + WebApplicationFactory tests but no Playwright reference and no CI E2E job. All Blazor pages use `@rendermode InteractiveServer` (Blazor Server over SignalR), which requires a real Kestrel TCP socket — not the in-memory TestServer transport — for Playwright to connect.

## Desired End State

A new `DevNote.E2eTests` project contains 6 E2E tests (3 unauthenticated-redirect, 1 cross-user ownership, 1 back-navigation, 1 edit-revert-reclassify). All pass locally with Chromium installed. CI gains an optional non-blocking `e2e` job. The test plan Phase 5 status advances to `planned`.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Project location | New `DevNote.E2eTests` project | Keeps E2E tooling (Playwright, browser binaries) isolated from the unit/bUnit/integration suite | Plan |
| Server binding | WebApplicationFactory + real Kestrel on ephemeral port | Blazor Server requires a real WebSocket-capable socket; in-memory TestServer cannot serve SignalR | Plan |
| LLM interception | Swap `IClassificationService` + `IHelperQuestionsService` in DI | These are server-side .NET calls, not browser-originated HTTP; `page.RouteAsync` only intercepts browser traffic | Plan |
| Database | EF Core InMemory (per fixture) | Consistent with Phases 1–3 pattern; fast, no Docker, full isolation | Plan |
| User seeding | `UserManager` in fixture + browser login via `/login` form | Fast, deterministic setup; does not couple test infrastructure to UI details unrelated to the risk | Plan |
| CI gate | Optional `e2e` job (`continue-on-error: true`) | Test plan §5 marks E2E as optional; non-blocking keeps the merge gate unaffected by E2E flakiness | Plan |

## Scope

**In scope:**
- New `DevNote.E2eTests` project with Playwright for .NET
- `PlaywrightWebApplicationFactory` (dual-host: TestServer + Kestrel)
- `FakeClassificationService`, `FakeHelperQuestionsService`, `UserHelper`, `E2eTestBase`
- 6 E2E tests covering risks #2, #3, #4, #5
- CI `e2e` job (non-blocking)
- Test plan Phase 5 status updated to `planned`

**Out of scope:**
- `page.RouteAsync` LLM interception (wrong layer)
- Playwright vision/screenshot tests
- Cross-circuit (page-reload) state preservation — accepted negative space
- Testcontainers Postgres
- Real Azure OpenAI in CI

## Architecture / Approach

`PlaywrightWebApplicationFactory` extends `WebApplicationFactory<Program>` and overrides `CreateHost` to return the TestServer host (needed by WAF internals) while also spinning up a second Kestrel host on `IPAddress.Loopback:0`. All DI overrides (InMemory DB, fake LLM services) are applied to both hosts. Playwright connects to the Kestrel host's ephemeral address. Each test class is `IClassFixture<PlaywrightWebApplicationFactory>`; each test method gets a fresh `IBrowserContext` (no cookie leakage between tests).

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Scaffold + infrastructure | Project, factory, stubs, helpers, base class — all plumbing, no scenarios | Two-host WAF pattern may throw `InvalidCastException` if Kestrel host isn't built correctly |
| 2. Risk #4 + #3 | 4 tests: 3 unauth-redirect + 1 cross-user ownership | Auth middleware must fire before ownership check on `/edit/{id}` |
| 3. Risk #2 | 1 test: accordion collapse→re-expand preserves values | `WizardStateService` must not be scoped to the component lifecycle (it's `AddScoped` on the circuit — correct) |
| 4. Risk #5 | 1 test: edit→Draft→reclassify→Completed lifecycle visible in `/notes` | `RevertToDraftAsync` runs in `OnInitializedAsync`; the intermediate Draft must be observable in the notes list before re-classification |
| 5. CI wiring | Optional `e2e` job in `ci.yml`, non-blocking | `playwright.ps1 install --with-deps chromium` must run before `dotnet test` |

**Prerequisites:** Phases 1–4 complete (change `testing-quality-gates-wiring` already merged); Chromium browser installed locally (`playwright.ps1 install chromium`).
**Estimated effort:** ~2 sessions across 5 phases; Phase 1 is the hardest (infrastructure); Phases 2–4 are mechanical once plumbing works.

## Open Risks & Assumptions

- The two-host WAF approach (`CreateHost` override) is the established pattern for Blazor Server E2E tests but requires careful handling of the TestServer/Kestrel boundary — the sanity test in Phase 1 catches this early
- `IServerAddressesFeature` address extraction must happen after the Kestrel host has started; timing issue would surface as a null `ServerAddress`
- InMemory DB is shared within a factory instance — tests using timestamp-suffixed emails prevent user collisions across parallel test runs

## Success Criteria (Summary)

- `dotnet test DevNote.E2eTests/DevNote.E2eTests.csproj` — 6 tests pass, no compilation errors
- All four risks (#2, #3, #4, #5) have a browser-layer test that fails when its targeted behavior is deliberately broken and passes when it is correct
- CI `e2e` job appears in GitHub Actions UI on PRs without blocking the `ci` job
