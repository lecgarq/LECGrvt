---
phase: 04-advanced-renaming-logic
plan: "02"
subsystem: Batch Rename / Preview
tags: [preview, status, IsRenameable, XAML, side-effects, cross-batch-collision]
dependency_graph:
  requires: [04-00, 04-01]
  provides: [ProcessPreview Status+IsRenameable population, SearchReplaceView muted-row style]
  affects: [SearchReplaceView.xaml, SearchReplacePreviewService, ElementData, BatchRenameExecutionService]
tech_stack:
  added: [FormulaNameUpdater.ContainsReference for preview formula scan]
  patterns: [ElementData metadata flags (Formula, IsDimensionLabel), claimed-NewValue HashSet, DataTrigger muted-row style]
key_files:
  created: []
  modified:
    - src/Services/Renaming/SearchReplacePreviewService.cs
    - src/Views/SearchReplaceView.xaml
    - LECG.Tests/Services/SearchReplacePreviewServiceTests.cs
    - src/Services/Renaming/SearchReplaceService.cs
    - src/Services/Renaming/BatchRenameExecutionService.cs
decisions:
  - "ElementData.Formula + IsDimensionLabel fields carry side-effect metadata from collection to preview without Revit API"
  - "Preview skip detection uses IsReadOnly as proxy for read-only/reporting FamilyParameter conditions"
  - "Cross-batch collision scoped per (Type='FamilyParameter', Id) for family params; per Type for standard items"
  - "TextMutedBrush used for muted-row Foreground (project convention from Brushes.xaml)"
  - "Sel column converted from DataGridCheckBoxColumn to DataGridTemplateColumn to support IsEnabled binding"
  - "Status column added at end of grid (after ID) — does not change existing column order"
metrics:
  duration: 35min
  completed: 2026-05-10
  tasks_completed: 2
  files_modified: 5
---

# Phase 4 Plan 02: Preview Status + IsRenameable Population Summary

Status/IsRenameable surfaced inline in Batch Rename grid for FamilyParameter rows; skipped rows are visually muted with disabled checkboxes.

## What Was Built

### Task 1: ProcessPreview Status + IsRenameable Population

**SearchReplacePreviewService** (`src/Services/Renaming/SearchReplacePreviewService.cs`) was extended with two new private static methods:

**`PopulateFamilyParameterStatus(row, el, allCandidates)`**
- Checks `el.IsReadOnly` → sets `Status = "read-only parameter (cannot rename)"`, `IsRenameable = false`, `IsChecked = false`
- Otherwise: counts formula references by scanning other candidates in the same family scope (same `Id`) where `Formula` is non-empty and `FormulaNameUpdater.ContainsReference(other.Formula, el.OriginalValue)` returns true
- Counts dimension labels from `el.IsDimensionLabel` (1 or 0)
- Formats Status using `FormatSideEffectStatus(formulaCount, dimensionCount)`:
  - Both nonzero: `"+N formulas, +M dimension[s]"`
  - Formula only: `"+N formula[s]"`
  - Dim only: `"+M dimension[s]"`
  - Neither: `""` (empty)
- `IsRenameable = true`, `IsChecked` left at default

**`ApplyCrossBatchCollisionCheck(rows)`**
- Walks the completed row list after construction
- Scope key: `"FamilyParameter:{Id}"` for FamilyParameter rows, `Type` for standard items
- Maintains `HashSet<string>` (ordinal) of claimed NewValues per scope
- First row claiming a NewValue is kept; second collision flips `IsRenameable = false`, `IsChecked = false`, `Status = "name '{NewValue}' already claimed by another row in this batch"`

**ElementData additions** (`src/Services/Renaming/SearchReplaceService.cs`):
- `Formula string` — formula string from `FamilyParameter.Formula`, used to compute cross-formula reference counts in preview
- `IsDimensionLabel bool` — true if this parameter is a dimension label in the family

### Task 2: SearchReplaceView.xaml Changes

Three targeted changes (no column order or sort behavior altered):

**Change 1 — Sel checkbox `IsEnabled` binding:**
Converted `DataGridCheckBoxColumn` to `DataGridTemplateColumn` containing a `CheckBox` with:
```xml
IsChecked="{Binding IsChecked, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
IsEnabled="{Binding IsRenameable}"
```

**Change 2 — Muted-row Style trigger:**
Added `DataGrid.RowStyle` with `DataTrigger` on `IsRenameable = False`:
```xml
<Setter Property="Opacity" Value="0.45"/>
<Setter Property="Foreground" Value="{DynamicResource TextMutedBrush}"/>
```
`TextMutedBrush` used (defined in `src/Resources/Base/Brushes.xaml` as `ColorTextMuted`). This matches project convention for muted/disabled text.

**Change 3 — Status column with tooltip:**
Added `DataGridTextColumn` for Status at position after ID column:
```xml
<DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="140">
    <DataGridTextColumn.ElementStyle>
        <Style TargetType="TextBlock">
            <Setter Property="ToolTip" Value="{Binding Status}"/>
            <Setter Property="TextTrimming" Value="CharacterEllipsis"/>
        </Style>
    </DataGridTextColumn.ElementStyle>
</DataGridTextColumn>
```

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed BatchRenameExecutionService compile errors from Plan 04-01**
- **Found during:** Task 1 build
- **Issue:** Plan 04-01 introduced `Family.IsSystemFamily` (does not exist in Revit API) and `SubTransaction.Rollback()` (should be `RollBack()`)
- **Fix:** Removed `IsSystemFamily` check (defaulted to `false` with comment for follow-up), fixed `RollBack()` casing
- **Files modified:** `src/Services/Renaming/BatchRenameExecutionService.cs`
- **Commit:** d5153ca

**2. [Rule 3 - Blocking] Added Formula + IsDimensionLabel to ElementData**
- **Found during:** Task 1 implementation
- **Issue:** `ProcessPreview` receives `List<ElementData>` without Revit API objects; formula/dimension data needed for side-effect counts must be embedded in ElementData at collection time
- **Fix:** Added `Formula` and `IsDimensionLabel` to `ElementData` — collection layer populates these, preview uses them without Revit API
- **Files modified:** `src/Services/Renaming/SearchReplaceService.cs`
- **Commit:** d5153ca

## Test Results

| Fixture | Before | After |
|---------|--------|-------|
| SearchReplacePreviewServiceTests | 3 pass, 5 skip | 8 pass, 0 skip |
| RenameSkipDetectorTests (Plan 04-01 fix) | 11 skip + compile error | 11 pass, 0 skip |
| BatchRenameSafeRenameTests | 1 pass, 9 skip | 1 pass, 9 skip |
| Full suite | 100 pass, 0 skip | 121 pass, 10 skip |

The 21 additional passing tests include 5 REQ-04 tests (this plan) + 16 from Plan 04-01 that were blocked by the BatchRenameExecutionService compile error.

## Notes for Follow-up

- `ElementData.Formula` and `IsDimensionLabel` must be populated in `BaseElementCollectionService` when collecting FamilyParameter rows (currently collection does not set them — side-effect counts in live Revit will show 0 until collection is updated)
- `isSystemFamily` detection in `GetStandardItemSkipReason` deferred to v1.2 (defaulted to `false`)
- Visual verification deferred to Plan 04-05 phase-end Revit verify session

## Self-Check: PASSED

- src/Services/Renaming/SearchReplacePreviewService.cs: FOUND
- src/Views/SearchReplaceView.xaml: FOUND
- LECG.Tests/Services/SearchReplacePreviewServiceTests.cs: FOUND
- Commit d5153ca: FOUND
- Commit 10d8502: FOUND
