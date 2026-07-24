# Visual Layer Redesign — Developer Notebook Aesthetic

## Overview

Przebudowa warstwy wizualnej DevNote z generycznego SaaS-starter na spójny "developer notebook" aesthetic oparty na palecie **Catppuccin Mocha** (dark theme), fontach **Inter + JetBrains Mono** oraz ikonach **Lucide**. Zmiana obejmuje wyłącznie warstwę UI — żadnych zmian w logice biznesowej, serwisach, modelach ani testach jednostkowych.

## Current State Analysis

Aktualny system wizualny:
- **Framework:** zero — 100% ręczny CSS, 627 linii, plik `wwwroot/css/app.css`
- **Kolory:** `#1a1a2e` navy + `#4a6cf7` niebieski — typowe kolory SaaS starter template, brak tożsamości
- **Fonty:** system font stack (`-apple-system, BlinkMacSystemFont, "Segoe UI"`), brak web fontów
- **Ikony:** brak biblioteki ikon; strzałki w `WizardSection` to znaki Unicode `▶`/`▼`
- **Layout:** flat `<header class="app-header">` + `<main class="app-main" style="max-width:800px">` — brak sidebara, brak nawigacji bocznej
- **Dark mode:** brak (jasny motyw)
- **Animacje:** wyłącznie `transform: translateY(-1px)` na hover przycisków

### Key Discoveries:

- `wwwroot/css/app.css` to jeden plik z całym systemem wizualnym — wystarczy przepisać go, aby zmienić cały wygląd
- `Components/App.razor` nie ma żadnych CDN linków — to miejsce na dodanie fontów i Lucide
- `Components/Layout/MainLayout.razor` ma 29 linii — prosta restrukturyzacja do układu z sidebarem
- `Components/Shared/WizardSection.razor` — ikony `▶`/`▼` w linii 5 jako tekst w `<span class="wizard-section-indicator">`
- Wszystkie nazwy klas CSS są semantyczne i konsekwentne (BEM-like) — nie trzeba zmieniać markup w stronach
- `wwwroot/js/app.js` ma 8 linii — tu dojdzie inicjalizacja Lucide
- Lucide w Blazor Server wymaga wywołania `lucide.createIcons()` po każdym re-renderze komponentu zawierającego ikony — `WizardSection` (toggle chevronów) potrzebuje `IJSRuntime` + `OnAfterRenderAsync`

## Desired End State

Aplikacja wygląda jak narzędzie developerskie z "notatnikowym" charakterem:
- Ciemne tło `#1e1e2e` (Catppuccin Mocha Base) z pastelowymi akcentami
- Sidebar po lewej stronie z logo i nawigacją
- Monospace font (JetBrains Mono) w nagłówkach, badge'ach i elementach "technicznych"; Inter dla treści
- Ikony Lucide zastępują znaki Unicode
- Subtelne fade-in animacje na kartach i smooth transitions na interakcjach

**Weryfikacja:** App wygląda kompletnie inaczej — dark, narzędziowy wygląd — a wszystkie 37 testów przechodzi bez zmian.

### Key Discoveries:

- `badge-a/b/c` klasy są współdzielone między `Result.razor`, `Notes.razor` i `WizardSection.razor` — zmiana w CSS obejmuje wszystkie trzy miejsca
- `app.css` używa wartości hardcoded hex; po redesignie wszystkie kolory przejdą na CSS custom properties (`var(--ctp-*)`)
- Lucide CDN: `https://unpkg.com/lucide@latest/dist/umd/lucide.min.js` — musi być załadowany przed `blazor.web.js`

## What We're NOT Doing

- Zmiany w logice klasyfikacji, serwisach, modelach, testach, migracji bazy
- Dodawanie trybu jasnego (light mode) — dark theme jest stały
- Pełny sidebar z listą sekcji wizarda po lewej stronie
- Animacje wymagające JS interop poza Lucide (slide-in, skeleton loaders)
- Responsywność poniżej 480px (poza istniejącym breakpointem 600px)
- Zmiana estructury stron (Pages/) poza `WizardSection.razor` i `MainLayout.razor`

## Implementation Approach

Cztery fazy w naturalnej kolejności zależności: najpierw tokeny i zasoby zewnętrzne (fonty, ikony), potem layout (sidebar), potem stylowanie wszystkich komponentów nowym CSS, na końcu drobne poprawki w markup'ie komponentów (ikony chevronów).

