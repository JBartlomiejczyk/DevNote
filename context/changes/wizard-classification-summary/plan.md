# Plan: wizard-classification-summary

## Change Identity

- **Change ID**: wizard-classification-summary
- **Title**: 8-Section Wizard + A/B/C Classification + Structured Summary
- **PRD refs**: US-02, US-04, US-05, FR-003, FR-005, FR-006, FR-007
- **Roadmap ref**: S-01 (north star feature)
- **Complexity**: HIGH
- **Estimated phases**: 3

## Decisions Log

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | Blazor Server hosting model | Simpler deploy on Railway (no WASM download), SignalR state management aligns with circuit-scoped services |
| D2 | In-memory wizard state (circuit-scoped) | No persistence in this slice (S-02 adds it). State lives in a DI-scoped service per Blazor circuit |
| D3 | Azure OpenAI (gpt-4o-mini) | Enterprise-grade, cost-effective model. Structured output JSON schema support |
| D4 | Single LLM call — combined classification + summary | Reduces latency and cost. One prompt returns A/B/C + justification + 11-field summary |
| D5 | Polish-only UI | All labels, prompts, wizard sections, and LLM output in Polish |
| D6 | Single-page accordion wizard | All 8 sections visible and scrollable. Active section highlighted. Better for review |
| D7 | Retry button on LLM failure | Show error message, preserve wizard data, let user retry manually |
| D8 | Formatted card/panel result display | Classification badge + labeled summary sections in a clean panel |

## End State

After this change is implemented:

1. The app serves a Blazor Server UI at the root URL
2. User sees an 8-section accordion wizard (all Polish) with sections: Problem, Proces, Strata czasu, Dane wejściowe, Oczekiwany wynik, Ryzyka, Użytkownicy, Skala
3. Each section has a label, description hint, and a single free-text textarea
4. User can expand/collapse sections freely, fill them in any order
5. A "Klasyfikuj" (Classify) submit button at the bottom triggers an Azure OpenAI call
6. The system returns: A/B/C classification badge with justification + 11-field structured summary
7. Result is displayed in a formatted panel below the wizard
8. On LLM failure, an error message with a retry button appears (wizard data preserved)
9. The `/healthz` endpoint still works. The `/weatherforecast` endpoint is removed

## File Contracts

### New Files

| File | Purpose |
|------|---------|
| `Components/_Imports.razor` | Global using directives for Razor components |
| `Components/App.razor` | Root Blazor component (HTML shell, HeadOutlet, Routes) |
| `Components/Routes.razor` | Router component |
| `Components/Layout/MainLayout.razor` | Main layout wrapper |
| `Components/Pages/Wizard.razor` | Wizard page — accordion UI + submit logic |
| `Components/Pages/Wizard.razor.css` | Scoped CSS for wizard page |
| `Components/Pages/Result.razor` | Result display component (classification + summary panel) |
| `Components/Shared/WizardSection.razor` | Reusable accordion section component |
| `Services/WizardStateService.cs` | Circuit-scoped in-memory state for 8 sections |
| `Services/ClassificationService.cs` | Azure OpenAI integration — prompt + structured output |
| `Models/WizardData.cs` | Data model: 8 section text fields |
| `Models/ClassificationResult.cs` | Data model: classification (A/B/C), justification, 11-field summary |
| `wwwroot/css/app.css` | Base application styles |

### Modified Files

| File | Change |
|------|--------|
| `Program.cs` | Add Blazor Server services, map Razor components, remove /weatherforecast |
| `dev-note.csproj` | Add `Azure.AI.OpenAI` package reference |
| `appsettings.json` | Add `AzureOpenAI` configuration section (endpoint, deployment, key placeholder) |
| `appsettings.Development.json` | Development-specific Azure OpenAI config |

## Success Criteria

- [ ] Blazor Server renders at `/` with the 8-section wizard
- [ ] All 8 sections display with Polish labels and textarea inputs
- [ ] Accordion expand/collapse works without losing data
- [ ] Submit calls Azure OpenAI and returns structured JSON
- [ ] Classification badge displays A, B, or C with color
- [ ] 11-field summary renders in formatted panel
- [ ] LLM failure shows error with retry button (data preserved)
- [ ] `/healthz` returns 200
- [ ] `/weatherforecast` no longer exists
- [ ] App builds and runs locally (`dotnet build` + `dotnet run`)

---

## Phase 1: Blazor Server Setup + Wizard UI

### Goal

Add Blazor Server to the existing ASP.NET Core 9 project and build the complete 8-section accordion wizard UI with in-memory state management.

### Tasks

