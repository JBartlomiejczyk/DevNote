# Note Management — Plan Brief

> Full plan: `context/changes/note-management/plan.md`

## What & Why

Add the ability for users to view past conversation notes, re-enter the wizard to edit them, and re-classify after editing. This completes the core CRUD cycle (S-03) so developers can iterate on their notes across multiple sessions — essential for the multi-session market-feedback validation goal.

## Starting Point

S-02 (auth + note persistence) is complete: users can register, log in, fill the wizard, classify, and have the result saved as a `ConversationNote` in PostgreSQL. The entity has all fields needed (status, classification, wizard data, summary, timestamps). However, there's no way to see past notes or edit them — the wizard always creates new notes and the result is shown inline once.

## Desired End State

A logged-in user can navigate to `/notes` from the header, see all their notes listed (newest first) with title, date, status, and classification badge, click any note to re-enter the wizard pre-filled with that note's data, modify fields, re-classify, and have the existing note updated in place. The workflow is: list → edit → re-classify → back to list.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
|---|---|---|
| Notes list route | `/notes` as separate page | Clean separation from wizard; header nav link always accessible |
| List item content | Title + date + status + classification badge | Matches FR-008 acceptance criteria directly; scannable |
| Edit entry point | Click row → wizard pre-filled | One click to edit; reuses existing wizard UI |
| Status revert timing | Revert to Draft on page load | Matches PRD FR-009 exactly; clear state signaling |
| Re-classify behavior | Overwrite existing note | Simplest; no version history needed for MVP |
| Navigation | Header link "Moje notatki" | Always accessible, follows existing header pattern |
| Sort/filter | Newest first, no filter | MVP scale is small; premature to add filtering |

## Scope

**In scope:**
- `NoteService` methods: list, get, update, revert-to-draft
- `/notes` list page with note cards
- `/edit/{id}` page with pre-filled wizard and update-on-classify
- Header navigation link
- Post-classify inline result with a "Przejdź do notatek" link (both create and edit flows)
- CSS for notes list

**Out of scope:**
- Note deletion
- Draft auto-save during wizard fill
- Filtering, searching, or pagination
- Note version history
- Viewing summary/result from list without entering edit mode

## Architecture / Approach

Extends the existing service layer (`NoteService`) with 4 new methods. Adds two new Blazor pages (`Notes.razor` at `/notes`, `EditNote.razor` at `/edit/{id}`). The edit page duplicates the wizard form markup (avoids premature abstraction) but calls `UpdateNoteAsync` instead of `CreateNoteAsync`. `WizardStateService.Data` is populated from the loaded note before the edit page renders. No schema changes or migrations needed.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. NoteService Extensions | List/get/update/revert methods | None — straightforward EF Core queries |
| 2. Notes List Page | `/notes` page with note cards | Styling alignment with existing UI |
| 3. Wizard Edit Mode | `/edit/{id}` with pre-fill + update | State management: ensuring wizard loads correctly from DB |
| 4. Navigation & Styling | Header link, CSS, "Nowa notatka" button | None — cosmetic polish |

**Prerequisites:** S-02 (auth-and-note-persistence) must be fully implemented and deployed.
**Estimated effort:** ~2 sessions across 4 phases.

## Open Risks & Assumptions

- Assumes S-02 is complete and `ConversationNote` entity + `NoteService.CreateNoteAsync` are working in production
- `WizardStateService` is scoped per-circuit; if user opens edit in a new tab while wizard is in progress, the scoped state may conflict (acceptable for MVP — single-tab usage expected)
- No confirmation dialog on status revert — user clicking a note immediately marks it Draft

## Success Criteria (Summary)

- User can see all past notes in a list at `/notes` with correct metadata
- User can edit any note by clicking it, modifying wizard fields, and re-classifying
- Re-classification updates the existing note (no duplicates) and reverts→completes the status cycle
