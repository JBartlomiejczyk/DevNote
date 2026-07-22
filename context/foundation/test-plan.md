# Test Plan

> Phased test rollout for this project. Strategy is frozen at the top
> (§1–§5); cookbook patterns at the bottom (§6) fill in as phases ship.
> Read before writing any new test.
>
> Refresh: re-run `/10x-test-plan --refresh` when stale (see §8).
>
> Last updated: 2026-07-22 (Phase 5 added — E2E critical flows)

## 1. Strategy

Tests follow three non-negotiable principles for this project:

1. **Cost × signal.** The cheapest test that gives a real signal for the
   risk wins. Do not promote to e2e because e2e "feels safer." Do not put a
   vision model on top of a deterministic check that already catches the
   regression. For DevNote, most top risks are catchable at the unit and
   integration layers — the LLM boundary, the auth boundary, and the wizard
   state machine — before any browser-driven test is justified.
2. **User concerns are first-class evidence.** Risks anchored in "the team
   is worried about X, and the failure would surface somewhere in <area>"
   carry the same weight as PRD lines or hot-spot data. The Phase 2
   interview surfaced cross-user note access, wizard back-navigation data
   loss, and LLM response-shape drift — all three are top-N risks below.
3. **Risks are scenarios, not code locations.** This plan documents *what
   could fail* and *why we believe it's likely* — drawn from documents,
   interview, and codebase *signal* (churn, structure, test base). It does
   NOT claim to know which line owns the failure. That knowledge is
   produced by `/10x-research` during each rollout phase. If the plan and
   research disagree about where the failure lives, research is the ground
   truth.

Hot-spot scope used for likelihood weighting: `Components/ (Pages, Shared, Layout)`, `Services/`, `Data/`, `Models/`, `Program.cs` — excluding `bin/obj`, `context/`, `.github/`, docs.

## 2. Risk Map

