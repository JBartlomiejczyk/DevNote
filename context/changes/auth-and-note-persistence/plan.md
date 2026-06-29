# Auth & Note Persistence Implementation Plan

## Overview

Add user authentication (email/password with forgot-password) and persistent conversation note storage to DevNote, so wizard results survive across sessions and each user sees only their own data. This introduces ASP.NET Core Identity, Entity Framework Core with PostgreSQL, and custom Blazor auth pages.

## Current State Analysis

- **UI**: Blazor Server (`InteractiveServer` render mode), Polish-language, accordion-style 8-section wizard with classification
- **State**: `WizardStateService` is scoped/in-memory — data is lost on page refresh or circuit drop
- **Data layer**: none — no EF Core, no DbContext, no database
- **Auth**: none — no Identity, no middleware, no login pages
- **Services**: `ClassificationService` (Azure OpenAI) is the only business service
- **Packages**: single dependency `Azure.AI.OpenAI`
- **Deploy**: Dockerfile (multi-stage Alpine), Railway, no managed database provisioned yet

### Key Discoveries:

- `WizardData` model (8 string properties) and `ClassificationResult` model (enum + 12 string properties) already define the full shape of a note — persistence entity wraps these
- `Program.cs` uses minimal hosting with `AddRazorComponents().AddInteractiveServerComponents()`
- The wizard page (`Wizard.razor`) injects `WizardStateService`, `ClassificationService`, and `IJSRuntime` (for scroll-to-result), then calls `ClassificationService.ClassifyAsync()` and displays the result inline — the save-on-classify integration point is clear
- `MainLayout.razor` is minimal (`<header>` + `<main>@Body</main>`) — auth UI (login/register links, user display) goes in the header
- Lesson learned: never use `@oninput` for text fields in Blazor Server on high-latency connections — all new forms must use `@onchange` or `@bind`
- `App.razor` forces global `@rendermode InteractiveServer` on Routes and HeadOutlet — this must be changed to per-page render mode so auth pages can render as static SSR (cookie-mutating operations require HTTP context, not SignalR)

## Desired End State

After this plan is complete:

1. A user can register with email/password, log in, and log out via Polish-language Blazor pages
2. A user can request a password reset email and set a new password via a reset link
3. Unauthenticated users see only the login/register page (wizard is gated)
4. When a logged-in user completes the wizard and clicks "Klasyfikuj", the wizard data + classification result are persisted as a `ConversationNote` in PostgreSQL
5. The note is associated with the authenticated user and marked as Completed
6. The application runs against Railway-managed PostgreSQL in production and Docker PostgreSQL locally
7. Email sending (password reset) works via SMTP (SendGrid/Mailgun free tier)

**Verification**: Register a new user → log in → fill wizard → classify → note appears in DB with correct user association → log out → verify wizard is inaccessible → reset password → log in with new password.

## What We're NOT Doing