## Phase 1: Design Tokens & External Assets

### Overview

Ustanowienie systemu design tokens (CSS custom properties z paletą Catppuccin Mocha) oraz załadowanie fontów Inter + JetBrains Mono i Lucide Icons.

### Changes Required:

#### 1. CDN links — fonty i ikony

**File**: `Components/App.razor`

**Intent**: Dodaj linki do Google Fonts (Inter + JetBrains Mono) oraz skrypt Lucide Icons CDN przed `blazor.web.js`. Fonty muszą być załadowane przed CSS aby uniknąć FOUT.

**Contract**: W sekcji `<head>`, po `<base href="/" />` a przed `<link rel="stylesheet" href="css/app.css" />`, dodaj:
```html
<link rel="preconnect" href="https://fonts.googleapis.com" />
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@400;600&display=swap" rel="stylesheet" />
```
Lucide script (`https://unpkg.com/lucide@latest/dist/umd/lucide.min.js`) dodaj w `<body>` **przed** `<script src="js/app.js"></script>`.

#### 2. Lucide initialization

**File**: `wwwroot/js/app.js`

**Intent**: Inicjalizuj Lucide po załadowaniu DOM oraz ponownie po każdej nawigacji Blazor (Blazor Server nie przeładowuje strony przy nawigacji — ikony w nowo wyrenderowanych komponentach nie byłyby przetworzone bez tego).

**Contract**: Istniejąca funkcja `scrollToResultAnchor` zostaje bez zmian. Dodaj inicjalizację Lucide po niej:
```javascript
function initLucide() {
    if (window.lucide) window.lucide.createIcons();
}
document.addEventListener('DOMContentLoaded', initLucide);
document.addEventListener('blazor:navigated', initLucide);
```

#### 3. CSS design tokens — paleta Catppuccin Mocha

**File**: `wwwroot/css/app.css`

**Intent**: Zastąp wszystkie hardcoded wartości hex CSS custom properties. Cały system kolorów, typografia i spacing definiowane w bloku `:root` na początku pliku.

**Contract**: Na początku pliku, przed obecnym blokiem `*, *::before, *::after`, dodaj blok `:root` zawierający:
- Kolory bazowe Catppuccin Mocha: `--ctp-base: #1e1e2e`, `--ctp-mantle: #181825`, `--ctp-crust: #11111b`, `--ctp-surface0: #313244`, `--ctp-surface1: #45475a`, `--ctp-surface2: #585b70`, `--ctp-overlay0: #6c7086`, `--ctp-subtext0: #a6adc8`, `--ctp-subtext1: #bac2de`, `--ctp-text: #cdd6f4`, `--ctp-mauve: #cba6f7`, `--ctp-lavender: #b4befe`, `--ctp-blue: #89b4fa`, `--ctp-green: #a6e3a1`, `--ctp-yellow: #f9e2af`, `--ctp-red: #f38ba8`, `--ctp-peach: #fab387`
- Tokeny semantyczne: `--color-bg`, `--color-bg-raised`, `--color-bg-subtle`, `--color-bg-interactive`, `--color-border`, `--color-border-focus`, `--color-text`, `--color-text-muted`, `--color-text-subtle`, `--color-accent`, `--color-accent-hover`, `--color-link`, `--color-success`, `--color-warning`, `--color-error`
- Typografia: `--font-body: 'Inter', -apple-system, sans-serif`, `--font-mono: 'JetBrains Mono', 'Fira Code', monospace`
- Layout: `--sidebar-width: 220px`
- Border radius: `--radius-sm: 6px`, `--radius-md: 8px`, `--radius-lg: 12px`
- Shadows z ciemnym tłem: `--shadow-sm`, `--shadow-md`, `--shadow-glow-accent` (glow ring wokół focused elementów)
- Animacje: `--transition-fast: 150ms ease`, `--transition-base: 200ms ease`

Zaktualizuj `body` aby używał `--color-bg` jako tło i `--color-text` jako kolor tekstu, a `--font-body` jako font-family.

### Success Criteria:

#### Automated Verification:

- Build przechodzi: `dotnet build`
- Wszystkie 37 testów przechodzą: `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual Verification:

- Strona ładuje się z ciemnym tłem `#1e1e2e` (widoczne w DevTools)
- Font Inter widoczny w sekcjach treści (DevTools → Computed → font-family)
- JetBrains Mono widoczny dla elementów monospacowych (nagłówki, kod)
- W DevTools Console brak błędów 404 dla CDN zasobów

**Implementation Note**: Po przejściu automated verification, poczekaj na manual confirmation przed przejściem do Phase 2.

---

## Phase 2: Sidebar Layout Restructure

### Overview

Przeprojektowanie `MainLayout.razor` z flat `<header>` + `<main>` na układ dwukolumnowy: sidebar po lewej + area treści po prawej. Sidebar zawiera logo z ikoną, nawigację i informacje o użytkowniku.

### Changes Required:

#### 1. MainLayout restructure

**File**: `Components/Layout/MainLayout.razor`

**Intent**: Zastąp obecny `<header>` + `<main>` strukturą `<div class="app-layout">` z `<aside class="app-sidebar">` i `<main class="app-main">`. Sidebar zawiera logo (ikona Lucide `notebook-pen` + tekst "DevNote"), nawigację (`Nowa notatka`, `Moje notatki` z Lucide ikonami), oraz footer z nazwą użytkownika i przyciskiem wylogowania. Autoryzowane i nieautoryzowane stany sidebara różnią się dostępnymi linkami.

**Contract**:
Nowa struktura HTML (Lucide ikony przez `<i data-lucide="[name]">`):
- Logo: `notebook-pen` icon
- "Nowa notatka": `plus-circle` icon  
- "Moje notatki": `book-open` icon
- "Zaloguj": `log-in` icon
- "Wyloguj": `log-out` icon
Klasy CSS: `.app-layout` (grid container), `.app-sidebar`, `.sidebar-logo`, `.sidebar-logo-text`, `.sidebar-nav`, `.sidebar-nav-item`, `.sidebar-footer`, `.sidebar-user`, `.sidebar-logout`.
`<main class="app-main">` nie zmienia swojej roli — nadal renderuje `@Body`.

#### 2. Sidebar & layout styles

**File**: `wwwroot/css/app.css`

**Intent**: Zdefiniuj nowy system layoutu oparty na CSS Grid. Sidebar ma stałą szerokość `--sidebar-width`, main area zajmuje resztę. Sidebar ma ciemniejsze tło niż main (`--color-bg-subtle`), separację obramowaniem. Nav items mają hover state i aktywny state. Na małych ekranach (≤768px) sidebar zmienia się w horizontal top bar.

**Contract**:
- `.app-layout`: `display: grid; grid-template-columns: var(--sidebar-width) 1fr; min-height: 100vh`
- `.app-sidebar`: `background: var(--color-bg-subtle); border-right: 1px solid var(--color-border); display: flex; flex-direction: column; padding: 1.5rem 0`
- `.sidebar-logo`: `display: flex; align-items: center; gap: 0.625rem; padding: 0 1.25rem 1.5rem; font-family: var(--font-mono); font-weight: 600; font-size: 1.1rem; color: var(--color-text)` — ikona w kolorze `--color-accent`
- `.sidebar-nav-item`: flex row z ikoną, padding 0.625rem 1.25rem, hover background `var(--color-bg-interactive)`, color `var(--color-text-muted)`, hover color `var(--color-text)`, transition `var(--transition-fast)`
- `.sidebar-footer`: margin-top auto, padding top 1rem, border-top 1px solid `var(--color-border)`, padding-left 1.25rem
- `.app-main`: `max-width: 800px; margin: 2rem auto; padding: 0 1.5rem` — bez zmian względem poprzedniego
- Breakpoint `@media (max-width: 768px)`: `.app-layout` zmienia się na single-column; `.app-sidebar` staje się horizontal bar (flex-direction row, height auto, border-right none, border-bottom 1px solid)

### Success Criteria:

#### Automated Verification:

