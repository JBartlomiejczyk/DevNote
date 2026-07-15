---
change_id: testing-wizard-state-edit-degraded-mode
title: Test wizard state, edit reversion, and degraded helper mode
status: implementing
created: 2026-07-14
updated: 2026-07-15
archived_at: null
---

## Notes

Open a change folder for rollout Phase 3 of context/foundation/test-plan.md: "Wizard state, edit-revert & degraded mode".
Risks covered: #2, #5, #6. Test types planned: component (bUnit) + integration/unit.
Risk response intent:
- #2: Prove back/forward navigation preserves every entered wizard value unchanged, including across re-render/state restoration boundaries.
- #5: Prove editing a Completed note first reverts it to Draft and re-submission replaces stale classification and summary data.
- #6: Prove helper-question failures do not block wizard progress and identical section+context requests do not trigger duplicate LLM calls.
After creating the folder, follow the downstream continuation rule.
