# GitHub Issues — DevNote Roadmap Migration

> Generated from `context/foundation/roadmap.md` (v1).
> Repo: https://github.com/JBartlomiejczyk/DevNote

## Configuration

- **Milestone:** `MVP`
- **Labels:**
  - `foundation` — cross-cutting enabler (F-NN items)
  - `slice` — vertical user-facing slice (S-NN items)
  - `decision` — open question requiring human resolution
  - `ready` — prerequisites met, can be planned/implemented
  - `blocked` — prerequisites not met or unknowns unresolved

## Issues

### Issue 1: [F-01] Deploy skeleton

- **Labels:** `foundation`, `ready`
- **Milestone:** MVP
- **Body:**

```markdown
## Outcome

(foundation) Dockerfile + GitHub Actions workflow landed; app auto-deploys to Railway on push to main.

## Details

| Field | Value |
|-------|-------|
| Roadmap ID | F-01 |
| Change ID | `deploy-skeleton` |
| PRD refs | — |
| Prerequisites | — |
| Parallel with | S-01 |
| Unlocks | S-01, S-02, S-03, S-04 |

## Risk

Sequenced early because market-feedback goal requires real-world usage; without deploy, validation stays local-only. Low technical risk — Railway + Dockerfile is a known pattern.

## Next step

Run `/10x-plan deploy-skeleton`
```

---

### Issue 2: [S-01] Wizard → Classification → Summary ⭐ North Star

- **Labels:** `slice`, `ready`
- **Milestone:** MVP
- **Body:**

```markdown
## Outcome

Developer can fill an 8-section wizard (problem, process, time waste, input data, output, risks, users, scale), navigate back without losing data, submit, and receive an A/B/C classification with justification plus a structured summary (11 fields).

## Details

| Field | Value |
|-------|-------|
| Roadmap ID | S-01 |
| Change ID | `wizard-classification-summary` |
| PRD refs | US-02, US-04, US-05, FR-003, FR-005, FR-006, FR-007 |
| Prerequisites | — |
| Parallel with | F-01 |

## Unknowns

- What subset of the workbook methodology is needed for the classification prompt? — Owner: user. Block: no.

## Risk

This is the north star and carries the most product uncertainty. The riskiest assumption — that structured wizard input + LLM classification produces output useful enough to save developers time — is tested here and nowhere else.

## Next step

Run `/10x-plan wizard-classification-summary`
```

---

### Issue 3: [S-02] Auth + persistent notes

- **Labels:** `slice`, `blocked`
- **Milestone:** MVP
- **Body:**

```markdown
## Outcome

Developer can register with email/password, log in, and have their wizard results saved as persistent conversation notes that survive across sessions.

## Details

| Field | Value |
|-------|-------|
| Roadmap ID | S-02 |
| Change ID | `auth-and-note-persistence` |
| PRD refs | US-01, FR-001, FR-002 |
| Prerequisites | S-01 |
| Parallel with | S-04 |

## Blocking reason

Depends on **S-01** (wizard-classification-summary) being completed first.

## Risk

Standard auth + CRUD pattern (low technical risk), but introduces three new layers at once (Identity, EF Core, PostgreSQL). Sequenced after S-01 because persistence enables multi-session market-feedback measurement.

## Next step

Wait for S-01 completion, then run `/10x-plan auth-and-note-persistence`
```

---

### Issue 4: [S-03] Note management

- **Labels:** `slice`, `blocked`
- **Milestone:** MVP
- **Body:**

```markdown
## Outcome

Developer can view a list of past notes (title, date, status, classification), re-enter the wizard to edit any note, and re-classify after editing (completed notes revert to Draft on edit).

## Details

| Field | Value |
|-------|-------|
| Roadmap ID | S-03 |
| Change ID | `note-management` |
| PRD refs | US-06, FR-008, FR-009 |
| Prerequisites | S-02 |
| Parallel with | S-04 |

## Blocking reason

Depends on **S-02** (auth-and-note-persistence) being completed first.

## Risk

Low — standard list/edit UI on top of existing entities. Sequenced after S-02 because you need saved notes to manage them.

## Next step

Wait for S-02 completion, then run `/10x-plan note-management`
```

---