- Build przechodzi: `dotnet build`
- Wszystkie testy przechodzą: `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual Verification:

- Sidebar widoczny na ekranie ≥769px z logo, nawigacją, użytkownikiem
- Na szerokości ≤768px sidebar staje się poziomym topbarem
- Lucide ikony w sidebarze renderują jako SVG (nie tekst Unicode)
- Linki nawigacyjne działają (Nowa notatka → `/`, Moje notatki → `/notes`)
- Wylogowanie działa przez form POST

**Implementation Note**: Po przejściu automated verification, poczekaj na manual confirmation przed przejściem do Phase 3.

---

## Phase 3: Full CSS Restyling

### Overview

Kompleksowe przepisanie `app.css` — wszystkie istniejące klasy komponentów (wizard, result, notes, auth, buttons, badges) w nowej palecie Catppuccin Mocha z użyciem CSS custom properties. Dodanie fade-in animacji na kartach.

### Changes Required:

#### 1. Kompletne przepisanie app.css

**File**: `wwwroot/css/app.css`

**Intent**: Zastąp wszystkie hardcoded wartości hex w każdej regule CSS odpowiadającymi tokenami `var(--ctp-*)` lub `var(--color-*)`. Ciemne tło, pastelowe akcenty, wyraźny kontrast tekstu. Dodaj `@keyframes fade-in` dla subtelnych animacji wejścia kart.

**Contract**: Każda sekcja CSS musi używać wyłącznie custom properties (bez hardcoded hex poza blokiem `:root`). Kluczowe zmiany per komponent:

**Wizard:**
- `.wizard-section`: `background: var(--color-bg-raised); border-color: var(--color-border)` — ciemna karta na ciemnym tle
- `.wizard-section-header:hover`: `background-color: var(--color-bg-interactive)` 
- `.wizard-section-textarea`: `background: var(--color-bg-subtle); border-color: var(--color-border); color: var(--color-text)` — ciemny textarea
- `.wizard-section-textarea:focus`: glow ring `var(--shadow-glow-accent)`
- `.wizard-helper-questions`: `background: var(--color-bg-subtle); border-color: var(--color-border-subtle)`
- `.wizard-section-indicator` zmienia color na `var(--color-accent)`

**Buttons:**
- `.btn-classify`, `.btn-auth`, `.btn-new-note`: `background-color: var(--color-accent); color: var(--ctp-base)` — ciemny tekst na pastelowym przycisku
- `.btn-classify:hover`: `background-color: var(--color-accent-hover)`
- `.btn-helper-refresh`: `border-color: var(--color-accent); color: var(--color-accent); background: transparent`

**Result panel:**
- `.result-panel`: `background: var(--color-bg-raised); border-color: var(--color-border); box-shadow: var(--shadow-md)`
- `.result-field h4`: `color: var(--color-text-subtle); font-family: var(--font-mono); font-size: 0.75rem; letter-spacing: 0.06em` — monospace labele pól jak komentarze w kodzie
- `.result-field p`: `color: var(--color-text)`
- `.classification-badge`: rozmiar 2.75rem, `font-family: var(--font-mono); font-weight: 600`
- `.badge-a`: `background-color: rgba(166, 227, 161, 0.2); color: var(--ctp-green); border: 1px solid var(--ctp-green)`
- `.badge-b`: `background-color: rgba(249, 226, 175, 0.2); color: var(--ctp-yellow); border: 1px solid var(--ctp-yellow)`
- `.badge-c`: `background-color: rgba(243, 139, 168, 0.2); color: var(--ctp-red); border: 1px solid var(--ctp-red)`

**Notes list:**
- `.notes-item`: `background: var(--color-bg-raised); border-color: var(--color-border); animation: fade-in 200ms var(--transition-base) both`
- `.notes-item:hover`: `border-color: var(--color-accent); box-shadow: var(--shadow-md)`
- `.notes-title`: `color: var(--color-text); font-weight: 600`
- `.notes-date`: `color: var(--color-text-muted); font-family: var(--font-mono); font-size: 0.8rem`
- `.notes-status--completed`: pastelowe green (jak badge-a)
- `.notes-status--draft`: `background: var(--color-surface0); color: var(--color-text-muted)`
- `.notes-empty`: `border-color: var(--color-border); background: var(--color-bg-raised)` — dashed border zostaje

**Auth forms:**
- `.auth-form-container`: `background: var(--color-bg-raised); border-color: var(--color-border); box-shadow: var(--shadow-md)` — centrowana karta logowania na ciemnym tle
- `.auth-field input`: `background: var(--color-bg-subtle); border-color: var(--color-border); color: var(--color-text)`
- `.auth-error`: `background: rgba(243, 139, 168, 0.1); border-color: var(--ctp-red); color: var(--ctp-red)`
- `.auth-success`: `background: rgba(166, 227, 161, 0.1); border-color: var(--ctp-green); color: var(--ctp-green)`

**Error banner:**
- `.error-banner`: dostosowanie do palety error (ctp-red na ciemnym tle)

**Animations:**
```css
@keyframes fade-in {
    from { opacity: 0; transform: translateY(4px); }
    to   { opacity: 1; transform: translateY(0); }
}
```
Zastosować na: `.notes-item`, `.result-panel`, `.auth-form-container`, `.wizard-section.expanded .wizard-section-body`

**h2 global**: `color: var(--color-text); font-family: var(--font-mono)`

### Success Criteria:

#### Automated Verification:

- Build przechodzi: `dotnet build`
- Wszystkie testy przechodzą: `dotnet test DevNote.Tests/DevNote.Tests.csproj`
- Format nie wymaga zmian: `dotnet format --verify-no-changes --no-restore`

#### Manual Verification:

- Cała aplikacja ma ciemne tło — żaden element nie ma jasnego tła (#fff lub podobnego)
- Kolor tekstu jest czytelny na ciemnym tle (WCAG AA: min 4.5:1 dla tekstu normalnego)
- Badges A/B/C w notes list i result panel mają pastelowe obramowanie i tło
- Result panel: labele pól (`Problem`, `Użytkownicy` itp.) wyglądają jak komentarze kodu (monospace, muted color, uppercase)
- Hover na notes-item zmienia border na accentowy kolor (mauve)
- Fade-in animacja widoczna przy pierwszym załadowaniu notes list
- Auth form (login/register) wygląda spójnie z dark theme — ciemna karta na ciemnym tle

**Implementation Note**: Po przejściu automated verification, poczekaj na manual confirmation przed przejściem do Phase 4.

---

## Phase 4: WizardSection Icon & Result Polish

### Overview

Zastąpienie znaków Unicode `▶`/`▼` ikonami Lucide w `WizardSection.razor`. Dodanie `IJSRuntime` dla ponownej inicjalizacji ikon po każdym toggle. Drobny polishing Result.razor.

### Changes Required:

#### 1. Replace Unicode arrows with Lucide icons

**File**: `Components/Shared/WizardSection.razor`

**Intent**: Zastąp `<span class="wizard-section-indicator">@(IsExpanded ? "▼" : "▶")</span>` ikoną Lucide chevron. Ponieważ Blazor re-renderuje komponent przy każdym toggle i zastępuje SVG z powrotem na `<i>`, dodaj `IJSRuntime` injection i wywołaj `lucide.createIcons()` w `OnAfterRenderAsync`.

**Contract**:
- W sekcji `@code`: dodaj `@inject IJSRuntime JS`
- Zmień span indicator na: `<i data-lucide="@(IsExpanded ? "chevron-down" : "chevron-right")" class="wizard-section-indicator"></i>`
- Dodaj override w `@code`:
```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    await JS.InvokeVoidAsync("lucide.createIcons");
}
```
- CSS dla `.wizard-section-indicator`: `width: 1rem; height: 1rem; color: var(--color-accent); flex-shrink: 0` (ikona SVG jest `<svg>`, nie `<span>` — `font-size` nie działa, używaj `width`/`height`)

#### 2. Result panel — helper icon (opcjonalnie)

**File**: `Components/Pages/Result.razor`

**Intent**: Dodaj małą ikonę Lucide (`clipboard-check`) przy tytule classification-label dla wzmocnienia wizualnego charakteru.

**Contract**: W `.result-header`, przed `<span class="classification-label">`, dodaj `<i data-lucide="clipboard-check" class="result-header-icon"></i>`. CSS: `color: var(--color-text-muted); width: 1.25rem; height: 1.25rem`. Opcjonalne — pomiń jeśli zbytnio komplikuje markup.

### Success Criteria:

#### Automated Verification:

- Build przechodzi: `dotnet build`
- Wszystkie testy przechodzą: `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual Verification:

- Ikona chevron w WizardSection zmienia się przy toggle (down/right) i renderuje jako SVG (nie tekst)
- Po wielokrotnym kliknięciu expand/collapse ikona zawsze wyświetla się poprawnie (nie pozostaje `<i>` bez SVG)
- Wizard nadal działa funkcjonalnie (toggle, wpisywanie tekstu, helper questions)
- Klasyfikacja i generowanie podsumowania działają end-to-end

**Implementation Note**: Po przejściu automated verification, wykonaj manual testing całego flow (create note → wizard → classify → result → notes list) przed uznaniem zmiany za ukończoną.

---

## Testing Strategy

### Unit Tests:

Brak nowych testów jednostkowych — zmiana jest wyłącznie wizualna. Istniejące 37 testów musi przechodzić bez modyfikacji.

### Integration Tests:

Istniejące testy integracyjne w `DevNote.Tests/` muszą przechodzić — nie dotykają CSS ani Razor markup.

### Manual Testing Steps:

1. Uruchom `dotnet run` i otwórz `http://localhost:5275`
2. Sprawdź ciemne tło i sidebar na ekranie ≥769px i ≤768px
3. Zaloguj się — sprawdź auth form w dark theme
4. Utwórz nową notatkę — sprawdź wizard z ikonami chevron i helper questions panel
5. Przejdź przez wszystkie 8 sekcji — sprawdź expand/collapse ikony, textarea styling
6. Kliknij "Klasyfikuj" — sprawdź result panel z pasterowymi badges i monospace labelami
7. Przejdź do "Moje notatki" — sprawdź listę kart z animacją fade-in
8. Sprawdź responsywność na 600px i 400px szerokości