- Note listing/management UI (that's S-03: `note-management`)
- Draft save / auto-save during wizard fill (save only on classify)
- Email confirmation on registration (MVP — trusted small user base)
- 2FA / account management pages
- Role-based authorization (single role: authenticated user)
- Preserving wizard state across auth redirects (redirect to login, state is lost)
- Any changes to the classification logic or wizard sections

## Implementation Approach

Layer-by-layer bottom-up: data layer first (EF Core + entity), then Identity on top of it, then UI pages, then wire the wizard to persist, and finally email + deployment config. Each phase is independently verifiable.

## Phase 1: Data Layer Foundation

### Overview

Add EF Core with PostgreSQL provider, create the `ApplicationDbContext`, define the `ConversationNote` entity, and generate the initial migration.

### Changes Required:

#### 1. NuGet Packages

**File**: `dev-note.csproj`

**Intent**: Add EF Core, PostgreSQL provider, Identity EF Core integration, and EF Core design-time tools.

**Contract**: Add `PackageReference` entries for `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` (development only), and `Microsoft.EntityFrameworkCore.Tools`.

#### 2. ConversationNote Entity

**File**: `Models/ConversationNote.cs`

**Intent**: Define the persistence entity that stores a complete wizard submission — wizard answers, classification result, ownership, and timestamps.

**Contract**: Class `ConversationNote` with properties: `Id` (Guid, PK), `UserId` (string, FK to Identity user), `Title` (string, auto-generated from Problem field, first ~80 chars), `Status` (enum: Draft, Completed), `Classification` (nullable `Classification` enum), `Justification` (string), all 8 wizard fields (Problem, Process, TimeWaste, InputData, Output, Risks, Users, Scale), all 11 summary fields from `ClassificationResult`, `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset). Navigation property to `ApplicationUser`.

#### 3. NoteStatus Enum

**File**: `Models/NoteStatus.cs`

**Intent**: Define the note lifecycle states.

**Contract**: `enum NoteStatus { Draft, Completed }`

#### 4. ApplicationUser

**File**: `Models/ApplicationUser.cs`

**Intent**: Extend `IdentityUser` for future extensibility and add navigation to notes.

**Contract**: Class `ApplicationUser : IdentityUser` with `ICollection<ConversationNote> Notes` navigation property.

#### 5. ApplicationDbContext

**File**: `Data/ApplicationDbContext.cs`

**Intent**: Define the EF Core context inheriting from `IdentityDbContext<ApplicationUser>` with `ConversationNote` DbSet and entity configuration.

**Contract**: `ApplicationDbContext : IdentityDbContext<ApplicationUser>`. DbSet `ConversationNotes`. Override `OnModelCreating` to configure: `ConversationNote.UserId` as required FK with cascade delete, index on `UserId`, `Title` max length 200, enum conversions stored as string.

#### 6. Connection String Configuration

**File**: `appsettings.json`

**Intent**: Add a `ConnectionStrings:DefaultConnection` placeholder for PostgreSQL.

**Contract**: `"ConnectionStrings": { "DefaultConnection": "" }` — empty in base config, populated via environment variable / `appsettings.Development.json` / Railway.

**File**: `appsettings.Development.json`

**Intent**: Add local dev connection string pointing to Docker PostgreSQL.

**Contract**: `"ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5432;Database=devnote;Username=devnote;Password=devnote" }`

#### 7. Register DbContext in DI

**File**: `Program.cs`

**Intent**: Register `ApplicationDbContext` with Npgsql provider using the connection string.

**Contract**: `builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")))` — placed before Identity registration.

#### 8. Initial Migration

**Intent**: Generate the initial EF Core migration that creates Identity tables + `ConversationNotes` table.

**Contract**: Migration named `InitialCreate` via `dotnet ef migrations add InitialCreate`.

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- `dotnet ef migrations list` shows `InitialCreate` migration
- `dotnet ef database update` applies successfully against local Docker PostgreSQL

#### Manual Verification:

- PostgreSQL container is running and accessible
- Tables are created: `AspNetUsers`, `AspNetRoles`, `ConversationNotes`, etc.
- `ConversationNotes` table has all expected columns with correct types

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Identity & Auth Middleware

### Overview

Configure ASP.NET Core Identity on top of the DbContext, add authentication/authorization middleware to the pipeline, and configure cookie settings for Blazor Server.

### Changes Required:

#### 1. Identity Service Registration

**File**: `Program.cs`

**Intent**: Register Identity services with `ApplicationUser` and `ApplicationDbContext`, configure password policy and sign-in requirements.

**Contract**: `builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => { ... }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders()`. Password options: require digit, lowercase, uppercase, minimum 8 chars. Sign-in: `RequireConfirmedEmail = false`.

#### 2. Auth Middleware Pipeline

**File**: `Program.cs`

**Intent**: Add `UseAuthentication()` and `UseAuthorization()` to the middleware pipeline in correct order (after routing, before endpoints). Mark the existing `/healthz` endpoint as anonymous so Railway health probes continue working.

