# Phase 2 Context

## Goal

The Warnings command is proven against a real model with the user at the keyboard (R8): group counts match Revit's Review Warnings dialog, select/show/isolate act on the correct elements, isolation is confirmed temporary, and the run is recorded in `docs/ai/revit-smoke-test.md` Run History.

## Decisions

- None needed — the phase is a guided checklist run, and the repo already decides the procedure (`docs/ai/revit-smoke-test.md`), the deploy recipe (`dotnet build -c Release` with Revit closed), and the fallback for the one known risk.

## Assumptions confirmed

- Deploy target is `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\`; a Release build with Revit closed refreshes it (smoke-test doc, Preconditions).
- The read path (`GetWarnings → GetDescriptionText/GetFailingElements → group`) was already exercised live via the revit MCP on 2026-07-27 (10 warnings / 1 group / 0 skipped). Phase 2 validates what MCP could not: ribbon presence, dialog bindings, action behavior, and count parity with the Review Warnings dialog.
- Relevant checklist subset, not the full 9 steps: startup/ribbon (steps 1–2), `ProjectDocumentAvailability` greying with no doc open (step 4), the Warnings dialog itself, and no-unexpected-modification (step 6 — Undo list clean, no save prompt after a read-only run).
- The revit MCP may assist during the run (e.g. independent warning count on the open model), but every pass/fail is user-observed — the agent must not claim a step passed otherwise (smoke-test doc header rule).
- No code changes expected in this phase. The only contingency: if `ShowElements` misbehaves behind the modal dialog, apply the documented fallback (close dialog before showing) and re-verify.

## Constraints

- Results recorded as a dated Run History entry in `docs/ai/revit-smoke-test.md` plus a `docs/ai/worklog.md` note (smoke-test doc, Recording results).
- Isolation must be verified *temporary* — cleared via Revit's own temporary-view-mode control, no custom reset (phase 1 CONTEXT, Assumptions).
- Read-only guarantee is part of the pass criteria: no new Undo entries, no save prompt on a clean document (R6 observed live).
- Revit must be closed during the deploying build (locked DLLs → stale mix).

## Out of scope

- The theme-scoping targeted section of the smoke-test doc (ran 2026-07-25; unchanged since).
- Steps 7–8 of the generic checklist (transactions, modeless/ExternalEvent) — the Warnings command uses neither.
- Any feature work; fixes only if the smoke test fails.

## Open

- **Test model choice** — the MCP-exercised model had 10 warnings in 1 group, which can't show count-descending group ordering. Prefer a model with ≥2 warning description groups; resolved at run time by whichever model the user opens.
- **`ShowElements` behind the modal dialog** — resolved by the test itself; fallback documented above.
