---
project: "DevNote"
version: 1
status: draft
created: 2026-06-08
context_type: greenfield
product_type: web-app
target_scale:
  users: small
  qps: low
  data_volume: small
timeline_budget: "# TODO: timeline_budget — see Open Questions"
---

## Vision & Problem Statement

Developers who talk to non-technical business stakeholders lack a structured way to diagnose the real problem, determine scope, and identify the simplest viable solution path. Conversations drift, key aspects (data, risks, current process) go unexplored, and "build an application" becomes the default recommendation — even when a process change or spreadsheet automation would suffice.

DevNote captures the insight that most business problems don't need custom software: the first job is classification and recommendation of the simplest safe path. A structured wizard forces the developer to explore all relevant dimensions before jumping to solutions, and an automated classification system prevents over-engineering by explicitly surfacing non-code alternatives.

## User & Persona

**Primary persona**: A developer (individual contributor) who regularly meets with non-technical business stakeholders to discuss problems, process improvements, or automation needs. They reach for DevNote before or during a conversation when they need to systematically explore the problem space, capture decisions, and produce a classified summary that communicates next steps back to the stakeholder.

No secondary persona in MVP. The business stakeholder is a conversation participant but not a system user.

## Success Criteria

### Primary

- After completing a conversation note, the developer has a structured summary with problem, MVP scope, out-of-scope items, and concrete next steps
- ≥50% time savings on summary preparation compared to manual note-taking

### Secondary

- ≥80% of notes contain an A/B/C classification
- ≥75% of topics have defined acceptance criteria before implementation starts
- ≥70% of topics end with a simplest-path recommendation (not automatic "build an app")

### Guardrails

- Business stakeholder understands what will be done, what won't be in MVP, and what to verify during acceptance
- Classification must never default to "C" (build an app) without the wizard having explored simpler alternatives first

## User Stories

### US-01: Developer creates a new conversation note

- **Given** a logged-in developer
- **When** they initiate a new note
- **Then** a Draft note is created and the wizard opens at section 1

#### Acceptance Criteria

- Note starts in Draft status
- Wizard sections are presented linearly starting from section 1

### US-02: Developer navigates the structured wizard

- **Given** a developer with an open Draft note
- **When** they proceed through the 8 wizard sections (problem, process, time waste, input data, output, risks, users, scale)
- **Then** they complete a structured exploration of the business problem

#### Acceptance Criteria

- All 8 sections are presented one at a time in order
- User can proceed forward and backward without losing entered data
- Each section captures free-text answers

### US-03: Developer receives contextual helper questions

- **Given** a developer viewing a wizard section
- **When** the section loads (considering previously filled sections as context)
- **Then** they see 3-5 contextually relevant helper questions to guide their exploration

#### Acceptance Criteria

- Helper questions are relevant to the current section type
- Questions account for answers already provided in previous sections
- Questions serve as prompts — answering them is optional

### US-04: Developer receives topic classification

- **Given** a developer who has completed all 8 wizard sections
- **When** they submit the completed wizard
- **Then** the system produces an A/B/C classification with justification

#### Acceptance Criteria

- Classification is one of: A (small local), B (departmental), C (large/sensitive)
- A justification explains why the classification was chosen
- Classification of "A" explicitly suggests non-code solutions

### US-05: Developer receives structured summary

- **Given** a developer whose note has been classified
- **When** classification completes
- **Then** they receive a structured summary covering: problem, users, current process, time waste, input data, expected output, recommended path, MVP scope, out-of-scope, acceptance criteria, and next step

#### Acceptance Criteria

- Summary contains all 11 specified fields
- Summary reflects the content entered in the wizard
- Note status transitions from Draft to Completed

### US-06: Developer manages past notes

- **Given** a logged-in developer with existing notes
- **When** they view their notes list
- **Then** they see all past notes with title, date, status, and classification

#### Acceptance Criteria

- Notes are listed with title, creation date, status (Draft/Completed), and classification (A/B/C or null)
- Developer can re-enter the wizard for any note to edit it
- Editing a Completed note reverts it to Draft until re-classified

## Functional Requirements

### Authentication

