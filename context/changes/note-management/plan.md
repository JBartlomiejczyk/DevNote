# Note Management Implementation Plan

## Overview

Add the ability for authenticated users to view a list of past conversation notes, re-enter the wizard to edit any note, and re-classify after editing — completing the core CRUD cycle for the `ConversationNote` entity. This is S-03 in the roadmap, building directly on S-02 (auth + note persistence).

## Current State Analysis

- **Entity**: `ConversationNote` with all needed fields (Title, Status, Classification, 8 wizard fields, 11 summary fields, timestamps) — no schema changes required
- **Service**: `NoteService` has only `CreateNoteAsync` — needs list/get/update methods
- **Wizard**: `Wizard.razor` at `/` always creates a new note via `NoteService.CreateNoteAsync` on classify — needs edit-mode branch
- **State**: `WizardStateService` holds in-memory `WizardData` (scoped per circuit) — editing a note means populating it before the wizard renders
- **UI**: Card-based panels, Polish-language labels, `@onchange` for textareas (lesson: no `@oninput` in Blazor Server)
- **Auth**: Fallback policy requires authenticated user; `AuthenticationStateProvider` used in wizard to get `userId`
- **Routing**: `AuthorizeRouteView` in `Routes.razor`; current pages: `/` (wizard), `/login`, `/register`, `/forgot-password`, `/reset-password`, `/logout`

### Key Discoveries:

- `NoteService.CreateNoteAsync` sets status to `Completed` immediately — the create flow never produces drafts; edits are the only source of Draft status
- `WizardStateService` is a simple wrapper around `WizardData` with no persistence — loading a note means overwriting `State.Data` properties
- `MainLayout.razor` header has space for navigation — currently shows app name + auth status (name + logout or login link)
- `ClassificationService.ClassifyAsync(WizardData)` is stateless and reusable for re-classification without modification
- No `NavigationManager` injection in `Wizard.razor` currently — will need it for redirect after save

## Desired End State

After this plan is complete:

1. A logged-in user sees a "Moje notatki" link in the header that navigates to `/notes`
2. The `/notes` page displays all user's notes ordered newest-first, showing title, creation date, status badge (Draft/Completed), and classification badge (A/B/C or "—")
3. Clicking a note row navigates to `/edit/{noteId}` which loads the wizard pre-filled with that note's data
4. Loading a Completed note for editing immediately reverts its status to Draft in the database
5. The "Klasyfikuj" button in edit mode updates the existing note (overwriting wizard data + classification + summary) and sets status back to Completed
6. After successful classification (create or edit), the user sees the result inline with a "Przejdź do notatek" link to navigate to `/notes`
7. The wizard at `/` continues to work as before for creating new notes

**Verification**: Log in → create a note via wizard → navigate to /notes → see note listed as Completed with classification → click it → wizard pre-fills → modify a field → re-classify → note is updated (not duplicated) → /notes shows updated title/date → verify note count unchanged in DB.

## What We're NOT Doing

- Note deletion (not in PRD scope)
- Draft auto-save during wizard fill (save only on classify)
- Filtering or searching notes (MVP scale is small)
- Pagination (unnecessary for expected note volume)
- Note version history or undo
- Sharing notes between users
- Viewing the classification result/summary from the list (must re-enter wizard or re-classify)

## Implementation Approach

Layer-by-layer: service methods first (testable independently), then the notes list page (new route), then modify the wizard to support edit mode (most complex change), and finally navigation wiring + CSS. Each phase is independently verifiable.

## Phase 1: NoteService Extensions

### Overview

Add methods to `NoteService` for listing, loading, and updating notes. These form the data access layer for all UI work in subsequent phases.

### Changes Required:

#### 1. List notes for user

**File**: `Services/NoteService.cs`

**Intent**: Add a method that returns all notes for a given user, ordered by creation date descending (newest first).

**Contract**: `Task<List<ConversationNote>> GetNotesForUserAsync(string userId)` — returns notes ordered by `CreatedAt` descending. No pagination, no filtering.

#### 2. Get single note by ID

**File**: `Services/NoteService.cs`

