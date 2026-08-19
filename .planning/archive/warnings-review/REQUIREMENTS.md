# Requirements — Warnings Review

**Goal:** Ship a read-only Warnings command in the LECG Health ribbon panel that lists the model's warnings grouped by description, with per-group actions to select, show, or temporarily isolate the offending elements.

**Opened:** 2026-07-27 · Source: CANDIDATES.md (shaped 2026-07-27, from EF-Tools concept — no GPL code)

## In scope

- R1: A Warnings button exists in the Health ribbon panel, wired the standard way — `WarningsCommand : RevitCommand`, project-document availability class, DI registration for service/ViewModel/View.
- R2: Invoking the command lists every warning returned by `Document.GetWarnings()`, grouped by description text, with a per-group count and a total count.
- R3: The grouping/ordering logic lives in `LECG.Core` and is unit-tested without Revit (input: description/severity/element-id tuples; output: ordered groups with counts).
- R4: From a warning group the user can select its failing elements in the model (selection set updated to exactly those elements).
- R5: From a warning group the user can temporarily isolate its failing elements in the active view; the isolation is Revit's temporary view mode — reversible, no document write.
- R6: The whole flow performs no document writes — no `ITransactionService` dependency in the command, service, or ViewModel.
- R7: A failure reading any single `FailureMessage` (disposed/invalid) skips that entry with a logged warning and continues; one bad message never aborts the listing.
- R8: Live Revit smoke check on a model with known warnings: group counts match Revit's own Review Warnings dialog and select/isolate act on the correct elements; result recorded in `docs/ai/revit-smoke-test.md` Run History.

## Out of scope

- Auto-fixing or dismissing warnings (read-only by design).
- Persisting or exporting warning reports.
- Anything touching the purge flows — view-template/filter purge already shipped in `PurgeExtendedElementService`.