1. **Add Blazor infrastructure to `Program.cs`**
   - Add `builder.Services.AddRazorComponents().AddInteractiveServerComponents()`
   - Add `app.MapRazorComponents<App>().AddInteractiveServerRenderMode()`
   - Add `app.UseStaticFiles()` and `app.UseAntiforgery()`
   - Remove the `/weatherforecast` endpoint and `WeatherForecast` record
   - Remove `builder.Services.AddOpenApi()` and `app.MapOpenApi()` (no API endpoints remain)
   - Remove `Microsoft.AspNetCore.OpenApi` package reference from `dev-note.csproj`

2. **Create root Blazor components**
   - `Components/App.razor` — HTML document shell with `<HeadOutlet>`, `<Routes @rendermode="InteractiveServer" />`, and `<script src="_framework/blazor.server.js"></script>`
   - `Components/Routes.razor` — `<Router>` with `<RouteView>` and `<FocusOnNavigate>`
   - `Components/_Imports.razor` — common `@using` directives

3. **Create layout**
   - `Components/Layout/MainLayout.razor` — simple layout with header ("DevNote") and `@Body`

4. **Create data models**
   - `Models/WizardData.cs` — class with 8 string properties (Problem, Process, TimeWaste, InputData, Output, Risks, Users, Scale)
   - `Models/ClassificationResult.cs` — class with Classification enum (A/B/C), Justification string, and 11 summary fields

5. **Create wizard state service**
   - `Services/WizardStateService.cs` — holds `WizardData` instance, registered as Scoped (per-circuit in Blazor Server)

6. **Create reusable accordion section component**
   - `Components/Shared/WizardSection.razor` — accepts Title, Description, bound Value (textarea), IsExpanded state, toggle logic

7. **Create wizard page**
   - `Components/Pages/Wizard.razor` — renders 8 `WizardSection` instances with Polish labels
   - Polish section metadata:
     - Problem → "Problem" / "Opisz problem biznesowy, który chcesz rozwiązać"
     - Process → "Obecny proces" / "Jak wygląda obecny proces? Kto jest zaangażowany?"
     - TimeWaste → "Strata czasu" / "Gdzie tracony jest czas? Jakie są wąskie gardła?"
     - InputData → "Dane wejściowe" / "Jakie dane są potrzebne? Skąd pochodzą?"
     - Output → "Oczekiwany wynik" / "Jaki jest pożądany efekt końcowy?"
     - Risks → "Ryzyka" / "Jakie są ryzyka? Co może pójść nie tak?"
     - Users → "Użytkownicy" / "Kto będzie korzystał z rozwiązania? Ilu użytkowników?"
     - Scale → "Skala" / "Jaka jest skala problemu? Jak często występuje?"
   - Submit button "Klasyfikuj" at bottom (disabled until at least 1 section filled)

8. **Add base CSS**
   - `wwwroot/css/app.css` — minimal styling for accordion, sections, textarea, button
   - `Components/Pages/Wizard.razor.css` — scoped wizard styles

### Verification

```bash
dotnet build
dotnet run
# Navigate to http://localhost:5275 — wizard renders with 8 accordion sections
# Expand/collapse sections, type text, verify no data loss
# Submit button visible (disabled state logic)
```

### Exit Criteria

- App compiles and starts without errors
- Browser shows wizard with 8 Polish-labeled accordion sections
- Text entered in sections persists across accordion expand/collapse
- No JavaScript errors in browser console

---

## Phase 2: Azure OpenAI Integration + Classification Engine

### Goal

Integrate Azure OpenAI SDK, build the classification service with a structured-output prompt, and wire the wizard submit button to trigger classification.

### Tasks

1. **Add NuGet package**
   - `Azure.AI.OpenAI` (latest stable, pulls in `OpenAI` dependency)

2. **Add configuration**
   - `appsettings.json` — add `AzureOpenAI` section:
     ```json
     "AzureOpenAI": {
       "Endpoint": "",
       "DeploymentName": "gpt-4o-mini",
       "ApiKey": ""
     }
     ```
   - **Security**: `ApiKey` in appsettings.json stays empty (placeholder only). For local dev, use `dotnet user-secrets` or `appsettings.Development.json` (gitignored). For Railway production, set `AzureOpenAI__ApiKey` as a service variable (ASP.NET Core env var override convention).
   - Register configuration in DI as `IOptions<AzureOpenAIOptions>`

3. **Create options class**
   - `Services/AzureOpenAIOptions.cs` — POCO with Endpoint, DeploymentName, ApiKey