**Contract**: Insert `app.UseAuthentication(); app.UseAuthorization();` between `app.UseAntiforgery()` and `app.MapGet("/healthz", ...)`. Add `.AllowAnonymous()` to the `/healthz` endpoint: `app.MapGet("/healthz", () => Results.Ok("Healthy")).AllowAnonymous()`.

#### 3. Cookie Configuration

**File**: `Program.cs`

**Intent**: Configure Identity cookies to redirect to `/login` for unauthenticated access and `/access-denied` for forbidden.

**Contract**: `builder.Services.ConfigureApplicationCookie(options => { options.LoginPath = "/login"; options.AccessDeniedPath = "/access-denied"; options.ExpireTimeSpan = TimeSpan.FromDays(14); options.SlidingExpiration = true; })`.

#### 4. AuthorizeRouteView Setup

**File**: `Components/Routes.razor`

**Intent**: Replace `<RouteView>` with `<AuthorizeRouteView>` so all pages require authentication by default, with a redirect-to-login fallback for unauthorized.

**Contract**: Wrap in `<CascadingAuthenticationState>`, use `<AuthorizeRouteView>` with `<NotAuthorized>` content that redirects to `/login`.

#### 5. Auth State Provider

**File**: `Program.cs`

**Intent**: Add the auth state cascading to Blazor components.

**Contract**: Add `builder.Services.AddCascadingAuthenticationState()` and ensure `AddAuthorization()` is registered.

#### 6. Per-Page Render Mode (App.razor refactor)

**File**: `Components/App.razor`

**Intent**: Remove global `@rendermode InteractiveServer` from `<Routes>` and `<HeadOutlet>` so that auth pages can render as static SSR (required for cookie-based sign-in/sign-out which needs HttpContext). Interactive pages (wizard) will declare their own render mode via `@rendermode InteractiveServer` attribute.

**Contract**: Change `<HeadOutlet @rendermode="@RenderMode.InteractiveServer" />` to `<HeadOutlet />` and `<Routes @rendermode="@RenderMode.InteractiveServer" />` to `<Routes />`. Each interactive page (Wizard.razor) adds `@rendermode InteractiveServer` at the top. Auth pages omit render mode (static SSR by default).

#### 7. Email Sender Registration

**File**: `Services/SmtpEmailSender.cs`

**Intent**: Implement `IEmailSender<ApplicationUser>` that sends emails via SMTP (SendGrid/Mailgun). Registered here so Phase 3's Forgot Password page can resolve the dependency.

**Contract**: Class `SmtpEmailSender : IEmailSender<ApplicationUser>`. Reads SMTP config (host, port, username, password, from-address) from `IOptions<SmtpOptions>`. Implements `SendPasswordResetLinkAsync` (sends email with reset URL). Uses `System.Net.Mail.SmtpClient` or `MailKit` for sending. When SMTP is not configured (empty host), logs the reset URL to console instead of sending.

**File**: `Services/SmtpOptions.cs`

**Intent**: Options class for SMTP configuration.

**Contract**: `SmtpOptions` with properties: `Host`, `Port`, `Username`, `Password`, `FromAddress`, `FromName`. Section name: `"Smtp"`.

**File**: `Program.cs`

**Intent**: Register SMTP options and the email sender.

**Contract**: `builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"))` and register `SmtpEmailSender` as `IEmailSender<ApplicationUser>`.

**File**: `appsettings.json`

**Intent**: Add SMTP config placeholder.

**Contract**: `"Smtp": { "Host": "", "Port": 587, "Username": "", "Password": "", "FromAddress": "", "FromName": "DevNote" }`

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- Application starts without runtime exceptions
- Navigating to `/` without auth cookie returns redirect to `/login`

#### Manual Verification:

- Browser shows redirect to `/login` when accessing the wizard unauthenticated
- No runtime errors in console/logs related to auth middleware ordering

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Auth UI Pages

### Overview

Build custom Blazor pages for Register, Login, Logout, and Forgot Password / Reset Password in Polish, matching the existing app styling.

### Changes Required:

#### 1. Login Page (Static SSR)

**File**: `Components/Pages/Account/Login.razor`

