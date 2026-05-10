---
phase: 03-grid-collection-fixes
plan: 06
subsystem: ui-grid-migration
tags: [REQ-01, ElementGridControl, ElementRowViewModel, ElementLabelService, ViewModel]
requires:
  - 03-01-SUMMARY (ElementLabelService.GetLabels)
  - 03-02-SUMMARY (ElementRowViewModel)
  - 03-05-SUMMARY (ElementGridControl shared UserControl)
provides:
  - DivideToposolidViewModel.RowItems: ObservableCollection<ElementRowViewModel>
  - FixPointsViewModel.RowItems: ObservableCollection<ElementRowViewModel>
  - ConvertCadViewModel.RowItems: ObservableCollection<ElementRowViewModel>
affects:
  - DivideToposolidView.xaml
  - FixPointsView.xaml
  - ConvertCadView.xaml
tech-stack:
  added: []
  patterns:
    - "Per-VM SetSelectedElements/SetSelection populates RowItems via ElementLabelService.GetLabels"
    - "View binds <controls:ElementGridControl RowItems=\"{Binding RowItems}\" />"
    - "Per-screen Status column derivation (ResolveStatus / ResolveStatus(element) / type-name)"
key-files:
  created: []
  modified:
    - src/ViewModels/DivideToposolidViewModel.cs
    - src/Views/DivideToposolidView.xaml
    - src/ViewModels/FixPointsViewModel.cs
    - src/Views/FixPointsView.xaml
    - src/ViewModels/ConvertCadViewModel.cs
    - src/Views/ConvertCadView.xaml
decisions:
  - "DivideToposolid Status carries layer-count outcome: 'Ready to divide (N layers)' / 'Already single layer' / 'Layer count unavailable'"
  - "FixPoints Status carries 'Level: {name}' to preserve the per-element level info the legacy DescribeElement string showed"
  - "ConvertCad has no legacy SelectedElementSummaries (single-selection screen); RowItems is populated on SetSelection with one row whose Status carries the import-instance type name (information the user previously saw via the auto-derived NewFamilyName)"
  - "ConvertCad grid lives only inside the Selection-mode StackPanel — File-mode flow has no Element to render so the grid stays hidden via SelectionVisibility"
metrics:
  duration: 3min
  tasks: 2
  files: 6
  completed_date: "2026-05-09"
---

# Phase 03 Plan 06: Text-summary Screens Batch A Summary

Migrated DivideToposolid, FixPoints, and ConvertCad from `ObservableCollection<string> SelectedElementSummaries` (or, for ConvertCad, no per-element list at all) to `ObservableCollection<ElementRowViewModel> RowItems` rendered via the shared `ElementGridControl`. Each row's Name and Category come from `ElementLabelService.GetLabels` so the no-blanks invariant holds; per-screen Status column carries the outcome text that previously lived inside the pipe-delimited summary string.

## Status-column content per migrated screen

| Screen | Status content | Source |
| --- | --- | --- |
| DivideToposolid | `Ready to divide (N layers)` / `Already single layer` / `Layer count unavailable` | `TryGetLayerCount(element)` via `HostObjAttributes.GetCompoundStructure().GetLayers().Count` |
| FixPoints | `Level: {levelName}` (or `Level: No Level`) | `LEVEL_PARAM` / `SCHEDULE_LEVEL_PARAM` lookup, same logic as legacy `DescribeElement` |
| ConvertCad | `Type: {typeName}` (or `Ready to convert` if type missing) | `e.GetTypeId()` -> `Document.GetElement(typeId).Name`, same path that derives `NewFamilyName` |

## Per-screen quirks encountered

- **DivideToposolid:** Layer count required `HostObjAttributes` cast and a try/catch over Revit-API exceptions (`Autodesk.Revit.Exceptions.ArgumentException` / `InvalidOperationException`) — preserved the existing pre-flight exception filter intact.
- **FixPoints:** Legacy `DescribeElement` already had a non-blank guard for Name (`string.IsNullOrWhiteSpace(element.Name) ? category : element.Name`); replaced wholesale by `ElementLabelService.GetLabels` which provides the same guarantee centrally with the additional non-English Revit fallback (`ALL_MODEL_TYPE_NAME`).
- **ConvertCad:** No legacy `SelectedElementSummaries` existed — this screen is single-select and previously surfaced the picked element only through the shared `SelectionControl` count + auto-derived `NewFamilyName`. Added a one-row grid below `SelectionControl` so users can verify the Name/Category they actually picked. The grid is gated by `SelectionVisibility` so File-mode (DWG path) hides it. ConvertCad's separate `Logs` `ObservableCollection<string>` is operational/runtime log output, not an element summary, so it is **not** in scope for this REQ-01 migration.

## Deviations from Plan

### Auto-fixed Issues

None.

### Plan ambiguity resolved

**1. ConvertCad has no `SelectedElementSummaries` to migrate**
- **Found during:** Task 2
- **Issue:** Plan template assumed all three screens followed the DivideToposolid pattern; ConvertCad is a single-selection screen with no per-element list.
- **Resolution:** Honoured the `must_haves.artifacts` contract (`ConvertCadViewModel.cs provides RowItems: ObservableCollection<ElementRowViewModel>`) by populating RowItems with the single picked element on `SetSelection`, and added `ElementGridControl` to the Selection-mode panel only. Status carries the type name that already drives `NewFamilyName` so no information visible to the user has been lost.
- **Files modified:** `src/ViewModels/ConvertCadViewModel.cs`, `src/Views/ConvertCadView.xaml`
- **Commit:** 6a81089

## Verification

- `dotnet build LECG.sln --no-restore -p:SkipRevitDeploy=true` -> Build succeeded, 0 Warning(s), 0 Error(s)
- `dotnet test LECG.Tests/LECG.Tests.csproj --no-restore --no-build` -> Passed: 100, Failed: 0, Skipped: 0
- `grep "ObservableCollection<string> SelectedElementSummaries" src/ViewModels/{DivideToposolid,FixPoints,ConvertCad}ViewModel.cs` -> no hits (done-criterion satisfied)
- Manual Revit verification deferred to Plan 03-09 (consolidated phase-end checkpoint), per plan body.

## Commits

- `1e03483` feat(03-06): migrate DivideToposolid + FixPoints to ElementGridControl
- `6a81089` feat(03-06): migrate ConvertCad to ElementGridControl

## Self-Check: PASSED

- FOUND: src/ViewModels/DivideToposolidViewModel.cs (RowItems exposed)
- FOUND: src/Views/DivideToposolidView.xaml (controls:ElementGridControl bound)
- FOUND: src/ViewModels/FixPointsViewModel.cs (RowItems exposed)
- FOUND: src/Views/FixPointsView.xaml (controls:ElementGridControl bound)
- FOUND: src/ViewModels/ConvertCadViewModel.cs (RowItems exposed)
- FOUND: src/Views/ConvertCadView.xaml (controls:ElementGridControl bound)
- FOUND: commit 1e03483
- FOUND: commit 6a81089
