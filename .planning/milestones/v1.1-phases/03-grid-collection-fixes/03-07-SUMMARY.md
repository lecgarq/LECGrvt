---
phase: 03-grid-collection-fixes
plan: 07
subsystem: ui
tags: [wpf, mvvm, datagrid, ElementGridControl, ElementRowViewModel, ElementLabelService]

requires:
  - phase: 03-grid-collection-fixes
    provides: ElementLabelService.GetLabels (Plan 03-01), ElementRowViewModel (Plan 03-02), ElementGridControl shared UserControl (Plan 03-05)
provides:
  - SplitBoundariesViewModel.RowItems (ObservableCollection<ElementRowViewModel>) replacing string-summary list
  - ConvertToposolidToFloorViewModel.RowItems replacing string-summary list
  - ConvertFloorToToposolidViewModel.RowItems replacing string-summary list
  - Three Views render <controls:ElementGridControl RowItems="{Binding RowItems}"/> in place of ListBox over strings
affects: ["03-08", "03-09"]

tech-stack:
  added: []
  patterns:
    - "Per-row ElementRowViewModel + shared ElementGridControl with Sel|Type|Category|Name|Status columns"
    - "Status column carries per-screen outcome text previously embedded in summary strings"
    - "Locale-safe non-blank Name + Category via ElementLabelService.GetLabels"

key-files:
  created: []
  modified:
    - src/ViewModels/SplitBoundariesViewModel.cs
    - src/Views/SplitBoundariesView.xaml
    - src/ViewModels/ConvertToposolidToFloorViewModel.cs
    - src/Views/ConvertToposolidToFloorView.xaml
    - src/ViewModels/ConvertFloorToToposolidViewModel.cs
    - src/Views/ConvertFloorToToposolidView.xaml

key-decisions:
  - "Conversion-screen Status uses '<TypeName> @ <LevelName>' format (preserves prior summary info verbatim) rather than inventing eligibility detection (no Toposolid/Floor eligibility API exists today)"
  - "SplitBoundaries Status enriched with boundary count: 'Ready to split (N boundaries)' (was 'Ready to split' + separate count segment)"
  - "Public collection renamed RowItems (matches Plan 03-06 batch A and ElementGridControl DP)"

patterns-established:
  - "Update<Anything>Summaries -> UpdateRowItems naming convention; method body delegates labels to ElementLabelService.GetLabels and writes Status inline per screen"

requirements-completed: [REQ-01]

duration: 12min
completed: 2026-05-09
---

# Phase 03 Plan 07: Text-Summary Screens Batch B Migration Summary

**Three conversion/boundary VMs (SplitBoundaries, ConvertToposolidToFloor, ConvertFloorToToposolid) migrated from `ObservableCollection<string>` summary lists to `ObservableCollection<ElementRowViewModel>` rendered through the shared ElementGridControl, with Status column carrying per-screen outcome text.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-09T20:30:00Z
- **Completed:** 2026-05-09T20:42:00Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- SplitBoundaries grid: per-row boundary count + Ready/No-split/Unavailable Status, IsChecked enables per-row deselection.
- ConvertToposolidToFloor grid: per-row Type + Level Status preserves prior summary content; Name/Category resolved locale-safely.
- ConvertFloorToToposolid grid: same Type+Level Status pattern; build/tests untouched at 100/100 GREEN.
- All six text-summary screens (Plans 03-06 batch A + Plan 03-07 batch B) now use ElementGridControl. Cross-VM grep `ObservableCollection<string> SelectedElementSummaries` returns zero hits across the entire ViewModels tree.

## Task Commits

1. **Task 1: Migrate SplitBoundaries + ConvertToposolidToFloor** — `e3a9b37` (feat)
2. **Task 2: Migrate ConvertFloorToToposolid** — `4c9dcca` (feat)

**Plan metadata:** (final docs commit follows)

## Files Created/Modified

