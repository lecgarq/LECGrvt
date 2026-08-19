---
state_version: 1.0
milestone: batch-rename-ux
milestone_name: Batch Rename UX
status: executing
stopped_at: Phase 1 steps 1–5 complete; steps 6 (live measurement + runtime check) blocked on Revit being open
last_updated: "2026-08-18"
last_activity: 2026-08-18 — phase 1 steps 1–5 shipped (R5, R12, R13, R14a). Suite 234 passed / 0 failed / 5 skipped
progress:
  total_phases: 4
  completed_phases: 0
---

# Project State

## Current Position

Milestone: Batch Rename UX — make the Batch Rename dialog usable on a real-sized model: filter-aware selection and counts, filtering that reaches every column, a preview that stays responsive and keeps the user's checkboxes, and a layout that collapses to give the grid room.
Phase: 1 of 4 (Preview pipeline — stops losing state, stops blocking)
Status: **code complete, not done.** Steps 1–5 shipped and committed; step 6 (R14b live measurement, R20 runtime check) is blocked on Revit being open. The phase cannot be closed until those run — see the pending list in `PLAN.md`.

## Context

- The command is **Batch Rename**: ribbon `btnBatchRename` (`src/Configuration/UIConstants.cs:55-57`) → `SearchReplaceCommand` → `SearchReplaceView.xaml` (501 lines) + `SearchReplaceViewModel.cs` (269 lines).
- **The engine is out of scope.** `BatchRenameExecutionService` (1019 lines) already does cross-batch collision detection, per-parameter `SubTransaction` rollback, formula rewriting and dimension-label reassignment. This milestone is interaction-layer only.
- **Decided 2026-08-18:** scope stays single-select. The nine pills currently behave as a radio group (`SetExclusiveScope`, `SearchReplaceViewModel.cs:198-207`); R19 makes the control honest rather than making it genuinely multi-scope, which would reach into `CollectBaseElements`, the nine-bool criteria shape, and the per-type rename dispatch.
- **Already-built, unreachable:** `SetColumnFilter` + the `ICollectionView` filter/sort plumbing exist and no XAML calls them (`SearchReplaceViewModel.cs:118-170`). Phase 3 is largely wiring, not building — check before writing anything new.
- **Grid virtualization is already on** (`src/Controls/LecgDataGrid.cs:23-26`). R13/R14 concern the preview *rebuild* (`Clear()` + N× `Add()`), not scrolling.
- **R12 and R14 were restated during /lecg-discuss 1**, and R5's key corrected. `CollectBaseElements` is pure Revit API and cannot leave the UI thread — R12 is now cache-per-scope plus an honest busy state. R14 measures the largest real model available with its row count recorded, not an assumed 5,000. R5 keys on `(Type, Id, OriginalValue)`, because FamilyParameter rows share one `Id` per family (`BaseElementCollectionService.cs:264`).
- Blast radius: 5 tests in `BaseElementCollectionServiceTests` / `SearchReplaceServiceTests` skip outside Revit and sit in this milestone's path. A green suite does not mean those paths are covered.
- View constraint: `SearchReplaceView.xaml:15-22` declares its own `Resources` block, which **replaces** the one `LecgWindow`'s constructor populates. The `LecgTheme.xaml` merge inside it is load-bearing — remove it and every `StaticResource` fails at runtime, invisible to both compiler and tests.
- Sanctioned validation: `dotnet build -p:SkipRevitDeploy=true` · `dotnet test -c Debug -p:SkipRevitDeploy=true` (**234 passed / 0 failed / 5 skipped**, 2026-08-18 after phase 1).
- **Shipped in phase 1:** `BulkObservableCollection<T>` (`src/ViewModels/Components/`) replaces the preview rows with one `Reset` instead of N `Add`s; check-state memory keyed `(Type, Id, OriginalValue)` remembers deselections for the life of the dialog; elements are cached per scope with a busy panel on the first collect of each.
- **Measured 2026-08-18:** `ProcessPreview` is 8 ms for 5,000 rows — it was never the bottleneck. The cost was the UI rebuild and `CollectBaseElements`, which is where phase 1 aimed. Do not spend effort optimising `ProcessPreview`.
- **New gotcha, recorded in `docs/ai/revit-protocol.md`:** a null guard does not stop the JIT loading Revit types. Any method body naming a Revit-typed interface loads it when JITed, before the guard runs — so a VM property setter that reaches such a service is untestable even with the service null. Fix pattern: extract the logic into a pure static taking primitives (`SearchReplaceViewModel.BuildScopeKey`).
- **Known limitation carried forward:** `ProcessPreview` computes cross-batch collisions from `IsChecked` before restored deselections are applied, so an unchecked row still claims its name and can mark another row as colliding. Display-only; execution renames checked rows only. Marked with a `ponytail:` comment at the call site.
- Live Revit access: `mcp-server-for-revit` is registered; requires Revit open with the plugin's MCP service toggled on (off by default after every Revit restart). It can drive service paths and document queries — it **cannot** confirm dialog rendering, bindings, or any interaction gesture. Every phase here needs eyes on Revit.

## Previous milestone

`warnings-review` closed as-is on 2026-08-18 — 6 of 8 requirements met, 2 partial, 0 unmet. Audit at `.planning/archive/warnings-review/AUDIT.md`. Its open debt (the Warnings dialog was never fully smoke-tested by a human) is recorded in `.planning/codebase/CONCERNS.md`.
