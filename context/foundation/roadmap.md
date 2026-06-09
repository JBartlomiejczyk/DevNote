---
project: "DevNote"
version: 1
status: draft
created: 2026-06-08
updated: 2026-06-08
prd_version: 1
main_goal: market-feedback
top_blocker: decisions
---

# Roadmap: DevNote

> Derived from `context/foundation/prd.md` (v1) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Vision recap

Developers who talk to non-technical stakeholders lack a structured way to diagnose the real problem, determine scope, and identify the simplest viable path. Conversations drift, and "build an application" becomes the default — even when a process change or spreadsheet would suffice. DevNote provides an 8-section wizard that forces systematic exploration, then classifies the topic (A/B/C) and generates a structured summary with concrete next steps.

## North star

**S-01: Developer fills the 8-section wizard and receives A/B/C classification + structured summary** — the smallest end-to-end slice that validates DevNote's core product hypothesis: structured wizard input + LLM classification produces useful, time-saving output.

> The north star is the smallest end-to-end slice whose successful delivery would prove the core product hypothesis — placed as early as Prerequisites allow because everything else only matters if this works.

## At a glance

| ID | Change ID | Outcome (user can …) | Prerequisites | PRD refs | Status |
|---|---|---|---|---|---|
| F-01 | deploy-skeleton | (foundation) Dockerfile + CI/CD landed; app deploys to Railway on push | — | — | done |
| S-01 | wizard-classification-summary | fill 8-section wizard and receive A/B/C classification + structured summary | — | US-02, US-04, US-05, FR-003, FR-005, FR-006, FR-007 | done |
| S-02 | auth-and-note-persistence | register, log in, and save conversation notes persistently | S-01 | US-01, FR-001, FR-002 | ready |
| S-03 | note-management | view past notes, re-enter wizard to edit, re-classify | S-02 | US-06, FR-008, FR-009 | proposed |
| S-04 | contextual-helper-questions | see contextual AI-generated helper questions during each wizard section | S-01 | US-03, FR-004 | ready |

## Streams

Navigation aid — groups items that share a Prerequisites chain. Canonical ordering still lives in the dependency graph below; this table is the proposed reading order across parallel tracks.

| Stream | Theme | Chain | Note |
|---|---|---|---|
| A | Core validation pipeline | `S-01` → `S-02` → `S-03` | North star first, then persistence for multi-session market-feedback testing. |
| B | AI-guided exploration | `S-04` | Joins Stream A at `S-01`; validates the second riskiest assumption (guidance quality). |
| C | Infrastructure | `F-01` | Parallel with all; enables production deployment for real-world validation. |

## Baseline

What's already in place in the codebase as of 2026-06-08 (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** absent — no Blazor SDK, no .razor files, no UI framework
- **Backend / API:** partial — ASP.NET Core 9 minimal API scaffold (`Program.cs`), sample `/weatherforecast` only, no real endpoints or middleware
- **Data:** absent — no EF Core, no DbContext, no migrations, no connection strings
- **Auth:** absent — no Identity packages, no auth middleware
- **Deploy / infra:** absent — no Dockerfile, no CI/CD, no railway.toml
- **Observability:** absent — built-in host logging only, no structured logging or error tracking

## Foundations

### F-01: Deploy skeleton

- **Outcome:** (foundation) Dockerfile + GitHub Actions workflow landed; app auto-deploys to Railway on push to main.
- **Change ID:** deploy-skeleton
- **PRD refs:** —
- **Unlocks:** S-01, S-02, S-03, S-04 — production deployment required for market-feedback validation with real meeting conversations
- **Prerequisites:** —
- **Parallel with:** S-01
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Sequenced early because market-feedback goal requires real-world usage; without deploy, validation stays local-only. Low technical risk — Railway + Dockerfile is a known pattern per `infrastructure.md`.
- **Status:** done

## Slices

### S-01: Wizard → Classification → Summary

- **Outcome:** developer can fill an 8-section wizard (problem, process, time waste, input data, output, risks, users, scale), navigate back without losing data, submit, and receive an A/B/C classification with justification plus a structured summary (11 fields)
- **Change ID:** wizard-classification-summary
- **PRD refs:** US-02, US-04, US-05, FR-003, FR-005, FR-006, FR-007
- **Prerequisites:** —
- **Parallel with:** F-01
- **Blockers:** —
- **Unknowns:**
  - What subset of the workbook methodology is needed for the classification prompt? — Owner: user. Block: no (can launch with a minimal ruleset and refine).
- **Risk:** This is the north star and carries the most product uncertainty. The riskiest assumption — that structured wizard input + LLM classification produces output useful enough to save developers time — is tested here and nowhere else. Sequenced first because market-feedback demands validating this assumption earliest.
- **Status:** done

### S-02: Auth + persistent notes