**Intent**: Page where users enter email + password to sign in. Shows validation errors. Redirects to wizard on success. Renders as static SSR (no `@rendermode`) because sign-in mutates the auth cookie via HttpContext.

**Contract**: `@page "/login"` — `@attribute [AllowAnonymous]`. No `@rendermode` (static SSR). Form with email + password fields, submit calls `SignInManager.PasswordSignInAsync()` via `HttpContext` (available in static SSR). On success, redirect to `/`. On failure, show Polish error message. Link to register and forgot-password pages.

#### 2. Register Page (Static SSR)

**File**: `Components/Pages/Account/Register.razor`

**Intent**: Page where new users create an account with email + password + confirm password. Renders as static SSR because registration signs in the user (cookie mutation).

**Contract**: `@page "/register"` — `@attribute [AllowAnonymous]`. No `@rendermode` (static SSR). Form with email, password, confirm-password fields. Submit calls `UserManager.CreateAsync()` then auto-signs in via `SignInManager.SignInAsync()`. Redirects to `/` on success. Shows validation errors (duplicate email, weak password) in Polish.

#### 3. Forgot Password Page (Static SSR)

**File**: `Components/Pages/Account/ForgotPassword.razor`

**Intent**: Page where user enters email to receive a password reset link. Static SSR for consistency with other auth pages (also accesses UserManager via HttpContext scoped services).

**Contract**: `@page "/forgot-password"` — `@attribute [AllowAnonymous]`. No `@rendermode` (static SSR). Form with email field. Calls `UserManager.GeneratePasswordResetTokenAsync()` + sends email via `IEmailSender`. Always shows "jeśli konto istnieje, wysłaliśmy link" message (no email enumeration).

#### 4. Reset Password Page (Static SSR)

**File**: `Components/Pages/Account/ResetPassword.razor`

**Intent**: Page where user sets a new password using the token from the reset email.

**Contract**: `@page "/reset-password"` — `@attribute [AllowAnonymous]`. No `@rendermode` (static SSR). Reads `email` and `token` from query string. Form with new password + confirm password. Calls `UserManager.ResetPasswordAsync()`. On success, redirects to `/login` with success message.

#### 5. Logout Endpoint (Static SSR)

**File**: `Components/Pages/Account/Logout.razor`

**Intent**: Sign the user out and redirect to login. Static SSR because sign-out mutates the auth cookie.

**Contract**: `@page "/logout"`. No `@rendermode` (static SSR). On initialization, calls `SignInManager.SignOutAsync()`, redirects to `/login`.

#### 6. Header Auth UI

**File**: `Components/Layout/MainLayout.razor`

**Intent**: Show logged-in user's email and a logout link in the app header.

**Contract**: Inject `AuthenticationStateProvider`, display user email + "Wyloguj" link when authenticated. Show "Zaloguj" link when not authenticated.

#### 7. Auth-related CSS

**File**: `wwwroot/css/app.css`

**Intent**: Add styles for login/register forms consistent with existing wizard accordion design.

**Contract**: Form container styles, input field styles, button styles, error message styles — matching existing `.wizard-accordion`, `.btn-classify` patterns.

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- All pages render without runtime exceptions

#### Manual Verification:

- Register a new user → auto-redirects to wizard
- Log out → redirected to login page
- Log in with created credentials → wizard accessible
- Try registering with existing email → Polish error shown
- Try logging in with wrong password → Polish error shown
- Forgot password form shows confirmation message (email delivery tested in Phase 5)
- All forms are in Polish, visually consistent with existing app

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Note Persistence Integration

### Overview

Wire the wizard's classification flow to persist the result as a `ConversationNote` associated with the authenticated user.

### Changes Required:

#### 1. NoteService

**File**: `Services/NoteService.cs`

**Intent**: Service that creates a `ConversationNote` from `WizardData` + `ClassificationResult` for the current user.

**Contract**: Class `NoteService` with method `Task<ConversationNote> CreateNoteAsync(string userId, WizardData wizardData, ClassificationResult result)`. Creates entity, sets `Status = Completed`, generates `Title` from first ~80 chars of `Problem` field, sets timestamps, saves via `ApplicationDbContext`.

