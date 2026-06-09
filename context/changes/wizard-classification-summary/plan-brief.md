# Plan Brief: wizard-classification-summary

## One-liner

Add Blazor Server 8-section accordion wizard (Polish UI) that calls Azure OpenAI gpt-4o-mini to produce A/B/C classification + 11-field structured summary in a single JSON-schema call.

## Phases (3)

1. **Blazor Server Setup + Wizard UI** — Add Blazor Server infra, build accordion wizard with 8 Polish sections, in-memory circuit-scoped state
2. **Azure OpenAI Integration** — Add SDK, classification service with structured-output prompt, wire submit → LLM → result
3. **Result Display + Polish** — Classification badge (A=green/B=yellow/C=red), 11-field summary panel, error+retry, CSS

## Key Decisions

- Blazor Server (not WASM) — simpler Railway deploy, circuit-scoped state
- In-memory state only (no DB) — persistence comes in S-02
- Single LLM call returns classification + summary together (cost/latency optimal)
- Polish-only UI — all labels, prompts, outputs
- gpt-4o-mini — cost-effective, sufficient for classification task
- Accordion (not stepper) — all sections visible for review

## Risk Register

| Risk | Mitigation |
|------|-----------|
| Azure OpenAI key not configured | Clear error message + retry. App still renders wizard without crashing |
| gpt-4o-mini quality insufficient for classification | Prompt engineered conservatively; model upgradeable to gpt-4o via config |
| Blazor Server SignalR disconnects | Default reconnection UI; state preserved in circuit service |
| Structured output schema rejected by model | Fallback to JSON mode without strict schema |

## Dependencies

- Azure OpenAI resource with gpt-4o-mini deployment (user must provision)
- No DB, no auth, no external services beyond Azure OpenAI
