---
state_version: 1.0
milestone: batch-rename-ux
milestone_name: Batch Rename UX
status: executing
stopped_at: Phase 1 steps 1–6a complete (R14b measured live). Remaining: deploy the new build and run the 5 interactive smoke steps (R20)
last_updated: "2026-08-18"
last_activity: 2026-08-18 — R14b measured live on Snowdon Towers Sample Architectural; worst scope 437 ms, no cancel path needed
progress:
  total_phases: 4
  completed_phases: 0
---

# Project State

## Current Position

Milestone: Batch Rename UX — make the Batch Rename dialog usable on a real-sized model: filter-aware selection and counts, filtering that reaches every column, a preview that stays responsive and keeps the user's checkboxes, and a layout that collapses to give the grid room.
Phase: 1 of 4 (Preview pipeline — stops losing state, stops blocking)
Status: **code complete, not done.** Steps 1–5 shipped and committed; R14b measured live 2026-08-18. Remaining for R20: the deployed add-in is still the **2026-07-28 build** (`C:/ProgramData/Autodesk/Revit/Addins/2026/LECG/LECG.dll`, 996,864 bytes, Jul 28 00:20) and predates this phase, so none of the dialog behaviour has been seen. Deploying needs `dotnet build -c Release` with **Revit closed** — it copies over the DLL Revit holds open. Five interactive steps then remain; see `PLAN.md`.

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
- **Measured 2026-08-18 (synthetic):** `ProcessPreview` is 8 ms for 5,000 rows — it was never the bottleneck. Do not spend effort optimising it.
- **Measured 2026-08-18 (live, Snowdon Towers Sample Architectural — 1,881 types / 37,877 elements / 7,598 FamilyInstances / 7 links):** collector cost per scope is Types 10 ms, Families 7, Views+Sheets 7, Materials 6, FillPatterns 6, and **FamilyParameters 437 ms / 1,171 rows**. Worst case is 437 ms, so **no cancel path is needed**; the busy panel is sufficient. The cache pays off on essentially one scope.
- **Next optimisation candidate (not phase 1):** 86% of the FamilyParameters cost is Phase B, which enumerates all 7,598 `FamilyInstance`s to find instance-only parameters when one instance per family would do. `BaseElementCollectionService` already tracks `processedInstanceFamilies` but still walks every instance.
- **Largest real scope is 1,881 rows**, so the synthetic 5,000-row test is ~2.7× headroom — a regression guard, not a proxy for real load.
- **New gotcha, recorded in `docs/ai/revit-protocol.md`:** a null guard does not stop the JIT loading Revit types. Any method body naming a Revit-typed interface loads it when JITed, before the guard runs — so a VM property setter that reaches such a service is untestable even with the service null. Fix pattern: extract the logic into a pure static taking primitives (`SearchReplaceViewModel.BuildScopeKey`).
- **Known limitation carried forward:** `ProcessPreview` computes cross-batch collisions from `IsChecked` before restored deselections are applied, so an unchecked row still claims its name and can mark another row as colliding. Display-only; execution renames checked rows only. Marked with a `ponytail:` comment at the call site.
- Live Revit access: `mcp-server-for-revit` is registered; requires Revit open with the plugin's MCP service toggled on (off by default after every Revit restart). It can drive service paths and document queries — it **cannot** confirm dialog rendering, bindings, or any interaction gesture. Every phase here needs eyes on Revit.

## Previous milestone

`warnings-review` closed as-is on 2026-08-18 — 6 of 8 requirements met, 2 partial, 0 unmet. Audit at `.planning/archive/warnings-review/AUDIT.md`. Its open debt (the Warnings dialog was never fully smoke-tested by a human) is recorded in `.planning/codebase/CONCERNS.md`.