- FR-001: User can register and log in with email and password. Priority: must-have

### Note Management

- FR-002: User can create a new conversation note (starts in Draft status). Priority: must-have
- FR-008: User can view a list of all their past notes (showing title, date, status, classification). Priority: must-have
- FR-009: User can edit previously saved notes by re-entering the wizard; editing a Completed note reverts it to Draft until re-classified. Priority: must-have

### Wizard Flow

- FR-003: User can navigate an 8-section linear wizard (sections: problem, process, time waste, input data, output, risks, users, scale). Priority: must-have
- FR-004: User receives contextually-suggested helper questions for each wizard section, informed by already-filled sections. Priority: must-have
- FR-005: User can navigate back to previous wizard sections without losing data. Priority: must-have

### Classification & Summary

- FR-006: After wizard completion, system classifies the topic as A, B, or C with justification. Priority: must-have
- FR-007: After classification, system generates a structured summary containing: problem, users, current process, time waste, input data, expected output, recommended path, MVP scope, out-of-scope, acceptance criteria, next step. Priority: must-have

## Non-Functional Requirements

# TODO: Non-Functional Requirements — see Open Questions

## Business Logic

The system must recommend the simplest viable path for each business problem — not assume that building an application is the answer; a classification of "A" must explicitly suggest non-code solutions (process change, spreadsheet automation, local scripting).

The classification rule consumes the user's answers across all 8 wizard sections and produces one of three categories:

| Class | Definition |
|-------|------------|
| A | Small local solution — solvable with scripting, spreadsheet tools, or process change |
| B | Departmental solution — internal tool with limited scope |
| C | Large solution, involves sensitive data, or requires formal review |

The classification methodology is sourced from an established workbook methodology that defines the boundaries between classes based on solution scale, data sensitivity, and organizational impact. The system surfaces a justification alongside the classification so the developer can validate or override the recommendation.

## Access Control

Single-user application with personal account. Each user sees only their own notes. Authentication is email/password registration. No roles beyond "authenticated user." Unauthenticated access shows only the login/registration page.

## Non-Goals

- **External issue-tracker task description generation** — deferred to v2; MVP produces a classified summary, not platform-specific artifacts
- **External documentation platform generation** — deferred to v2; same rationale
- **Automatic code generation** — the system recommends paths, it does not generate implementations
- **Full integration with external project management, version control, documentation, or deployment platforms** — deferred; MVP is self-contained
- **Automatic deployment of solutions** — out of scope entirely; the system is advisory
- **Advanced IT/security approval workflow** — a C-classification notes the need for review but does not orchestrate it
- **Full project lifecycle management** — MVP covers problem diagnosis and classification only
- **Multi-organization support or advanced permissions** — single-user model in MVP
- **Mobile application** — web-only in MVP
- **Automatic technical decisions without developer involvement** — the developer always validates and owns the classification
- **Full security/legal compliance analysis** — the system flags topics that need review (class C) but does not perform compliance analysis

## Open Questions

1. **Workbook content extraction** — The methodology workbook needs conversion to a text-based prompt format. How much content is relevant for system context? What's the token budget? Owner: user. Block: no (system can launch with a subset).
2. **Cost management for contextual question generation** — Helper questions are triggered per wizard section (up to 8 calls per note). Should there be rate limiting or caching for similar contexts? Owner: user. Block: no (functional without, but cost risk).
3. **Degraded mode when external services are unreachable** — Can the wizard still be filled without contextual suggestions and classification? What's the fallback UX? Owner: user. Block: no (but affects reliability).
4. **Data retention policy** — How long are notes stored? Any deletion/export requirements? Owner: user. Block: no.
5. **UI language** — Polish only, English only, or bilingual? Owner: user. Block: yes (affects all user-facing text).
6. **Timeline and budget** — What is the target MVP delivery timeline (weeks)? Is this after-hours only? Any hard deadline? Owner: user. Block: no (affects planning but not product shape).
7. **Non-Functional Requirements** — No explicit NFRs were captured during shaping. What are the performance, availability, and security targets? Owner: user. Block: no (can launch with reasonable defaults, but explicit targets prevent scope disputes).