## Performance Considerations

- Google Fonts z `display=swap` i `preconnect` — FOUT zminimalizowany
- Lucide CDN (~20KB gzipped) — załadowany przed Blazor, brak blokowania renderowania
- Animacje CSS-only (nie JS) — brak wpływu na Blazor rendering cycle poza `OnAfterRenderAsync` w WizardSection

## References

- Catppuccin Mocha palette spec: https://github.com/catppuccin/catppuccin#-palette
- Lucide Icons: https://lucide.dev/icons/
- Change brief: `context/changes/visual-layer-redesign/change.md`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Design Tokens & External Assets

#### Automated

- [x] 1.1 Build przechodzi: `dotnet build`
- [x] 1.2 Wszystkie testy przechodzą: `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual

- [ ] 1.3 Strona ładuje się z ciemnym tłem `#1e1e2e`
- [ ] 1.4 Fonty Inter i JetBrains Mono załadowane (DevTools → Computed)
- [ ] 1.5 Brak błędów 404 dla CDN zasobów

### Phase 2: Sidebar Layout Restructure

#### Automated

- [ ] 2.1 Build przechodzi: `dotnet build`
- [ ] 2.2 Wszystkie testy przechodzą: `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual

- [ ] 2.3 Sidebar widoczny z logo, nawigacją, użytkownikiem (≥769px)
- [ ] 2.4 Na ≤768px sidebar staje się poziomym topbarem
- [ ] 2.5 Lucide ikony w sidebarze renderują jako SVG
- [ ] 2.6 Linki nawigacyjne i wylogowanie działają poprawnie

### Phase 3: Full CSS Restyling

#### Automated

- [ ] 3.1 Build przechodzi: `dotnet build`
- [ ] 3.2 Wszystkie testy przechodzą: `dotnet test DevNote.Tests/DevNote.Tests.csproj`
- [ ] 3.3 Format nie wymaga zmian: `dotnet format --verify-no-changes --no-restore`

#### Manual

- [ ] 3.4 Brak jasnych (#fff) teł w całej aplikacji
- [ ] 3.5 Badges A/B/C z pastelowymi obramowaniami i tłem
- [ ] 3.6 Result panel: labele pól w monospace, muted
- [ ] 3.7 Hover na notes-item zmienia border na accentowy kolor
- [ ] 3.8 Fade-in animacja na notes list

### Phase 4: WizardSection Icon & Result Polish

#### Automated

- [ ] 4.1 Build przechodzi: `dotnet build`
- [ ] 4.2 Wszystkie testy przechodzą: `dotnet test DevNote.Tests/DevNote.Tests.csproj`

#### Manual

- [ ] 4.3 Ikona chevron zmienia się przy toggle i zawsze renderuje jako SVG
- [ ] 4.4 Pełny flow (create → wizard → classify → result → notes) działa bez regresji