4. **Create classification service**
   - `Services/ClassificationService.cs`
   - Constructor injects `IOptions<AzureOpenAIOptions>`
   - Method: `Task<ClassificationResult> ClassifyAsync(WizardData data, CancellationToken ct)`
   - Uses `ChatClient` with Azure endpoint and API key
   - System prompt (Polish): instructs LLM to classify using A/B/C methodology, output structured JSON
   - User prompt: concatenates all 8 wizard sections
   - Response format: `ChatResponseFormat.CreateJsonSchemaFormat()` with schema matching `ClassificationResult`
   - Parses JSON response into `ClassificationResult`

5. **Design the classification prompt**
   - System message defines:
     - A = małe lokalne rozwiązanie (skrypt, arkusz, zmiana procesu)
     - B = rozwiązanie departamentalne (wewnętrzne narzędzie o ograniczonym zakresie)
     - C = duże rozwiązanie, wrażliwe dane, wymaga formalnej weryfikacji
   - Instruction: classify conservatively — prefer A over B over C
   - Instruction: for A, explicitly suggest non-code alternatives
   - Output schema: classification, justification, + 11 summary fields (problem, users, currentProcess, timeWaste, inputData, expectedOutput, recommendedPath, mvpScope, outOfScope, acceptanceCriteria, nextStep)

6. **Register service in DI**
   - `Program.cs`: `builder.Services.AddScoped<ClassificationService>()`
   - Bind `AzureOpenAI` config section

7. **Wire wizard submit to service**
   - `Wizard.razor`: inject `ClassificationService`
   - On submit: show loading spinner, call `ClassifyAsync`, store result in component state
   - On success: show result panel (see Task 8)
   - On failure: show error message

8. **Create result display component**
   - `Components/Pages/Result.razor`
   - Accepts `ClassificationResult` as parameter
   - Classification badge: colored pill (A=green, B=yellow, C=red) with letter + Polish label
   - Justification block: italic text below badge
   - 11-field summary: labeled sections in card format (Problem, Użytkownicy, Obecny proces, Strata czasu, Dane wejściowe, Oczekiwany wynik, Rekomendowana ścieżka, Zakres MVP, Poza zakresem, Kryteria akceptacji, Następny krok)
   - Render in Wizard.razor below accordion after successful classification

### Verification

```bash
dotnet build
# Set valid Azure OpenAI credentials in appsettings.Development.json or env vars
dotnet run
# Fill wizard sections with test data, click "Klasyfikuj"
# Verify: loading state appears, then classification result renders
# Test with empty/minimal input — verify graceful handling
```

### Exit Criteria

- Azure OpenAI call succeeds and returns valid JSON
- Classification (A/B/C) + justification + 11 fields parsed correctly
- Result object available in wizard component state after submit
- Invalid/empty API key shows a clear error (not crash)

---

## Phase 3: Result Display + Polish

### Goal

Build the result display panel with classification badge and 11-field summary, add error handling with retry, and apply final styling.

### Tasks

1. **Refine result component styling**
   - Classification badge: polish colors, sizing, typography (A=green, B=yellow, C=red pill)
   - 11-field summary: card borders, spacing, section dividers
   - Responsive layout adjustments

2. **Integrate result into wizard page**
   - Scroll to result panel on completion

3. **Error handling UI**
   - On LLM failure: display red error banner with message
   - "Spróbuj ponownie" (Try again) button that re-triggers `ClassifyAsync`
   - Wizard data remains intact during error state

4. **Loading state**
   - During API call: disable submit button, show spinner with "Klasyfikuję..." text
   - Prevent double-submit

5. **Final CSS polish**
   - Accordion section styling (borders, expand indicator)
   - Textarea sizing (min-height, resize)
   - Result panel card styling
   - Classification badge colors
   - Responsive layout (works on desktop, readable on tablet)
   - Error banner styling

6. **Cleanup**
   - Verify `/weatherforecast` endpoint removed (Phase 1)
   - Verify `/healthz` still works
   - Test full flow end-to-end

### Verification

```bash
dotnet build
dotnet run
# Full flow: fill wizard → submit → see classification + summary
# Test error: set invalid API key → submit → see error + retry button
# Test retry: fix key → click retry → success
# Verify /healthz returns 200
# Verify /weatherforecast returns 404
```

### Exit Criteria

- Classification badge renders with correct color for A/B/C
- All 11 summary fields display with Polish labels
- Error state shows message + retry button
- Retry works without page reload
- Wizard data preserved across submit/error/retry cycle
- App passes `dotnet build` with no warnings

---

## Progress

| Phase | Status | SHA | Notes |
|-------|--------|-----|-------|
| 1 | done | 1c6dda7 | |
| 2 | done | 0894a28 | |
| 3 | done | ef407ad | |
