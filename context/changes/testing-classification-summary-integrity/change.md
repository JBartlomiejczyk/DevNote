---
change_id: testing-classification-summary-integrity
title: Test harness and classification-summary integrity
status: implemented
created: 2026-07-08
updated: 2026-07-08
archived_at: null
---

## Notes

Open a change folder for rollout Phase 1 of context/foundation/test-plan.md: "Test harness + classification/summary integrity".
Risks covered: #1 (a changed/malformed LLM response yields a wrong classification or an empty/mis-mapped summary the developer trusts).
Test types planned: unit (plus bootstrapping the xUnit test project — the project currently has no test suite).
Risk response intent: prove that, given a valid completed wizard, the parsed result carries a defined A/B/C classification and all PRD-required summary fields; and that a malformed or partial model response is rejected or surfaced, never silently rendered as a blank-but-valid summary. Challenge the assumption that a strict JSON schema guarantees valid output. Avoid the oracle-problem anti-pattern.
After creating the folder, follow the downstream continuation rule (suggest /10x-research next).