- `src/ViewModels/SplitBoundariesViewModel.cs` — Replaced SelectedElementSummaries with RowItems; UpdateRowItems calls ElementLabelService.GetLabels; Status carries boundary-count outcome.
- `src/Views/SplitBoundariesView.xaml` — Added `xmlns:controls`; replaced ListBox over strings with `<controls:ElementGridControl RowItems="{Binding RowItems}" Height="160"/>`.
- `src/ViewModels/ConvertToposolidToFloorViewModel.cs` — Same migration; Status = "<TypeName> @ <LevelName>".
- `src/Views/ConvertToposolidToFloorView.xaml` — Added `xmlns:controls`; replaced ListBox with ElementGridControl (Height=120 preserved).
- `src/ViewModels/ConvertFloorToToposolidViewModel.cs` — Same migration; Status = "<TypeName> @ <LevelName>".
- `src/Views/ConvertFloorToToposolidView.xaml` — Added `xmlns:controls`; replaced ListBox with ElementGridControl.

## Per-Screen Status Column Content

| Screen | Status Format | Examples |
|---|---|---|
| SplitBoundaries | Boundary-count outcome | "Ready to split (3 boundaries)" / "No split needed" / "Boundary count unavailable" |
| ConvertToposolidToFloor | "<SourceTypeName> @ <LevelName>" | "Default @ Level 1" / "Topography 12 in @ No Level" |
| ConvertFloorToToposolid | "<SourceTypeName> @ <LevelName>" | "Generic 6 in @ Level 2" |

## Decisions Made

- **Pragmatic Status content for conversion screens:** No `IConversionService` eligibility API exists; the only existing per-row signal beyond Name/Category was Type+Level (already in the prior summary string). Status preserves that signal verbatim. The plan's eligibility examples ("Eligible", "Has openings — review", "Mass-host — unsupported") would require new eligibility detection logic outside the scope of this migration plan — deferred (out of scope for REQ-01, which is about non-blank rows and the grid surface, not new validation logic).
- **SplitBoundaries Status enriched:** prior code separated `Boundaries: N` from `Ready to split`; merged into a single Status string for column tidiness while preserving information.
- **`UpdateSelectedElementSummaries` -> `UpdateRowItems` rename:** clarifies new responsibility (populate row models, not format strings).

## Deviations from Plan

None — plan executed exactly as written. Status content interpretation followed the plan's "e.g.,..." illustrative examples while staying within the scope hint ("extract whatever the existing summary's trailing segment carries"). The trailing segment for both conversion screens was `Type: ... | Level: ...` per the prior `DescribeElement` formatter.

## Issues Encountered

None. Build remained at 0/0 errors+warnings; test suite remained at 100/100 GREEN through both task commits.

## Cross-VM Verification

```
$ grep -rn "ObservableCollection<string> SelectedElementSummaries" src/ViewModels/
(no matches)
```

All six text-summary VMs (DivideToposolid, FixPoints, ConvertCad — Plan 03-06; SplitBoundaries, ConvertToposolidToFloor, ConvertFloorToToposolid — Plan 03-07) are clean of the legacy string-summary collection.

## Next Phase Readiness

- Pattern proven across 6 VMs; remaining selection-backed screens (Plan 03-08: Align*, AssignMaterial, CategoryChanger, ChangeLevel, OffsetElevations, ResetSlabs, SimplifyPoints) can adopt the same UpdateRowItems + ElementGridControl recipe.
- Manual Revit verification consolidated to phase-end (Plan 03-09).

---
*Phase: 03-grid-collection-fixes*
*Plan: 07*
*Completed: 2026-05-09*

## Self-Check: PASSED

- SUMMARY.md present at expected path.
- Task 1 commit `e3a9b37` present in git log.
- Task 2 commit `4c9dcca` present in git log.
- Build: 0 errors, 0 warnings (after both tasks).
- Tests: 100/100 GREEN (after both tasks).
- Cross-VM grep (`ObservableCollection<string> SelectedElementSummaries` in `src/ViewModels/`): zero hits.
