# Shape Notes — DevNote

---
checkpoint:
  current_phase: completed
  phases_completed:
    - vision
    - persona_access
    - mvp_scope
    - functional_requirements
    - business_logic_data
    - stack_openness
  frs_drafted: 9
  quality_check_status: passed
  anti_patterns:
    empty_crud: false
    mvp_too_big: borderline_not_flagged
---

## Vision

DevNote is a structured wizard web application for developers who talk to non-technical business stakeholders. Instead of asking "what app should we build?", DevNote guides the developer through understanding the problem, process, data, risks, and simplest safe solution — then generates a classified summary ready for further action.

**One-liner**: DevNote to notatnik dla developera, który pomaga prowadzić rozmowy z biznesem, diagnozować realny problem, określać MVP, klasyfikować temat A/B/C i zamieniać ustalenia ze spotkania w konkretne artefakty.

## Persona & Access

| Aspect | Decision |
|--------|----------|
| Primary user | Developer (human) conducting business conversations |
| Secondary user | None in MVP (business stakeholder is not a system user) |
| Access model | Single-user with login (personal account, cloud-saved notes) |
| Auth method | Simple email/password registration |
| Platform | Web app (browser-based) |
| Multi-tenancy | No — single user per instance in MVP |

## MVP Scope

### Core Flow (single primary path)

1. Developer logs in
2. Creates new note (meeting/conversation)
3. Follows **linear wizard** (one section at a time, can go back) through 8 sections:
   - Business problem
   - Current process
   - Time waste / loss
   - Input data
   - Expected output
   - Risks
   - Users
   - Solution scale
4. At each step, **LLM suggests helper questions** contextually
5. After completing the wizard, **LLM classifies** the topic as A/B/C
6. **LLM generates structured summary** (problem, users, current process, time waste, input data, output, recommended path, MVP, out-of-scope, acceptance criteria, next step)
7. Developer can view list of all past notes and edit them

### Note Lifecycle

```
Draft (in progress) → Completed (classified + summary generated)
```

### What is OUT of MVP scope

- Jira task description generation (moved to v2)
- Confluence documentation generation (moved to v2)
- Automatic code generation
- Full Jira/GitLab/Confluence/Coolify integration
- Automatic deployment of solutions
- Advanced IT/security approval workflow
- Full project lifecycle management
- Multi-org and advanced permissions
- Mobile app
- Automatic technical decisions without developer
- Full security/legal compliance analysis

## Functional Requirements

| ID | Requirement | Notes |
|----|-------------|-------|
| FR-001 | User can register and log in with email/password | Simple auth, no OAuth in MVP |
| FR-002 | User can create a new conversation note | Starts in Draft status |
| FR-003 | User navigates an 8-section linear wizard | Sections: problem, process, time waste, input data, output, risks, users, scale |
| FR-004 | Each wizard section shows LLM-suggested helper questions | Context-aware based on already-filled sections |
| FR-005 | User can go back to previous wizard sections | Non-destructive navigation |
| FR-006 | After wizard completion, system classifies topic as A/B/C via LLM | A=small local, B=departmental, C=large/sensitive/review |
| FR-007 | After classification, system generates structured summary via LLM | Contains: problem, users, process, time waste, input, output, recommended path, MVP, out-of-scope, acceptance criteria, next step |
| FR-008 | User can view a list of all their past notes | Shows title, date, status, classification |
| FR-009 | User can edit previously saved notes (re-enter wizard) | Editing a Completed note reverts it to Draft until re-classified |

## Business Logic & Data

### Classification Rules (A/B/C)

Rules are sourced from the **Workbook DEV AI v1.docx** methodology, embedded as system prompt context for the LLM. The LLM receives:
- All 8 wizard section answers
- The classification ruleset (from Workbook)
- Instruction to return A, B, or C with justification

| Class | Definition |
|-------|-----------|
| A | Small local solution (script, Excel, Power Query, process change) |
| B | Departmental solution (internal tool, limited scope) |
| C | Large solution, sensitive data, or requires IT/security review |

### Key Business Rule

> The system must recommend the **simplest viable path** — not assume that building an application is the answer. A classification of "A" should explicitly suggest non-code solutions (process change, Excel, Power Query, local script).

### Data Model (conceptual)

```
User
  - id (PK)
  - email
  - password_hash
  - created_at

Note
  - id (PK)
  - user_id (FK → User)
  - title
  - status: Draft | Completed
  - classification: A | B | C | null
  - created_at
  - updated_at

WizardSection
  - id (PK)
  - note_id (FK → Note)
  - section_type: problem | process | time_waste | input_data | output | risks | users | scale
  - content (text, user's answers)
  - helper_questions (text, LLM-generated suggestions shown to user)

Summary
  - id (PK)
  - note_id (FK → Note, 1:1)
  - problem
  - users
  - current_process
  - time_waste
  - input_data
  - expected_output
  - recommended_path
  - mvp_scope
  - out_of_scope
  - acceptance_criteria
  - next_step
  - classification_justification
```

### LLM Integration Points

| Point | Trigger | Input | Output |
|-------|---------|-------|--------|
| Helper questions | User enters a wizard section | Section type + previous sections' answers | 3-5 contextual questions |
| Classification | Wizard completed | All 8 section answers + ruleset | A/B/C + justification |
| Summary | After classification | All answers + classification | Structured summary (11 fields) |

## Stack Openness

| Aspect | Decision | Binding? |
|--------|----------|----------|
| Product type | Web application | ✅ Yes |
| Language family | C# | ✅ Yes (user preference) |
| Frontend | Blazor Server | ✅ Yes (user preference) |
| Database | PostgreSQL | ✅ Yes (Railway constraint) |
| LLM provider | Azure OpenAI | ✅ Yes |
| Deployment | Railway | ✅ Yes |
| VCS | GitHub (CLI) | ✅ Yes |
| Auth | ASP.NET Identity (email/password) | Recommended |
| ORM | Entity Framework Core | Recommended |
| CSS/UI framework | Open | ❌ |

## Success Criteria

1. Developer after conversation has a complete structured summary with problem, MVP, out-of-scope, and next steps
2. ≥80% of notes contain A/B/C classification
3. ≥75% of topics have defined acceptance criteria before implementation starts
4. ≥70% of topics end with simplest-path recommendation (not automatic "build an app")
5. ≥50% time savings on summary preparation vs. manual note-taking
6. Business stakeholder understands what will be done, what won't be in MVP, and what to verify during acceptance

## Open Questions

1. **Workbook content extraction**: The Workbook DEV AI v1.docx needs to be converted to a text-based prompt format. How much of the workbook content is relevant for system prompt? What's the token budget?
2. **LLM cost management**: Helper questions are triggered per wizard section (potentially 8 calls per note). Should there be rate limiting or caching for similar contexts?
3. **Offline/degraded mode**: What happens if Azure OpenAI is unreachable? Can the wizard still be filled (just without suggestions/classification)?
4. **Data retention**: How long are notes stored? Any deletion/export needs?
5. **Language**: Is the UI in Polish only, or English, or bilingual?
