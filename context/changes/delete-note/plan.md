# Delete Note Implementation Plan

## Overview

Add the ability for a user to delete their own conversation note directly from the notes list. A trash icon appears on hover next to each note item; clicking it prompts the browser's native confirm dialog before performing a permanent delete. The list updates in-place without a full page reload.

## Current State Analysis

- `Components/Pages/Notes.razor` renders all user notes as `<ul class="notes-list">` items; each item is currently a single `<a>` link with no delete affordance.
- `Services/NoteService.cs` has `GetNoteAsync(Guid, userId)` and `UpdateNoteAsync` with ownership checks, but **no `DeleteNoteAsync`**.
- `Data/ApplicationDbContext.cs` has cascade delete on the `User → ConversationNotes` FK, but no user-facing delete path exists.
- Lucide icons are already loaded via CDN (`App.razor`) and working in the sidebar; `data-lucide="trash-2"` is a valid icon name.

## Desired End State

A user on `/notes` sees a trash icon appear on hover for each note. Clicking it shows `confirm("Czy na pewno chcesz usunąć notatkę?")`. On confirmation the note is permanently deleted from the database and removed from the displayed list without page reload. On cancel, nothing happens.

### Key Discoveries

- Ownership check pattern: `GetNoteAsync(Guid noteId, string userId)` uses `.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId)` — the same guard must apply to delete.
- Lucide icons in Blazor Server require `JS.InvokeVoidAsync("lucide.createIcons")` in `OnAfterRenderAsync` (already done in `WizardSection.razor` — same pattern needed here after list re-render).
- `IJSRuntime` is injected in the component layer (not in the service) — `confirm()` call must stay in `Notes.razor`.
- In-place removal: after confirmed delete, call `notes.Remove(note)` and `StateHasChanged()` — no navigation or full reload needed.

## What We're NOT Doing

- No soft delete / trash / undo — this is a permanent hard delete.
- No bulk delete.
- No custom modal dialog — native `confirm()` is sufficient and consistent with LOW complexity scope.
- No migration — no schema change required.

## Implementation Approach

Two-step change: (1) add `DeleteNoteAsync` to the service layer with ownership enforcement, (2) wire the trash icon + confirm + in-place list update in `Notes.razor`.

## Phase 1: Service layer — DeleteNoteAsync

### Overview

Add a single `DeleteNoteAsync(Guid noteId, string userId)` method to `NoteService` that verifies ownership then hard-deletes the note.

### Changes Required

#### 1. NoteService — DeleteNoteAsync method

**File**: `Services/NoteService.cs`

**Intent**: Add a public async method that fetches the note by `noteId` + `userId` (ownership check), then removes it from the DB context and saves. Returns `true` if deleted, `false` if not found or not owned.

**Contract**:
```csharp
public async Task<bool> DeleteNoteAsync(Guid noteId, string userId)
```
- Use `_context.ConversationNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId)` — mirrors the existing ownership pattern in `GetNoteAsync`.
- If `null`, return `false`.
- `_context.ConversationNotes.Remove(note)` then `await _context.SaveChangesAsync()`, return `true`.

### Success Criteria

#### Automated Verification