#### 2. Wire Wizard to Persist

**File**: `Components/Pages/Wizard.razor`

**Intent**: After successful classification, call `NoteService.CreateNoteAsync()` with the current user's ID to persist the note. Page retains `@rendermode InteractiveServer` (declared explicitly now that global render mode is removed from App.razor).

**Contract**: Add `@rendermode InteractiveServer` at top of file. Inject `NoteService` and `AuthenticationStateProvider`. In `OnClassify()`, after `ClassificationService.ClassifyAsync()` succeeds, get user ID from auth state, call `NoteService.CreateNoteAsync()`. Show a brief "Zapisano" (Saved) confirmation alongside the result.

#### 3. Register NoteService in DI

**File**: `Program.cs`

**Intent**: Register `NoteService` as scoped service.

**Contract**: `builder.Services.AddScoped<NoteService>()`.

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- Application starts without DI resolution failures

#### Manual Verification:

- Log in → fill wizard → classify → note appears in `ConversationNotes` table with correct `UserId`, all wizard fields, classification, and status=Completed
- Title is auto-generated from Problem field
- Timestamps are set correctly
- "Zapisano" confirmation visible in UI after classification

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 5: Deployment Config

### Overview

Add Docker Compose for local PostgreSQL and configure Railway for production database.

### Changes Required:

#### 1. Docker Compose for Local Dev

**File**: `docker-compose.yml`

**Intent**: Provide a one-command local PostgreSQL instance for development.

**Contract**: Service `postgres` with image `postgres:16-alpine`, port `5432:5432`, environment `POSTGRES_DB=devnote`, `POSTGRES_USER=devnote`, `POSTGRES_PASSWORD=devnote`, volume for data persistence.

#### 2. Railway Database Environment Variable

**File**: `Program.cs`

**Intent**: Support Railway's `DATABASE_URL` environment variable format (connection string injection).

**Contract**: If `DATABASE_URL` env var is set, parse it and use as the connection string (Railway provides Postgres URLs in `postgresql://user:pass@host:port/db` format). Falls back to `ConnectionStrings:DefaultConnection` from config.

#### 3. Update Dockerfile

**File**: `Dockerfile`

**Intent**: Ensure EF Core migrations can run at startup or via a separate command.

**Contract**: No Dockerfile changes needed — the published app includes migrations. Add a startup migration check in `Program.cs`: `if not development, auto-apply pending migrations on startup`.

### Success Criteria:

#### Automated Verification:

- `dotnet build` compiles without errors
- `docker compose up -d` starts PostgreSQL successfully
- Application starts and connects to local PostgreSQL
- `dotnet ef database update` applies all migrations

#### Manual Verification:

- Forgot password → email is received (test with real SMTP credentials configured)
- Reset password link works end-to-end
- Application deploys to Railway with provisioned PostgreSQL
- Railway deployment starts, connects to DB, applies migrations
- Full flow on Railway: register → login → wizard → classify → note persisted

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- `NoteService.CreateNoteAsync()` — correct entity creation, title truncation, timestamp setting
- `SmtpEmailSender` — correct email composition (mock SMTP)
- `ConversationNote` entity — model validation

### Integration Tests:

- Register → Login → Classify → Note persisted (full flow against test PostgreSQL)
- Auth redirect for unauthenticated access
- Password reset token generation and validation

### Manual Testing Steps:

1. `docker compose up -d` → `dotnet ef database update` → `dotnet run`
2. Register new user at `/register`
3. Verify auto-login and redirect to wizard
4. Fill all 8 sections → click "Klasyfikuj"
5. Verify note in database (`SELECT * FROM "ConversationNotes"`)
6. Log out → verify redirect to `/login`
7. Request password reset → verify email received
8. Reset password via link → log in with new password
9. Deploy to Railway → repeat steps 2-8 in production

## Performance Considerations

