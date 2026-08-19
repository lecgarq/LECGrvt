---
state_version: 1.0
milestone: batch-rename-ux
milestone_name: Batch Rename UX
status: planning
stopped_at: Phase 1 context gathered — plan not yet written
last_updated: "2026-08-18"
last_activity: 2026-08-18 — /lecg-discuss 1: R12 and R14 restated, R5 key corrected; CONTEXT.md written
progress:
  total_phases: 4
  completed_phases: 0
---

# Project State

## Current Position

Milestone: Batch Rename UX — make the Batch Rename dialog usable on a real-sized model: filter-aware selection and counts, filtering that reaches every column, a preview that stays responsive and keeps the user's checkboxes, and a layout that collapses to give the grid room.
Phase: 1 of 4 (Preview pipeline — stops losing state, stops blocking)
Status: planning. Phase 1 CONTEXT.md written (`.planning/phases/01-preview-pipeline/CONTEXT.md`); PLAN.md not yet written. Nothing built.

## Context

- The command is **Batch Rename**: ribbon `btnBatchRename` (`src/Configuration/UIConstants.cs:55-57`) → `SearchReplaceCommand` → `SearchReplaceView.xaml` (501 lines) + `SearchReplaceViewModel.cs` (269 lines).
- **The engine is out of scope.** `BatchRenameExecutionService` (1019 lines) already does cross-batch collision detection, per-parameter `SubTransaction` rollback, formula rewriting and dimension-label reassignment. This milestone is interaction-layer only.
- **Decided 2026-08-18:** scope stays single-select. The nine pills currently behave as a radio group (`SetExclusiveScope`, `SearchReplaceViewModel.cs:198-207`); R19 makes the control honest rather than making it genuinely multi-scope, which would reach into `CollectBaseElements`, the nine-bool criteria shape, and the per-type rename dispatch.
- **Already-built, unreachable:** `SetColumnFilter` + the `ICollectionView` filter/sort plumbing exist and no XAML calls them (`SearchReplaceViewModel.cs:118-170`). Phase 3 is largely wiring, not building — check before writing anything new.
- **Grid virtualization is already on** (`src/Controls/LecgDataGrid.cs:23-26`). R13/R14 concern the preview *rebuild* (`Clear()` + N× `Add()`), not scrolling.
- **R12 and R14 were restated during /lecg-discuss 1**, and R5's key corrected. `CollectBaseElements` is pure Revit API and cannot leave the UI thread — R12 is now cache-per-scope plus an honest busy state. R14 measures the largest real model available with its row count recorded, not an assumed 5,000. R5 keys on `(Type, Id, OriginalValue)`, because FamilyParameter rows share one `Id` per family (`BaseElementCollectionService.cs:264`).
- Blast radius: 5 tests in `BaseElementCollectionServiceTests` / `SearchReplaceServiceTests` skip outside Revit and sit in this milestone's path. A green suite does not mean those paths are covered.
- View constraint: `SearchReplaceView.xaml:15-22` declares its own `Resources` block, which **replaces** the one `LecgWindow`'s constructor populates. The `LecgTheme.xaml` merge inside it is load-bearing — remove it and every `StaticResource` fails at runtime, invisible to both compiler and tests.
- Sanctioned validation: `dotnet build -p:SkipRevitDeploy=true` · `dotnet test -c Debug -p:SkipRevitDeploy=true` (222 passed / 0 failed / 5 skipped, verified 2026-08-18).
- Live Revit access: `mcp-server-for-revit` is registered; requires Revit open with the plugin's MCP service toggled on (off by default after every Revit restart). It can drive service paths and document queries — it **cannot** confirm dialog rendering, bindings, or any interaction gesture. Every phase here needs eyes on Revit.

## Previous milestone

`warnings-review` closed as-is on 2026-08-18 — 6 of 8 requirements met, 2 partial, 0 unmet. Audit at `.planning/archive/warnings-review/AUDIT.md`. Its open debt (the Warnings dialog was never fully smoke-tested by a human) is recorded in `.planning/codebase/CONCERNS.md`.
