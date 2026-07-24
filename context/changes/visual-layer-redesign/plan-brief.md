# Visual Layer Redesign — Plan Brief

> Full plan: `context/changes/visual-layer-redesign/plan.md`

## What & Why

Aktualna warstwa wizualna DevNote jest generyczna — SaaS starter template z `#1a1a2e` navy i `#4a6cf7` blue, system font stack, zero ikon i zero brandingu. Zmiana odświeża wyłącznie UI tak, aby aplikacja wyglądała jak prawdziwe narzędzie developerskie: ciemny notebook z charakterem, nie kolejny szablon.

## Starting Point

Cały system wizualny żyje w jednym pliku `wwwroot/css/app.css` (627 linii, zero external framework). Markup w `.razor` jest semantyczny i odseparowany od stylów — wystarczy przepisać jeden CSS plik i trzy pliki Razor (`App.razor`, `MainLayout.razor`, `WizardSection.razor`).

## Desired End State

Aplikacja działa na stałym dark theme **Catppuccin Mocha** (`#1e1e2e` base) z pastelowymi akcentami, sidebar-based layoutem z logo i nawigacją, fontami **Inter** (treść) + **JetBrains Mono** (nagłówki/labele/badge) oraz ikonami **Lucide** zastępującymi znaki Unicode. Wszystkie 37 testów przechodzi bez zmian — żaden plik poza warstwą UI nie jest dotknięty.

## Key Decisions Made

| Decision | Choice | Why | Source |
|---|---|---|---|
| Kierunek estetyczny | Ciemny IDE / terminal | Natychmiast rozpoznawalny jako narzędzie developerskie | Plan |
| Paleta kolorów | Catppuccin Mocha | Znana w środowisku devów, gotowe reguły kontrastu, spójna estetyka | Plan |
| Typografia | JetBrains Mono + Inter (Google Fonts CDN) | Monospace w elementach "technicznych" + czytelny sans-serif dla treści | Plan |
| Biblioteka ikon | Lucide Icons (unpkg CDN) | Minimalne, SVG, działa przez `data-lucide` + 1 JS call | Plan |
| Layout | Sidebar z nawigacją (logo + linki) | Typowy UX narzędzi produktywności; uwalnia header od linków | Plan |
| Animacje | Subtelne CSS transitions (200ms) | Dodaje życia bez rozpraszania; zero dodatkowego JS | Plan |
| Dark mode toggle | Brak — stały dark theme | Spójny charakter; zero dodatkowej złożoności CSS | Plan |

## Scope

**In scope:**
- `wwwroot/css/app.css` — pełny redesign z CSS custom properties (Catppuccin Mocha)
- `Components/App.razor` — CDN linki (fonty + Lucide)
- `wwwroot/js/app.js` — inicjalizacja Lucide + handler nawigacji Blazor
- `Components/Layout/MainLayout.razor` — sidebar layout z Lucide ikonami
- `Components/Shared/WizardSection.razor` — Lucide chevrons zamiast `▶`/`▼`
- `Components/Pages/Result.razor` — drobny polish (opcjonalna ikona)

**Out of scope:**
- Wszystkie serwisy, modele, migracje, testy
- Light mode / dark mode toggle
- Pełny sidebar z listą sekcji wizarda
- Animacje JS (skeleton, slide-in)
- Zmiany w Pages/ (Wizard, EditNote, Notes, Account) poza `Result.razor`

## Architecture / Approach

Brak zewnętrznego framework CSS — cały redesign to przepisanie jednego pliku CSS przy użyciu CSS custom properties jako warstwy abstrakcji nad paletą Catppuccin Mocha. Lucide integruje się przez CDN script + `lucide.createIcons()` call; Blazor Server wymaga re-initu przy każdym re-renderze komponentu z ikonami (`OnAfterRenderAsync` w `WizardSection`).

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Design Tokens & Assets | `:root` z paletą Catppuccin, fonty Inter + JetBrains Mono, Lucide CDN | CDN latency; FOUT jeśli `preconnect` brak |
| 2. Sidebar Layout | `MainLayout.razor` przebudowany na CSS Grid z sidebarem | Mobile responsywność; Lucide ikony w sidebarze wymagają poprawnego kolejności ładowania |
| 3. Full CSS Restyling | Wszystkie komponenty w nowym dark theme, fade-in animacje | Kontrast WCAG — pastelowe kolory na ciemnym tle mogą spaść poniżej 4.5:1 |
| 4. WizardSection & Result Polish | Lucide chevrons w accordion, `OnAfterRenderAsync` dla re-init ikon | Blazor DOM diffing vs Lucide SVG replacement — ikony mogą "znikać" po re-renderze jeśli interop nie działa |

**Prerequisites:** Działająca aplikacja z przechodzącymi 37 testami (baseline)  
**Estimated effort:** ~1 sesja × 4 fazy (głównie praca CSS + drobny Razor markup)

## Open Risks & Assumptions

- **Lucide + Blazor Server interop:** `OnAfterRenderAsync` w `WizardSection` wywołuje `lucide.createIcons()` przy każdym re-renderze — może generować nadmiarowe wywołania JS, ale Lucide jest idempotentny i lekki
- **Kontrast kolorów:** Pastelowe akcenty Catppuccin (np. `#a6e3a1` green na `#1e1e2e` bg) osiągają ok. 8:1 — bezpieczne; sprawdź `--color-text-muted` (`#a6adc8`) który zbliża się do granicy 4.5:1

## Success Criteria (Summary)

- Wizualnie aplikacja wygląda jak narzędzie developerskie — ciemne tło, monospace akcenty, sidebar, pastelowe badge
- Wszystkie 37 testów przechodzi bez modyfikacji w plikach testowych
- Pełny user flow (login → wizard → classify → result → notes list) działa end-to-end