- Build passes: `dotnet build`
- All tests pass: `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual Verification

- (verified in Phase 2 UI)

---

## Phase 2: UI — trash icon, confirm dialog, in-place list update

### Overview

Update `Notes.razor` to show a Lucide `trash-2` icon on hover for each note, call `confirm()` via JS interop, invoke `DeleteNoteAsync`, and remove the item from the local list on success.

### Changes Required

#### 1. Notes.razor — inject IJSRuntime and NoteService delete call

**File**: `Components/Pages/Notes.razor`

**Intent**: Inject `IJSRuntime` at the top of the component (alongside existing `@inject` lines). Add an `async Task DeleteNote(ConversationNote note)` handler that: calls `JS.InvokeAsync<bool>("confirm", "Czy na pewno chcesz usunąć notatkę?")`, if confirmed calls `NoteService.DeleteNoteAsync(note.Id, userId)`, removes the note from the local `notes` list, and calls `StateHasChanged()`.

**Contract**: The `userId` used in the delete call must be the same string already resolved in `OnInitializedAsync`. Extract it to a field (`private string? _userId`) so both methods can access it.

#### 2. Notes.razor — trash icon in list item markup

**File**: `Components/Pages/Notes.razor`

**Intent**: Wrap each `<li class="notes-item">` content in a relative container so the trash button can be positioned on top-right. Add a `<button>` with an `<i data-lucide="trash-2">` inside, wired to `@onclick="() => DeleteNote(note)"` with `@onclick:stopPropagation="true"` so clicking the icon doesn't also navigate to the edit page.

#### 3. Notes.razor — re-initialize Lucide icons after list render

**File**: `Components/Pages/Notes.razor`

**Intent**: Add `OnAfterRenderAsync(bool firstRender)` override that calls `await JS.InvokeVoidAsync("lucide.createIcons")` — same pattern as `WizardSection.razor` — so Lucide replaces `<i data-lucide="...">` with SVG after each Blazor render cycle.

#### 4. app.css — trash button hover styles

**File**: `wwwroot/css/app.css`

**Intent**: Add CSS for `.notes-item-delete` — positioned absolute top-right of `.notes-item`, hidden by default (`opacity: 0`), visible on `.notes-item:hover` (`opacity: 1`). Use `var(--color-danger)` (already defined as `--ctp-red` in `:root`) for the icon color on hover.

**Contract**:
```css
.notes-item { position: relative; }
.notes-item-delete {
    position: absolute; top: 50%; right: var(--space-md);
    transform: translateY(-50%);
    opacity: 0; transition: opacity var(--transition-fast);
    background: none; border: none; cursor: pointer;
    color: var(--color-text-muted);
}
.notes-item:hover .notes-item-delete { opacity: 1; }
.notes-item-delete:hover { color: var(--color-danger); }
```

### Success Criteria

#### Automated Verification

- Build passes: `dotnet build`
- All tests pass: `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual Verification

- Trash icon appears on hover over a note row
- Icon is NOT visible when not hovering
- Clicking the icon shows `confirm()` dialog with Polish text
- Cancelling confirm — note remains in the list
- Confirming delete — note disappears from list immediately (no reload)
- Navigating back to `/notes` — deleted note does not reappear
- Another user cannot delete notes they don't own (service returns false)

---

## Testing Strategy

### Unit Tests

No new unit tests required — the ownership logic in `DeleteNoteAsync` follows the exact same pattern as `GetNoteAsync` which is already tested. The delete path is covered by the existing test infrastructure.

### Manual Testing Steps

1. Log in, go to `/notes`
2. Hover over a note — confirm trash icon appears
3. Move mouse away — confirm icon disappears
4. Click trash icon → confirm dialog appears with "Czy na pewno chcesz usunąć notatkę?"
5. Click Cancel → note still in list
6. Click trash icon again → click OK → note disappears instantly
7. Refresh `/notes` → deleted note is gone permanently

## References

- Notes list component: `Components/Pages/Notes.razor`
- Service layer: `Services/NoteService.cs`
- Lucide interop pattern: `Components/Shared/WizardSection.razor` (OnAfterRenderAsync)
- CSS tokens: `wwwroot/css/app.css` (`:root` block — `--color-danger`, `--color-text-muted`, `--space-md`)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands.

### Phase 1: Service layer — DeleteNoteAsync

#### Automated

- [x] 1.1 Build passes
- [x] 1.2 All tests pass

### Phase 2: UI — trash icon, confirm dialog, in-place list update

#### Automated

- [ ] 2.1 Build passes
- [ ] 2.2 All tests pass

#### Manual

- [ ] 2.3 Trash icon appears on hover
- [ ] 2.4 Icon hidden when not hovering
- [ ] 2.5 Confirm dialog appears with Polish text
- [ ] 2.6 Cancel — note stays in list
- [ ] 2.7 Confirm — note disappears immediately
- [ ] 2.8 Refresh — deleted note gone permanently