**Intent**: Add a method that loads a single note by ID, scoped to the requesting user (prevents accessing another user's notes).

**Contract**: `Task<ConversationNote?> GetNoteAsync(Guid noteId, string userId)` — returns null if not found or wrong user. Single query with both `Id` and `UserId` in the WHERE clause.

#### 3. Update existing note after re-classification

**File**: `Services/NoteService.cs`

**Intent**: Add a method that overwrites a note's wizard data, classification result, title, timestamps, and status after re-classification.

**Contract**: `Task<ConversationNote> UpdateNoteAsync(Guid noteId, string userId, WizardData wizardData, ClassificationResult result)` — loads note (throws if not found/wrong user), overwrites wizard fields, classification fields, summary fields, recalculates title from Problem field, sets `Status = Completed`, sets `UpdatedAt = UtcNow`, saves. Returns the updated entity.

#### 4. Revert note status to Draft

**File**: `Services/NoteService.cs`

**Intent**: Add a method that marks a Completed note as Draft when the user opens it for editing. Called when the edit page loads.

**Contract**: `Task RevertToDraftAsync(Guid noteId, string userId)` — loads note, if status is Completed sets `Status = Draft` and `UpdatedAt = UtcNow`, saves. No-op if already Draft. Throws if not found/wrong user.

**Also verify**: `Data/ApplicationDbContext.cs` — confirm `UserId` index on `ConversationNotes` is in place and new LINQ queries use it correctly (no full-table scans).

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- All four new methods exist and are callable

#### Manual Verification:

- Query `/admin/db-check` endpoint to verify note status changes after calling methods

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Notes List Page

### Overview

Create a new Blazor page at `/notes` that displays all of the current user's notes in a list/card layout with title, date, status, and classification badge. Each row links to the edit page.

### Changes Required:

#### 1. Notes list page component

**File**: `Components/Pages/Notes.razor`

**Intent**: New page at route `/notes` that fetches and displays all user notes. Uses `InteractiveServer` render mode (same as wizard). Shows an empty state message when no notes exist.

**Contract**: Route `@page "/notes"`. Injects `NoteService` and `AuthenticationStateProvider`. On initialize, gets userId and calls `GetNotesForUserAsync`. Renders a list of note cards. Each card shows: Title, `CreatedAt` formatted as date, status badge ("Szkic"/"Ukończona"), classification badge (A/B/C or "—"). Each card is a clickable link navigating to `/edit/{note.Id}`. Empty state: "Nie masz jeszcze żadnych notatek." with a link to `/` to create one.

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- Page is routable at `/notes`

#### Manual Verification:

- Navigate to `/notes` while logged in — see list of existing notes
- Verify title, date, status badge, classification badge render correctly
- Empty state shows when user has no notes
- Clicking a note navigates to `/edit/{id}`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Wizard Edit Mode

### Overview

Modify the wizard to support loading an existing note by ID, pre-filling all fields, and updating (instead of creating) on re-classification. This is the most complex phase — it adds a new route and branching logic to the existing wizard.

### Changes Required:

#### 1. Edit page wrapper

**File**: `Components/Pages/EditNote.razor`

**Intent**: New page at route `/edit/{NoteId:guid}` that loads the note, reverts it to Draft, populates `WizardStateService`, and renders the wizard in edit mode. Separating this from the main wizard avoids complicating the create flow.

**Contract**: Route `@page "/edit/{NoteId:guid}"`. Parameter `[Parameter] public Guid NoteId { get; set; }`. On initialize: get userId, call `NoteService.GetNoteAsync`, if null redirect to `/notes`. Call `RevertToDraftAsync`. Populate `WizardStateService.Data` fields from the loaded note (all editable fields + title/classification context). Store `NoteId` in a field accessible to the classify handler. Render the same wizard UI (reuse `WizardSection` components). The "Klasyfikuj" button calls `NoteService.UpdateNoteAsync` instead of `CreateNoteAsync`. After successful update, show the result inline and keep a "Przejdź do notatek" link for manual navigation.

#### 2. Extract wizard form as shared markup or keep inline

**Intent**: The edit page needs the same accordion + classify button UI as `Wizard.razor`. Rather than creating a shared component (over-engineering for 2 usages), duplicate the wizard form markup in `EditNote.razor` with the different submit handler. This keeps both pages simple and independently modifiable.

**Contract**: `EditNote.razor` contains the same 8 `WizardSection` bindings and "Klasyfikuj" button as `Wizard.razor`, but its `OnClassify` method calls `UpdateNoteAsync`, renders the updated result inline, and exposes a link back to `/notes`.

#### 3. Keep result display and add navigation link

**File**: `Components/Pages/Wizard.razor`

**Intent**: After a successful new note creation, continue showing the classification result inline (preserving US-05 behavior) and add a "Przejdź do notatek" link below the result so the user can navigate to `/notes` when ready.

**Contract**: Inject `NavigationManager`. Keep the existing inline `Result` component display after classify. Add an anchor `<a href="/notes">Przejdź do notatek →</a>` below the result panel. Keep the `noteSaved` confirmation. Explicit state rule: when the `/` wizard page initializes for create flow, it clears `WizardStateService.Data` to a fresh `WizardData` instance before user input; `/edit/{id}` is the only route that hydrates state from an existing note.

#### 4. Show result in EditNote after re-classify

**File**: `Components/Pages/EditNote.razor`

**Intent**: After successful re-classification in edit mode, show the updated result inline (same as create flow) with a link to navigate back to `/notes`.

**Contract**: `EditNote.razor` stores the `ClassificationResult` after `UpdateNoteAsync` succeeds. Renders the `Result` component (`Components/Pages/Result.razor`) below the wizard form when result is available. Adds a "Przejdź do notatek →" link below the result.

**Also verify**: `Components/Shared/RedirectToLogin.razor` — ensure new routes `/notes` and `/edit/{id}` are properly protected by the fallback auth policy (no `[AllowAnonymous]`); the existing redirect-to-login component will handle unauthenticated access automatically.

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- `/edit/{guid}` route resolves

#### Manual Verification:

- Navigate to `/edit/{existingNoteId}` — wizard pre-fills with note data
- Note status is now Draft in DB
- Modify a field and click "Klasyfikuj" — note is updated (check DB: same ID, new content, status Completed)
- Classification result and summary display inline after re-classify
- "Przejdź do notatek" link navigates to `/notes`
- Creating a new note via `/` shows result inline with link to /notes
- Editing a note that doesn't belong to user (or bad GUID) redirects to `/notes`
- Unauthenticated access to `/notes` or `/edit/{id}` redirects to login page (return-url works correctly)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Navigation & Styling

### Overview

Wire up header navigation to `/notes`, add "Nowa notatka" link on the notes page, and add CSS styles for the notes list.

### Changes Required:

#### 1. Header navigation link

**File**: `Components/Layout/MainLayout.razor`

**Intent**: Add a "Moje notatki" link in the header for authenticated users, providing constant access to the notes list.

**Contract**: Inside the `<Authorized>` section, add an anchor `<a href="/notes">Moje notatki</a>` before the user name/logout area.

#### 2. Notes list CSS

**File**: `wwwroot/css/app.css`

**Intent**: Add styles for the notes list page — note cards, status/classification badges, empty state, hover effects.

**Contract**: New CSS class family `.notes-*`: `.notes-list` (container), `.notes-item` (clickable card row with hover), `.notes-title`, `.notes-date`, `.notes-status` (badge — green for Completed, gray for Draft), `.notes-classification` (badge — colored per A/B/C like existing `.badge-a/b/c`). Follow existing card pattern (white bg, border, border-radius, subtle shadow). Empty state uses `.notes-empty`.

#### 3. New note link on list page

**File**: `Components/Pages/Notes.razor`

**Intent**: Add a "Nowa notatka" button/link at the top of the notes page that navigates to `/` (the wizard) for creating a new note.

**Contract**: Anchor styled as button: `<a href="/" class="btn-new-note">Nowa notatka</a>` placed above the notes list.

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- CSS has no syntax errors (page renders)

#### Manual Verification:

- Header shows "Moje notatki" link when logged in — navigates to `/notes`
- Notes list page is styled with cards, badges are colored correctly
- "Nowa notatka" button on /notes navigates to wizard
- Hover effects on note cards work
- Mobile responsive (cards stack vertically)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- `NoteService.GetNotesForUserAsync` returns only notes for the specified user, ordered by newest first
- `NoteService.GetNoteAsync` returns null for wrong user or non-existent ID
- `NoteService.UpdateNoteAsync` overwrites all fields and sets status to Completed
- `NoteService.RevertToDraftAsync` changes Completed to Draft, no-ops on Draft

### Integration Tests:

- Create note → list → edit → re-classify → verify single note updated (not duplicated)
- Attempt to access another user's note via `/edit/{id}` → redirected to `/notes`

### Manual Testing Steps:

1. Log in and create a note via the wizard — verify inline result is shown with a link to /notes
2. See note in list with correct title, date, "Ukończona" badge, and classification badge
3. Click note — wizard pre-fills, note status shows Draft in DB
4. Modify Problem field, re-classify — note updates, back to Completed
5. Verify note count unchanged (no duplicates)
6. Log in as different user — verify /notes is empty, cannot access other user's note by URL

## Performance Considerations

- No pagination needed for MVP scale (expected <50 notes per user)
- `GetNotesForUserAsync` uses the existing `UserId` index on `ConversationNotes`
- No eager loading of navigation properties needed for list (only scalar fields displayed)

## References

- Roadmap: `context/foundation/roadmap.md` § S-03
- PRD: `context/foundation/prd.md` § FR-008, FR-009, US-06
- Prerequisite plan: `context/changes/auth-and-note-persistence/plan.md`
- Existing entity: `Models/ConversationNote.cs`
- Existing service: `Services/NoteService.cs`
- Lesson: never use `@oninput` in Blazor Server on high-latency — `context/foundation/lessons.md`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: NoteService Extensions

#### Automated

- [x] 1.1 `dotnet build` compiles without errors — 44d10c3
- [x] 1.2 All four new methods exist and are callable — 44d10c3

#### Manual

- [x] 1.3 Verify note status changes via `/admin/db-check` — 44d10c3

### Phase 2: Notes List Page

#### Automated

- [x] 2.1 `dotnet build` compiles without errors — 7d79547
- [x] 2.2 Page is routable at `/notes` — 7d79547

#### Manual

- [ ] 2.3 Navigate to `/notes` while logged in — see list of existing notes
- [ ] 2.4 Verify title, date, status badge, classification badge render correctly
- [ ] 2.5 Empty state shows when user has no notes
- [ ] 2.6 Clicking a note navigates to `/edit/{id}`

### Phase 3: Wizard Edit Mode

#### Automated

- [x] 3.1 `dotnet build` compiles without errors
- [x] 3.2 `/edit/{guid}` route resolves

#### Manual

- [ ] 3.3 Wizard pre-fills with note data on edit page
- [ ] 3.4 Note status reverts to Draft on edit load
- [ ] 3.5 Re-classify updates existing note (no duplicates)
- [ ] 3.6 Classification result and summary display inline after re-classify
- [ ] 3.7 "Przejdź do notatek" link navigates to `/notes`
- [ ] 3.8 Creating a new note via `/` shows result inline with link to /notes
- [ ] 3.9 Editing a note that doesn't belong to user (or bad GUID) redirects to `/notes`
- [ ] 3.10 Unauthenticated access to `/notes` or `/edit/{id}` redirects to login with return-url

### Phase 4: Navigation & Styling

#### Automated

- [ ] 4.1 `dotnet build` compiles without errors
- [ ] 4.2 CSS has no syntax errors

#### Manual

- [ ] 4.3 Header "Moje notatki" link visible and works
- [ ] 4.4 Notes list styled with cards and colored badges
- [ ] 4.5 "Nowa notatka" button navigates to wizard
- [ ] 4.6 Hover effects on note cards work
- [ ] 4.7 Mobile responsive (cards stack vertically)