- **Outcome:** developer can register with email/password, log in, and have their wizard results saved as persistent conversation notes that survive across sessions
- **Change ID:** auth-and-note-persistence
- **PRD refs:** US-01, FR-001, FR-002
- **Prerequisites:** S-01
- **Parallel with:** S-04
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Standard auth + CRUD pattern (low technical risk), but introduces three new layers at once (Identity, EF Core, PostgreSQL). Sequenced after S-01 because persistence enables multi-session market-feedback measurement (comparing outcomes across real meetings).
- **Status:** ready

### S-03: Note management

- **Outcome:** developer can view a list of past notes (title, date, status, classification), re-enter the wizard to edit any note, and re-classify after editing (completed notes revert to Draft on edit)
- **Change ID:** note-management
- **PRD refs:** US-06, FR-008, FR-009
- **Prerequisites:** S-02
- **Parallel with:** S-04
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Low — standard list/edit UI on top of existing entities. Sequenced after S-02 because you need saved notes to manage them.
- **Status:** proposed

### S-04: Contextual helper questions

- **Outcome:** developer sees 3-5 contextually relevant AI-generated helper questions when entering each wizard section, informed by answers already provided in previous sections
- **Change ID:** contextual-helper-questions
- **PRD refs:** US-03, FR-004
- **Prerequisites:** S-01
- **Parallel with:** S-02, S-03
- **Blockers:** —
- **Unknowns:**
  - What rate-limiting or caching strategy is needed for per-section LLM calls (up to 8 per note)? — Owner: user. Block: no (functional without, but cost risk).
- **Risk:** Validates the second riskiest assumption — that AI-guided exploration during fill produces materially better input than an unguided form. Sequenced parallel with persistence because it's an independent enhancement to the wizard.
- **Status:** ready

## Backlog Handoff

| Roadmap ID | Change ID | Suggested issue title | Ready for `/10x-plan` | Notes |
|---|---|---|---|---|
| F-01 | deploy-skeleton | Set up Dockerfile + CI/CD for Railway auto-deploy | — | Done (implemented) |
| S-01 | wizard-classification-summary | 8-section wizard with A/B/C classification and structured summary | — | Done (implemented) |
| S-02 | auth-and-note-persistence | User registration, login, and persistent note saving | yes | Run `/10x-plan auth-and-note-persistence` |
| S-03 | note-management | Past notes list, re-enter wizard, re-classify | no | Depends on S-02 |
| S-04 | contextual-helper-questions | LLM-generated contextual helper questions per wizard section | yes | Run `/10x-plan contextual-helper-questions` |

## Open Roadmap Questions

1. **UI language — Polish only, English only, or bilingual?** — Owner: user. Block: roadmap-wide (affects all user-facing text in every slice).
2. **Workbook content extraction — what subset of the methodology workbook is relevant for LLM classification prompts? What's the token budget?** — Owner: user. Block: no (system can launch with a minimal subset).
3. **LLM cost management — rate limiting or caching for helper questions (up to 8 LLM calls per note)?** — Owner: user. Block: no (functional without, but cost risk at scale).
4. **Degraded mode — can the wizard still be filled when Azure OpenAI is unreachable? What's the fallback UX?** — Owner: user. Block: no (affects reliability, not core function).
5. **Data retention — how long are notes stored? Any deletion or export requirements?** — Owner: user. Block: no.
6. **Timeline and budget — target MVP delivery timeline? After-hours only? Hard deadline?** — Owner: user. Block: no (affects pacing, not product shape).
7. **Non-Functional Requirements — performance, availability, and security targets?** — Owner: user. Block: no (can launch with reasonable defaults).

## Parked

- **External issue-tracker task description generation** — Why parked: PRD §Non-Goals, deferred to v2.
- **External documentation platform generation** — Why parked: PRD §Non-Goals, deferred to v2.
- **Automatic code generation** — Why parked: PRD §Non-Goals; system recommends paths, doesn't generate implementations.
- **Full integration with external platforms (Jira/GitLab/Confluence/Coolify)** — Why parked: PRD §Non-Goals, MVP is self-contained.
- **Automatic deployment of solutions** — Why parked: PRD §Non-Goals, out of scope entirely.
- **Advanced IT/security approval workflow** — Why parked: PRD §Non-Goals; C-classification notes the need but doesn't orchestrate.
- **Full project lifecycle management** — Why parked: PRD §Non-Goals; MVP covers diagnosis + classification only.
- **Multi-organization support or advanced permissions** — Why parked: PRD §Non-Goals; single-user model in MVP.
- **Mobile application** — Why parked: PRD §Non-Goals; web-only in MVP.
- **Automatic technical decisions without developer involvement** — Why parked: PRD §Non-Goals; developer always validates.
- **Full security/legal compliance analysis** — Why parked: PRD §Non-Goals; system flags but doesn't analyze.

## Done

- **F-01** deploy-skeleton — Dockerfile + GitHub Actions CI/CD, Railway auto-deploy (SHA: 0e94842)
- **S-01** wizard-classification-summary — 8-section Blazor wizard + Azure OpenAI A/B/C classification + 11-field summary (SHAs: 1c6dda7, 0894a28, ef407ad)

