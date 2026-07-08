---
date: 2026-07-08T12:38:38.051+02:00
researcher: Copilot CLI (AI assistant)
git_commit: 60997c6ccd42271d365b2857e09be27fe94cd08e
branch: master
repository: JBartlomiejczyk/DevNote
topic: "Risk #1 end-to-end: classification/summary integrity and malformed response handling"
tags: [research, codebase, classification, summary-integrity, testing]
status: complete
last_updated: 2026-07-08
last_updated_by: Copilot CLI (AI assistant)
---

# Research: Risk #1 end-to-end classification/summary integrity

**Date**: 2026-07-08T12:38:38.051+02:00  
**Researcher**: Copilot CLI (AI assistant)  
**Git Commit**: 60997c6ccd42271d365b2857e09be27fe94cd08e  
**Branch**: master  
**Repository**: JBartlomiejczyk/DevNote

## Research Question

How does the current app flow handle classification + summary generation for a completed wizard, and where can malformed/partial LLM responses still produce wrong or blank-but-valid outputs? Scope includes phase-1 unit-test seams and xUnit bootstrap readiness.

## Summary

- The submit flow is clear and centralized: wizard submit calls `ClassificationService.ClassifyAsync`, then renders and persists the mapped result.
- The JSON schema is strict at request level, but runtime integrity still has risk points:
  - unknown `classification` silently defaults to `B`;
  - nullable summary values are normalized to empty strings, which can look "valid";
  - broad `catch (Exception)` in UI surfaces only a generic error and hides failure type.
- There is currently no test project and CI runs build only; phase 1 must bootstrap xUnit before integrity coverage can be enforced.

## Detailed Findings

### 1) Runtime flow: wizard completion to persisted result

- Submit buttons in both create/edit paths call `OnClassify` and invoke `ClassificationService.ClassifyAsync(State.Data)`:
  - `Components/Pages/Wizard.razor:99-102,172-183`
  - `Components/Pages/EditNote.razor:100-103,191-204`
- Parsed result is then persisted:
  - create flow: `NoteService.CreateNoteAsync(...)` in `Wizard.razor:188-190`
  - edit flow: `NoteService.UpdateNoteAsync(...)` in `EditNote.razor:203-204`
- UI render of summary uses 11 displayed sections in `Result.razor:13-57`.

### 2) Contract definition and enforcement points

- Classification + summary contract is expressed in two places:
  - LLM response JSON schema with enum `["A","B","C"]`, required fields, and `additionalProperties: false` (`Services/ClassificationService.cs:32-93`);
  - app DTO + enum (`Models/ClassificationResult.cs:3-25`).
- PRD requires classification A/B/C and 11 summary fields (`context/foundation/prd.md:90-104,138-140`), matching the model/UI surfaces.

### 3) Integrity gaps for risk #1

- Unknown classification fallback:
  - `ParseResponse` maps unknown token to `Classification.B` (`Services/ClassificationService.cs:168-175`), potentially masking drift instead of surfacing it.
- Null-to-empty coercion:
  - all parsed text fields use `?? string.Empty` (`Services/ClassificationService.cs:180-191`), allowing empty-but-renderable summary output.
- Error surfacing is generic:
  - both wizard pages catch all exceptions and show one generic banner (`Wizard.razor:192-195`, `EditNote.razor:205-208`).
  - malformed JSON or missing required properties will throw during `JsonDocument.Parse` / `GetProperty`, but details are hidden from the user path.

### 4) Persistence and render mapping

- Result fields are copied into note summary columns in both create and update flows:
  - `Services/NoteService.cs:38-48,92-103`
- Entity stores all summary fields and classification:
  - `Models/ConversationNote.cs:10-35`
- This means any silent defaulting in parse step propagates directly to persisted business output.

### 5) Test surface and phase-1 readiness

- No test project currently exists:
  - only app csproj present (`dev-note.csproj:1-20`);
  - test-file glob in repo yields no `*Tests*.cs`.
- CI currently runs restore+build only, no `dotnet test`:
  - `.github/workflows/deploy.yml:19-24`
- Primary unit seam is parse/mapping logic in `ClassificationService.ParseResponse` (`163-193`), but it is private and currently coupled with network call path inside `ClassifyAsync`.

## Code References

- [`Services/ClassificationService.cs:32-93`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Services/ClassificationService.cs#L32-L93) - strict JSON schema definition.
- [`Services/ClassificationService.cs:101-161`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Services/ClassificationService.cs#L101-L161) - LLM call orchestration.
- [`Services/ClassificationService.cs:163-193`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Services/ClassificationService.cs#L163-L193) - parse + mapping and fallback behavior.
- [`Components/Pages/Wizard.razor:172-206`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Components/Pages/Wizard.razor#L172-L206) - create-note classify flow and broad exception handling.
- [`Components/Pages/EditNote.razor:191-219`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Components/Pages/EditNote.razor#L191-L219) - edit-note classify flow and broad exception handling.
- [`Services/NoteService.cs:16-57`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Services/NoteService.cs#L16-L57) - create persistence mapping.
- [`Services/NoteService.cs:73-107`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Services/NoteService.cs#L73-L107) - update persistence mapping.
- [`Models/ClassificationResult.cs:3-25`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Models/ClassificationResult.cs#L3-L25) - classification + summary model contract.
- [`Components/Pages/Result.razor:13-57`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/Components/Pages/Result.razor#L13-L57) - rendered summary field surfaces.
- [`dev-note.csproj:1-20`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/dev-note.csproj#L1-L20) - no test project/packages.
- [`.github/workflows/deploy.yml:19-24`](https://github.com/JBartlomiejczyk/DevNote/blob/60997c6ccd42271d365b2857e09be27fe94cd08e/.github/workflows/deploy.yml#L19-L24) - CI does not run tests yet.

## Architecture Insights

- Current architecture puts both transport concerns (Azure call) and parse semantics in one service, while UI layers own final exception presentation. This is workable but reduces direct unit-testability for parse integrity unless a seam is extracted or exposed.
- Integrity risk is not about "schema vs no schema"; it is about post-schema interpretation choices (fallback classification and empty-string coercion) and how those choices propagate into persisted note summaries.

## Historical Context (from prior changes)

- Original north-star implementation intentionally used single LLM call for classification + 11-field summary (`context/changes/wizard-classification-summary/plan.md:19,157-197`).
- Current phase intent in `change.md` explicitly challenges "strict schema is enough" and requires malformed/partial response surfacing (`context/changes/testing-classification-summary-integrity/change.md:12-16`).
- Test plan phase 1 names this exact risk as top priority and requires unit-first proof (`context/foundation/test-plan.md:46,65,83`).
- No archived implementation artifacts yet (archive currently only README): `context/archive/README.md:1-3`.

## Related Research

- No prior `research.md` artifacts found under `context/changes/**` or `context/archive/**` in this repository at the time of writing.

## Open Questions

1. Should unknown `classification` remain a silent default to `B`, or become an explicit parse failure path?
2. Which summary fields are allowed to be empty by policy, and which must fail-fast when empty/null after parse?
3. For phase-1 unit tests, should parse logic be exposed as an internal seam or extracted into a dedicated mapper/validator component?
