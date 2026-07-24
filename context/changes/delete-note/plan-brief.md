# Delete Note — Plan Brief

> Full plan: `context/changes/delete-note/plan.md`

## What & Why

Użytkownik potrzebuje możliwości trwałego usunięcia notatki z listy swoich notatek. Aktualnie nie ma żadnej drogi usunięcia notatki poza usunięciem całego konta. Funkcja uzupełnia podstawowe CRUD dla notatek.

## Starting Point

`Notes.razor` renderuje listę notatek jako `<ul>` bez żadnej opcji usunięcia. `NoteService` ma metody create/read/update ale brak `DeleteNoteAsync`. Model nie wymaga zmian — brak migracji.

## Desired End State

Na liście notatek (`/notes`) pojawia się ikona kosza przy każdym wierszu po najechaniu myszą. Kliknięcie pokazuje natywny dialog potwierdzenia. Po potwierdzeniu notatka znika z listy natychmiast (in-place, bez przeładowania) i jest trwale usunięta z bazy danych.

## Key Decisions Made

| Decision | Choice | Why |
|---|---|---|
| Potwierdzenie | `confirm()` przeglądarki | Wystarczające dla LOW complexity, zero nowego komponentu |
| Widoczność przycisku | Ikona kosza tylko na hover | Czyste UI — nie zaśmieca listy, zgodne z developer-notebook stylem |
| Aktualizacja listy | In-place (usunięcie z lokalnej listy) | Najlepsza UX — brak migotania strony, natychmiastowa reakcja |

## Scope

**In scope:**
- `DeleteNoteAsync(Guid, userId)` w serwisie z ownership checkiem
- Ikona `trash-2` (Lucide, już załadowane) na hover w `Notes.razor`
- `confirm()` dialog (JS interop)
- In-place usunięcie z listy po potwierdzeniu

**Out of scope:**
- Soft delete / undo / kosz
- Bulk delete
- Custom modal
- Żadna migracja bazy

## Architecture / Approach

Dwie warstwy: (1) serwis — `DeleteNoteAsync` z ownership guard identycznym jak w `GetNoteAsync`; (2) UI — `IJSRuntime` inject, `confirm()` call, `notes.Remove()` + `StateHasChanged()`. Lucide re-init w `OnAfterRenderAsync` (ten sam wzorzec co `WizardSection.razor`).

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Service layer | `DeleteNoteAsync` z ownership check | Brak — prosty wzorzec |
| 2. UI — trash icon + confirm | Ikona, dialog, in-place usunięcie, CSS hover | Lucide wymaga `createIcons()` po re-render |

**Prerequisites:** brak  
**Estimated effort:** ~1 sesja, 2 fazy

## Open Risks & Assumptions

- Lucide icons muszą być re-inicjalizowane po każdym Blazor render cycle — wzorzec z `WizardSection.razor` wystarczy.

## Success Criteria (Summary)

- Notatka znika z listy natychmiast po potwierdzeniu usunięcia
- Odświeżenie `/notes` potwierdza trwałe usunięcie
- Inne notatki użytkownika są nienaruszone