The top failure scenarios this project must protect against, ordered by
risk = impact × likelihood. Risks are failure scenarios in user / business
terms, not test names. The Source column cites the *evidence that surfaced
this risk* — never a specific file as "where the failure lives" (that is
research's job, see §1 principle #3).

| # | Risk (failure scenario) | Impact | Likelihood | Source (evidence — not anchor) |
|---|-------------------------|--------|------------|--------------------------------|
| 1 | A changed or malformed LLM response yields a wrong A/B/C classification or a summary with empty/mis-mapped fields, and the developer trusts it | High | High | PRD §Success Criteria, §Business Logic; interview Q2 ("LLM shape changed"); roadmap north star S-01; hot-spot `Services/` (2 helper/classification services, 3 commits/30d) |
| 2 | Developer fills the 8-section wizard, navigates back (or the Blazor Server circuit reconnects/reloads), and entered answers are lost or overwritten | High | High | PRD US-02, FR-005; interview Q1, Q3; hot-spot `Components/Pages` — 20 commits/30d |
| 3 | A logged-in user opens or edits **another user's** note by supplying its id (authorization/ownership — IDOR) | High | Medium | PRD §Access Control ("each user sees only their own notes"), US-06; interview Q1; hot-spot `Components/Pages` — 20 commits/30d |
| 4 | An **unauthenticated** request reaches an endpoint that returns user emails or notes, violating "unauthenticated access shows only login/registration" (abuse / PII leakage) | High | High | PRD §Access Control (abuse lens); tech-stack.md `has_auth: true`; hot-spot `Program.cs` — 11 commits/30d |
| 5 | Editing a Completed note fails to revert to Draft / re-classify, leaving a stale classification and summary that no longer match the answers | Medium | Medium | PRD US-06, FR-009; hot-spot `Components/Pages` (EditNote) — 20 commits/30d |
| 6 | A helper-questions LLM call fails or is slow and blocks wizard progress (no degraded mode), or repeated identical calls run unbounded and spike cost | Medium | Medium | PRD Open Questions 2 (cost) & 3 (degraded mode); roadmap Open Q3/Q4; hot-spot `Services/` (HelperQuestions) — 3 commits/30d |

5–7 rows; every row cites at least one source. High × High protected first
(#1, #2, #4), then High × Medium (#3), then Medium × Medium (#5, #6).

**Excluded from the map (by calibration).** Auto-apply of EF Core migrations
on startup is High-impact but Low-likelihood (stable, rarely touched). It
belongs to a deploy smoke check / observability, not a unit or integration
test — noted here rather than padding the map.

### Risk Response Guidance

| Risk | What would prove protection | Must challenge | Context `/10x-research` must ground | Likely cheapest layer | Anti-pattern to avoid |
|------|-----------------------------|----------------|--------------------------------------|-----------------------|-----------------------|
| #1 | Given a valid completed wizard, the parsed result carries a defined classification and all summary fields the PRD requires; a malformed/partial model response is rejected or surfaced, never silently rendered as a blank-but-valid summary | "Strict JSON schema guarantees valid output" — the endpoint can error, the schema can drift, and an unknown classification value can silently default to a valid-looking one | The classification/summary parse boundary, the required-field contract (11 summary fields), and the fallback behavior when a field or the whole response is missing | unit | Oracle problem — copying the expected value from the parser's own defaulting logic instead of from the PRD field contract |
| #2 | After entering data and collapsing/re-expanding wizard sections (accordion navigation) within the same Blazor Server circuit, every previously entered section value is still present and unchanged; a re-render/state-restoration boundary does not drop values. Leaving the page or landing on a new circuit is accepted negative space (see §7), not a covered guarantee | "Blazor Server persists state automatically" — wizard state is held in a scoped/in-memory service tied to the circuit; a reconnect or reload can reset it | Where wizard answers live between sections, the lifetime/scope of that state, and what happens on circuit reconnect and on load-from-note | component (bUnit) + service unit | Happy-path forward-only navigation test that never exercises back navigation or a re-render |
| #3 | A user requesting a note id they do not own receives not-found/forbidden, not the other user's content | "Being authenticated is enough" — ownership must be enforced per request, not just authentication; every route/page that accepts a note id must filter by the current user | The note-fetch path for each id-bearing route, how the current user id is resolved, and whether ownership is enforced server-side | integration (WebApplicationFactory) | Testing only that the owner can read their own note; never asserting a non-owner is denied |
| #4 | Anonymous requests to any data-returning route are redirected/denied; no diagnostic or admin route returns user emails or notes without authorization | "The global FallbackPolicy protects everything" — endpoints can opt out with `AllowAnonymous`, and diagnostic routes can leak data | The set of anonymous-allowed endpoints, what each returns, and whether any exposes user or note data | integration (WebApplicationFactory) | Asserting only that the login page is anonymously reachable; never enumerating the routes that must stay protected |
| #5 | Editing a Completed note moves it to Draft and, on load, that reversion clears all generated output (classification, justification, and the 11 summary fields) before any re-classification; once re-submitted, its classification and summary reflect the edited answers (no stale carry-over) | "Saving an edit always re-runs classification" — revert-to-Draft is conditional on current status, and re-classification is a separate step that can be skipped | The status-transition rules on edit, when re-classification runs, and how stale summary fields are cleared or replaced | unit + integration | Asserting only the final Completed state; never checking the intermediate Draft revert |
| #6 | When the LLM call fails, the section still lets the developer proceed (degraded, empty questions, clear message) and classification stays available; identical section+context requests are served from the context-hash cache, not re-called. Scope is sequential cache hits only — concurrent in-flight deduplication is out of scope (see §7) | "An error just shows a message" — must verify the wizard is not hard-blocked, and that the context-hash cache actually prevents duplicate *sequential* calls | The failure path in the helper-questions coordinator, the cache key/lifetime, and what the UI does on error vs. success | unit (coordinator) + component | Mocking the LLM to always succeed, so the failure/degraded path and the cache-hit path are never exercised |

Every top risk has a response row, none cite file anchors, and each names
the behavior to prove, the assumption to challenge, and one anti-pattern.

## 3. Phased Rollout

Each row is a discrete rollout phase that will open its own change folder
via `/10x-new`. Status moves left-to-right through the values below; the
orchestrator updates Status as artifacts appear on disk.

| # | Phase name | Goal (one line) | Risks covered | Test types | Status | Change folder |
|---|------------|-----------------|---------------|------------|--------|---------------|
| 1 | Test harness + classification/summary integrity | Bootstrap the xUnit runner and prove the LLM classify/summary parse cannot silently produce wrong or empty results | #1 | unit | complete | testing-classification-summary-integrity |
| 2 | Auth & ownership boundary | Prove anonymous requests cannot reach user data and no user can read another user's note | #3, #4 | integration | complete | testing-auth-ownership-boundary |
| 3 | Wizard state, edit-revert & degraded mode | Prove wizard back-navigation preserves data, edit reverts Completed→Draft, and helper-questions failure degrades without blocking | #2, #5, #6 | component (bUnit) + integration/unit | complete | testing-wizard-state-edit-degraded-mode |
| 4 | Quality-gates wiring | Lock format + build + unit + integration in CI and enable the local post-edit hook | cross-cutting | gates | complete | testing-quality-gates-wiring |
| 5 | E2E critical flows | Prove the full Blazor Server user journey works in a real browser — login/register, wizard completion end-to-end, back-navigation preserving data, and edit→revert→reclassify | #2, #3, #4, #5 (browser layer) | e2e (Playwright for .NET) | change opened | testing-e2e-critical-flows |

**Status vocabulary** (fixed — parser literals): `not started` → `change opened` → `researched` → `planned` → `implementing` → `complete`.

Order rationale: Phase 1 is the cheapest highest-signal layer (pure unit
tests, no infrastructure) and it stands up the test project the later
phases depend on. Phase 2 needs a test host (WebApplicationFactory) and
defends the two security risks. Phase 3 needs bUnit for interactive
component/state coverage. Phase 4 locks the floor once real suites exist.

## 4. Stack

The classic test base for this project. AI-native tools (if any) carry a
`checked:` date so future readers can see which lines need re-verification.
Recommendations are grounded in local manifests/configs plus the MCP/tools
actually exposed in the current session.

| Layer | Tool | Version | Notes |
|-------|------|---------|-------|
| unit + integration | xUnit | latest for .NET 9 | none yet — new `DevNote.Tests` project bootstrapped by §3 Phase 1 |
| test doubles | NSubstitute or Moq | latest | fake the Azure OpenAI edge and `ILogger`/`IOptions`; never mock internal services |
| Blazor component | bUnit | latest | wizard / section back-navigation state; added in §3 Phase 3 |
| integration host | Microsoft.AspNetCore.Mvc.Testing (`WebApplicationFactory`) | .NET 9 | auth/ownership boundary; pair with EF Core InMemory or a Postgres Testcontainer; added in §3 Phase 2 |
| e2e | Playwright for .NET | n/a | none yet — planned in §3 Phase 5; intercept LLM calls via `RouteAsync` (no real Azure OpenAI in CI) |
| accessibility | none yet | n/a | deferred — no top risk points here |
| (optional) AI-native | post-edit hook (`dotnet build` + `dotnet test`) | n/a | recommended local, wired in §3 Phase 4; when NOT to use: not a CI substitute |

**Stack grounding tools (current session):**
- Docs: Context7 — available; can validate xUnit / bUnit / `WebApplicationFactory` / Playwright-for-.NET APIs and .NET 9 test setup before each phase; checked: 2026-07-08
- Search: Exa.ai — available; use for current tool status/comparison only, then prefer official docs; checked: 2026-07-08
- Runtime/browser: Playwright MCP / browser tool — not available in current session; e2e stays deferred until a browser layer is justified; checked: 2026-07-08
- Provider/platform: GitHub MCP — available; relevant to §3 Phase 4 CI-gate wiring and PR check inspection; checked: 2026-07-08

Use docs MCPs for current framework/library APIs and setup details. Use
search MCPs for discovery or current status only. Do not use MCP docs/search
to infer code failure anchors; those belong in per-phase `/10x-research`.

## 5. Quality Gates

The full set of gates that must pass before a change reaches production.
"Required after §3 Phase N" means the gate is enforced once that rollout
phase lands; before that, the gate is `planned`.

| Gate | Where | Required? | Catches |
|------|-------|-----------|---------|
| format + build (`dotnet format`, `dotnet build`) | local + CI | required | syntactic / style / compile drift |
| unit | local + CI | required after §3 Phase 1 | LLM parse + note-mapping + state regressions |
| integration (auth/ownership) | CI on PR | required after §3 Phase 2 | broken auth boundary, cross-user access, PII exposure |
| component (bUnit) | local + CI | required after §3 Phase 3 | wizard state / back-navigation regressions |
| post-edit hook (`dotnet build` + `dotnet test`) | local (agent loop) | recommended after §3 Phase 4 | regressions at edit time |
| e2e on critical flows | CI on PR | optional (§3 Phase 5) | full deployed-shape failures — routing, real circuit, auth redirect |
| pre-prod smoke (health + migration) | between merge + prod | optional | environment-specific / migration failures |

Every row corresponds to a gate that is wired or will be wired by a named
rollout phase. No aspirational gates listed.

## 6. Cookbook Patterns

How to add new tests in this project. Each sub-section is filled in once the
relevant rollout phase ships; before that, it reads "TBD — see §3 Phase N."

### 6.1 Adding a unit test

- TBD — see §3 Phase 1 (LLM classify/summary parse integrity; contract-derived oracles for the 11 summary fields).

### 6.2 Adding an integration test (auth / ownership boundary)

- TBD — see §3 Phase 2 (`WebApplicationFactory` host; anonymous-denied and non-owner-denied patterns).

### 6.3 Adding a Blazor component / wizard-state test

- **Location**: `DevNote.Tests/Components/`. Reference tests:
  `WizardSectionTests.cs` (isolated component: collapse→re-expand value
  restoration, `FirstExpanded` once-only, mutually-exclusive helper states),
  `WizardTests.cs` and `EditNoteTests.cs` (page-level behavior).
- **Framework**: bUnit 2.7.2. Derive the test class from `BunitContext`; render
  with `Render<TComponent>(...)`; use `.Bind(p => p.Value, ...)` for two-way
  parameters; drive UI through rendered controls (`Find("button...").Click()`,
  `Find("textarea").Change(...)`), never private fields. `@rendermode
  InteractiveServer` is ignored by the bUnit renderer.
- **Auth + JS**: `AddAuthorization().SetAuthorized("user-1")` plus
  `SetClaims(new Claim(ClaimTypes.NameIdentifier, "user-1"))`; set
  `JSInterop.Mode = JSRuntimeMode.Loose` so scroll/interop calls are no-ops.
- **Services**: register the real `WizardStateService`,
  `HelperQuestionsCoordinator`, and `NoteService`; substitute only
  `IClassificationService` and `IHelperQuestionsService`. Give each EF-backed
  test its own InMemory database via `ComponentTestDb.Create()`.
- **Risk #2 scope**: assert same-circuit accordion collapse/re-expand
  preservation only; page-leave/new-circuit loss is accepted negative space (§7).
- **Run**: `dotnet test DevNote.Tests\DevNote.Tests.csproj --filter "FullyQualifiedName~DevNote.Tests.Components"`.

### 6.4 Adding a test for a new AI/LLM service call

- **Boundary**: depend on an interface (`IClassificationService`,
  `IHelperQuestionsService`), never the concrete Azure-calling class. Substitute
  it with NSubstitute; return a structured result for success and use
  `.ThrowsAsync(...)` for the failure/degraded path.
- **Reference tests**: `DevNote.Tests/Services/HelperQuestionsCoordinatorTests.cs`
  (sequential cache-hit served once, changed-context cache miss, `forceRefresh`
  bypass, failure → empty questions + error + `IsLoading` false).
- **Assert**: result structure, UI/coordinator state, and invocation count
  (`Received(1)`/`Received(2)`). Never assert exact Polish wording or pinned hash
  literals (see §7). Risk #6 covers *sequential* cache hits, not concurrent
  in-flight deduplication.
- **Run**: `dotnet test DevNote.Tests\DevNote.Tests.csproj --filter "FullyQualifiedName~HelperQuestionsCoordinatorTests"`.

### 6.5 Adding a test for a note state transition

- **Location / reference tests**: service level in
  `DevNote.Tests/Services/NoteServiceTests.cs`
  (`RevertToDraftAsync_CompletedNote_ClearsGeneratedOutput`,
  `EditLifecycle_ReclassificationReplacesGeneratedOutputOnSameNote`); page level
  in `DevNote.Tests/Components/EditNoteTests.cs` (load reverts to clean Draft,
  success updates the original row, failure leaves a clean Draft).
- **Pattern**: seed a Completed note with *distinct* stale generated values, then
  assert the intermediate Draft revert clears classification + all 11 summary
  fields *before* re-classification — do not assert only the final Completed
  state. Use independent old/new values so mapping assertions are not
  tautological.
- **Data isolation**: one InMemory database per test
  (`TestDb_{Guid.NewGuid()}` / `ComponentTestDb.Create()`).
- **Run**: `dotnet test DevNote.Tests\DevNote.Tests.csproj --filter "FullyQualifiedName~NoteServiceTests"`.

### 6.6 Per-rollout-phase notes

(Optional. After each phase lands, `/10x-implement` appends a 2–3 line note
here capturing anything surprising the phase taught.)

## 7. What We Deliberately Don't Test

Exclusions agreed during the rollout (Phase 2 interview, Q5). Future
contributors should respect these unless the underlying assumption changes.

- **Exact Polish wording of LLM output** (classification justification text, generated helper questions) — the strict JSON schema already enforces structure and field presence; test structure, field contract, and count, not literal text. Re-evaluate if the output contract stops being schema-enforced. (Source: Phase 2 interview Q5.)
- **.NET Identity framework internals** (registration/login/token plumbing we did not author) — trust the framework; test *our* authorization wiring and per-user note filtering instead (see Risks #3, #4). Re-evaluate if we customize Identity's core flows. (Source: Phase 2 interview Q5, reasonable corollary.)
- **EF Core migration auto-apply on startup as a unit/integration target** — High-impact but Low-likelihood; covered by a deploy smoke / health check, not the test suite (see §2 exclusion note).
- **Cross-circuit / page-leave wizard-state preservation (Risk #2 boundary)** — state lives in a circuit-scoped `WizardStateService`; only same-circuit accordion navigation and re-render/state-restoration are guaranteed and tested. Leaving the page or landing on a fresh circuit intentionally starts clean. Re-evaluate if wizard state is moved to durable per-user storage. (Source: Phase 3 scope decision.)
- **Concurrent in-flight helper-question deduplication (Risk #6 boundary)** — the context-hash cache prevents duplicate *sequential* identical calls; two simultaneous in-flight requests for the same section are not deduplicated. Re-evaluate if helper generation becomes a measured cost hot-spot. (Source: Phase 3 scope decision.)

## 8. Freshness Ledger

- Strategy (§1–§5) last reviewed: 2026-07-08
- Stack versions last verified: 2026-07-08
- AI-native tool references last verified: 2026-07-08

Refresh (`/10x-test-plan --refresh`) when:

- a new top-3 risk surfaces from the roadmap or archive,
- a recommended tool's `checked:` date is older than three months,
- the project's tech stack changes (new framework, new test runner),
- §7 negative-space no longer matches what the team believes.
