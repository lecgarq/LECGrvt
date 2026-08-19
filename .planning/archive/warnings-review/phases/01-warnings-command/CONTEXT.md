# Phase 1 Context

## Goal

The Warnings button exists and, in a project document, opens a view listing all warnings grouped by description with working select/show/isolate actions — everything except live-Revit proof (R1–R7).

## Decisions

- **Modal vs modeless** -> **Modal `LecgDialog`.** Everything stays synchronous inside `Execute()`: select/show/isolate run in valid API context with no ExternalEvent plumbing, and the command avoids the `ExternalEventCommand` no-reentrancy-guard debt entirely (`CONCERNS.md` — Fragile Areas). Cost accepted: the user closes the dialog to inspect the model; select/isolate persist after close, so the workflow is isolate → close → inspect → reopen.

## Assumptions confirmed

- `WarningsCommand : RevitCommand` with `ProjectDocumentAvailability`; button added to the existing **Project Health** panel in `src/Core/Ribbon/RibbonService.cs`.
- Service, ViewModel, and View registered in `src/Core/Bootstrapper.cs` following the Name↔NameView convention (service singleton, VM/View transient).
- Grouping/ordering is a pure `LECG.Core` policy (new `Warnings/` folder) taking description/severity/element-id tuples and returning ordered groups with counts — groups sorted by count descending, total count exposed. Unit-tested without Revit (R3).
- Actions map to plain read-only API calls: select = `Selection.SetElementIds`, show = `UIDocument.ShowElements`, isolate = `View.IsolateElementsTemporary` (temporary view mode, reversible, no document write). R6 holds by construction — no `ITransactionService` reference anywhere in command/service/VM.
- Isolating a group replaces any prior temporary isolation; un-isolating uses Revit's own temporary-view-mode control — no custom reset button.
- R7 (skip-and-continue) lives in the Revit-side warnings-reading service: a failure reading one `FailureMessage` logs a warning and continues; tested at service/VM level with a faked reader.

## Constraints

- No document writes anywhere in the flow (R6); `ITransactionService` must not appear in the dependency graph of the new types.
- `LECG.Core` stays pure — no Revit API types in the grouping policy (`.planning/codebase/MAP.md` — LECG.Core policy).
- Ribbon buttons reference command classes by **name string** (`RibbonService.cs`) — the button registration and class name must match exactly.
- View derives from `LecgDialog` (`src/Views/Base/`) — theme scoping guard lives there; never touch `Application.Current.Resources`.
- Validation commands: `dotnet build -p:SkipRevitDeploy=true` · `dotnet test -c Debug -p:SkipRevitDeploy=true` (211 green at milestone open).

## Out of scope

- Auto-fixing or dismissing warnings (read-only by design).
- Persisting or exporting warning reports.
- Purge flows (`PurgeExtendedElementService` already shipped).
- The `ExternalEventCommand` reentrancy guard — not needed by the modal choice; remains accepted debt.
- Live Revit validation (R8) — Phase 2.

## Open

- Whether `ShowElements` / view refresh behaves well behind a modal WPF dialog — cheap to verify in Phase 2's smoke test; if it misbehaves, fallback is closing the dialog before showing.