### Issue 5: [S-04] Contextual helper questions

- **Labels:** `slice`, `blocked`
- **Milestone:** MVP
- **Body:**

```markdown
## Outcome

Developer sees 3-5 contextually relevant AI-generated helper questions when entering each wizard section, informed by answers already provided in previous sections.

## Details

| Field | Value |
|-------|-------|
| Roadmap ID | S-04 |
| Change ID | `contextual-helper-questions` |
| PRD refs | US-03, FR-004 |
| Prerequisites | S-01 |
| Parallel with | S-02, S-03 |

## Blocking reason

Depends on **S-01** (wizard-classification-summary) being completed first.

## Unknowns

- What rate-limiting or caching strategy is needed for per-section LLM calls (up to 8 per note)? — Owner: user. Block: no.

## Risk

Validates the second riskiest assumption — that AI-guided exploration during fill produces materially better input than an unguided form.

## Next step

Wait for S-01 completion, then run `/10x-plan contextual-helper-questions`
```

---

### Issue 6: [Decision] UI language

- **Labels:** `decision`
- **Milestone:** MVP
- **Body:**

```markdown
## Question

UI language — Polish only, English only, or bilingual?

## Impact

Block: **roadmap-wide** — affects all user-facing text in every slice.

## Owner

User

## Options to consider

1. Polish only — matches the primary persona's natural language
2. English only — simpler i18n, broader potential reach
3. Bilingual — maximum flexibility, but doubles UI copy effort
```

---

### Issue 7: [Decision] Workbook content extraction

- **Labels:** `decision`
- **Milestone:** MVP
- **Body:**

```markdown
## Question

What subset of the methodology workbook is relevant for LLM classification prompts? What's the token budget?

## Impact

Block: no — system can launch with a minimal subset and refine.

## Owner

User
```

---

### Issue 8: [Decision] LLM cost management

- **Labels:** `decision`
- **Milestone:** MVP
- **Body:**

```markdown
## Question

Rate limiting or caching for helper questions (up to 8 LLM calls per note)?

## Impact

Block: no — functional without, but cost risk at scale.

## Owner

User
```

---

### Issue 9: [Decision] Degraded mode

- **Labels:** `decision`
- **Milestone:** MVP
- **Body:**

```markdown
## Question

Can the wizard still be filled when Azure OpenAI is unreachable? What's the fallback UX?

## Impact

Block: no — affects reliability, not core function.

## Owner

User
```

---

### Issue 10: [Decision] Data retention

- **Labels:** `decision`
- **Milestone:** MVP
- **Body:**

```markdown
## Question

How long are notes stored? Any deletion or export requirements?

## Impact

Block: no.

## Owner

User
```

---

### Issue 11: [Decision] Timeline and budget

- **Labels:** `decision`
- **Milestone:** MVP
- **Body:**

```markdown
## Question

Target MVP delivery timeline? After-hours only? Hard deadline?

## Impact

Block: no — affects pacing, not product shape.

## Owner

User
```

---

### Issue 12: [Decision] Non-Functional Requirements

- **Labels:** `decision`
- **Milestone:** MVP
- **Body:**

```markdown
## Question

Performance, availability, and security targets for MVP?

## Impact

Block: no — can launch with reasonable defaults.

## Owner

User
```

## Summary

| # | Title | Labels | Status |
|---|-------|--------|--------|
| 1 | [F-01] Deploy skeleton | foundation, ready | ready |
| 2 | [S-01] Wizard → Classification → Summary ⭐ | slice, ready | ready |
| 3 | [S-02] Auth + persistent notes | slice, blocked | blocked (needs S-01) |
| 4 | [S-03] Note management | slice, blocked | blocked (needs S-02) |
| 5 | [S-04] Contextual helper questions | slice, blocked | blocked (needs S-01) |
| 6 | [Decision] UI language | decision | open |
| 7 | [Decision] Workbook content extraction | decision | open |
| 8 | [Decision] LLM cost management | decision | open |
| 9 | [Decision] Degraded mode | decision | open |
| 10 | [Decision] Data retention | decision | open |
| 11 | [Decision] Timeline and budget | decision | open |
| 12 | [Decision] Non-Functional Requirements | decision | open |
