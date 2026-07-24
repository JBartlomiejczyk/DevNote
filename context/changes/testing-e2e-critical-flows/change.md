---
change_id: testing-e2e-critical-flows
title: E2E critical flows — Phase 5 Playwright tests for wizard, auth, and note lifecycle
status: implemented
created: 2026-07-22
updated: 2026-07-24
archived_at: null
---

## Notes

Phase 5 of context/foundation/test-plan.md: "E2E critical flows".
Risks covered: #2 (wizard back-navigation preserves data in a real Blazor Server browser circuit), #3 (cross-user note access denied in browser), #4 (unauthenticated requests redirected in browser), #5 (edit-revert-reclassify end-to-end in browser).
Test types planned: e2e (Playwright for .NET).
Risk response intent:
- Risk #2: prove that filling wizard sections 1-N, navigating back in the browser, and returning forward still shows the originally entered values — the real Blazor Server circuit does not drop WizardStateService state on accordion back-navigation.
- Risk #3: prove that authenticated user A requesting a note owned by user B (by supplying B's note id in the URL) sees not-found/forbidden, not B's content — ownership enforced in the routed Blazor page, not only at API level.
- Risk #4: prove that opening any data-bearing page URL without a valid session cookie redirects to /login and returns no user/note data — the real ASP.NET Core auth middleware fires in the hosted app.
- Risk #5: prove that opening a Completed note, editing any field, and submitting shows the note as Draft first (classification cleared in UI), then shows a new classification and summary after re-submission — the full browser round-trip, not just component state.
