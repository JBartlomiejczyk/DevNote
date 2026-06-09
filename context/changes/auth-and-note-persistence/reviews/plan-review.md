<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Auth & Note Persistence

- **Plan**: context/changes/auth-and-note-persistence/plan.md
- **Mode**: Deep
- **Date**: 2026-06-09
- **Verdict**: SOUND (after fixes)
- **Findings**: [1 critical] [2 warnings] [1 observation]

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS (after fix) |
| Blind Spots | PASS (after fixes) |
| Plan Completeness | PASS (after fix) |

## Grounding

Grounding: 12/12 paths ✓, 6/6 symbols ✓, brief↔plan ✓

## Findings

### F1 — Auth pages cannot use SignInManager in InteractiveServer mode

- **Severity**: ❌ CRITICAL
- **Impact**: 🔬 HIGH — architectural stakes; think carefully before deciding
- **Dimension**: Architectural Fitness
- **Location**: Phase 3 — Auth UI Pages
- **Detail**: `App.razor` forces entire routed component tree into `@rendermode InteractiveServer`. Cookie-mutating operations (sign-in/sign-out) require HTTP request/response cycle, not WebSocket message. Auth pages calling SignInManager would fail at runtime.
- **Fix A ⭐ Recommended**: Static SSR auth area — auth pages use no `@rendermode` (static), wizard stays interactive. Change App.razor to not force global render mode; apply per-page instead.
  - Strength: Standard Microsoft pattern for Blazor Identity in .NET 8/9.
  - Tradeoff: Auth pages lose interactivity (acceptable for simple forms).
  - Confidence: HIGH — documented Microsoft pattern.
  - Blind spot: None significant.
- **Fix B**: HTTP POST endpoints for auth — cookie ops handled via form POST to minimal API.
  - Strength: Auth pages remain interactive Blazor components.
  - Tradeoff: More complex, non-standard; antiforgery token handling unclear.
  - Confidence: MEDIUM.
  - Blind spot: Antiforgery between Blazor and HTTP endpoints.
- **Decision**: FIXED via Fix A — auth pages render as static SSR; App.razor global render mode removed; wizard declares `@rendermode InteractiveServer` explicitly.

### F2 — /healthz endpoint needs explicit [AllowAnonymous]

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 2 — Auth Middleware Pipeline
- **Detail**: Railway health probe sends unauthenticated requests to `/healthz`. Once authorization middleware is added, endpoint may require auth — breaking health checks.
- **Fix**: Add `.AllowAnonymous()` to the health endpoint.
- **Decision**: FIXED — added to Phase 2 contract.

### F3 — Email sender registered in Phase 5 but Forgot Password page in Phase 3

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 3 (ForgotPassword.razor) vs Phase 5 (SmtpEmailSender)
- **Detail**: Phase 3 builds Forgot Password calling `IEmailSender<ApplicationUser>`, but implementation wasn't registered until Phase 5. DI resolution would fail between phases.
- **Fix A ⭐ Recommended**: Register no-op stub in Phase 2; replace in Phase 5.
- **Fix B**: Move email sender registration to Phase 2.
- **Decision**: FIXED via Fix B — email sender (SmtpEmailSender with console-log fallback when unconfigured) moved to Phase 2.

### F4 — Wizard.razor already injects IJSRuntime (undocumented in plan)

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 4 — Wizard.razor changes
- **Detail**: Plan omitted `IJSRuntime` injection in Wizard.razor's current state description.
- **Fix**: Add IJSRuntime to Key Discoveries.
- **Decision**: FIXED — added to Key Discoveries bullet.