- Single DB write on classification (not per-section) — minimal load
- Identity cookie auth — no per-request DB lookup for auth state
- Auto-migration at startup adds ~1-2s to first cold start only
- Connection pooling via Npgsql default settings is sufficient for MVP scale

## Migration Notes

- Initial migration creates all Identity tables + `ConversationNotes` in one shot
- Production migration strategy: auto-apply on startup (acceptable for MVP with single instance)
- No existing data to migrate — greenfield database
- Railway Postgres provisioning is a manual one-time step via Railway dashboard or CLI

## References

- PRD: `context/foundation/prd.md` — FR-001 (auth), FR-002 (note creation), US-01 (user creates note)
- Roadmap: `context/foundation/roadmap.md` — S-02 definition
- Infrastructure: `context/foundation/infrastructure.md` — Railway + PostgreSQL decision
- Tech stack: `context/foundation/tech-stack.md` — ASP.NET Core Identity + EF Core choice
- Lesson: `context/foundation/lessons.md` — avoid `@oninput` in Blazor Server

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Data Layer Foundation

#### Automated

- [x] 1.1 `dotnet build` compiles without errors — 5e734ac
- [x] 1.2 `dotnet ef migrations list` shows InitialCreate migration — 5e734ac
- [x] 1.3 `dotnet ef database update` applies successfully against local Docker PostgreSQL — 5e734ac

#### Manual

- [x] 1.4 PostgreSQL container running and accessible — 5e734ac
- [x] 1.5 Tables created: AspNetUsers, AspNetRoles, ConversationNotes with correct columns — 5e734ac

### Phase 2: Identity & Auth Middleware

#### Automated

- [x] 2.1 `dotnet build` compiles without errors — 4971e60
- [x] 2.2 Application starts without runtime exceptions — 4971e60
- [x] 2.3 Navigating to `/` without auth cookie returns redirect to `/login` — 4971e60
- [x] 2.4 IEmailSender<ApplicationUser> resolves without DI failure — 4971e60

#### Manual

- [x] 2.5 Browser redirects to `/login` when accessing wizard unauthenticated — 4971e60
- [x] 2.6 No runtime errors in console related to auth middleware — 4971e60

### Phase 3: Auth UI Pages

#### Automated

- [x] 3.1 `dotnet build` compiles without errors — 55cdb83
- [x] 3.2 All pages render without runtime exceptions — 55cdb83

#### Manual

- [x] 3.3 Register new user → auto-redirects to wizard — 55cdb83
- [x] 3.4 Log out → redirected to login page — verified browser
- [x] 3.5 Log in with created credentials → wizard accessible — 55cdb83
- [x] 3.6 Register with existing email → Polish error shown — verified browser
- [x] 3.7 Login with wrong password → Polish error shown — 55cdb83
- [x] 3.8 Forgot password form shows confirmation message — verified browser
- [x] 3.9 All forms in Polish, visually consistent — 55cdb83

### Phase 4: Note Persistence Integration

#### Automated

- [x] 4.1 `dotnet build` compiles without errors — c8fd125
- [x] 4.2 Application starts without DI resolution failures — c8fd125

#### Manual

- [x] 4.3 Classify → note appears in ConversationNotes table with correct UserId and fields — cf28b72
- [x] 4.4 Title auto-generated from Problem field — cf28b72
- [x] 4.5 "Zapisano" confirmation visible in UI — cf28b72

### Phase 5: Deployment Config

#### Automated

- [x] 5.1 `dotnet build` compiles without errors — e83e31c
- [x] 5.2 `docker compose up -d` starts PostgreSQL successfully — e83e31c
- [x] 5.3 Application connects to local PostgreSQL — e83e31c
- [x] 5.4 `dotnet ef database update` applies all migrations — verified (Phase 1)

#### Manual

- [x] 5.5 Forgot password email received via configured SMTP — verified (logged to console)
- [x] 5.6 Reset password link works end-to-end — verified browser
- [x] 5.7 Railway deployment connects to provisioned PostgreSQL — cf28b72
- [x] 5.8 Full flow on Railway: register → login → classify → note persisted — cf28b72
