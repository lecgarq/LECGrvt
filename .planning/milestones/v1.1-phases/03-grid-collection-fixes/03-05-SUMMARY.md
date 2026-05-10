---
phase: 03-grid-collection-fixes
plan: 05
subsystem: ui
tags: [batch-rename, wpf, datagrid, icollectionview, mvvm, element-grid-control, sort-filter]

requires:
  - phase: 03-grid-collection-fixes
    provides: ElementRowViewModel shared row model (Plan 03-02)
  - phase: 03-grid-collection-fixes
    provides: SearchReplacePreviewService -> ElementRowViewModel migration (Plan 03-04)
provides:
  - "ElementGridControl shared WPF UserControl (Sel | Type | Category | Name | Status) with RowItems/AdditionalColumns/SelectionMode dependency properties"
  - "SearchReplaceViewModel.PreviewView ICollectionView with Category-ascending default sort and AND-combined filter (FilterCategory dropdown + per-column predicates)"
  - "Batch Rename grid column order locked: ☑ | Type | Category | Original | New (visual-verified in Revit)"
  - "SetColumnFilter(string, Predicate) API on SearchReplaceViewModel for per-column filter wiring"
affects:
  - 03-06 (text-summary screen migrations consume ElementGridControl)
  - 03-07 (text-summary screen migrations batch B)
  - 03-08 (selection-backed screen sweep)
  - 03-09 (phase-end Revit verification — REQ-01 acceptance)

tech-stack:
  added: []
  patterns:
    - "ICollectionView default sort + filter wiring via CollectionViewSource.GetDefaultView on the VM-owned ObservableCollection"
    - "ICollectionView.Filter as the AND of (a) top-of-grid dropdown predicate and (b) named per-column predicate dictionary"
    - "Shared UserControl wrapping LecgDataGrid for cross-screen grid reuse without restyling"
    - "Batch-Rename keeps an inline LecgDataGrid (not ElementGridControl) because Original/New columns don't fit the generic shape"

key-files:
  created:
    - src/Controls/ElementGridControl.xaml
    - src/Controls/ElementGridControl.xaml.cs
  modified:
    - src/ViewModels/SearchReplaceViewModel.cs
    - src/Views/SearchReplaceView.xaml
    - LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs

key-decisions:
  - "Batch Rename keeps inline LecgDataGrid (not ElementGridControl) because Original/New columns don't fit the generic Sel|Type|Category|Name|Status shape; ElementGridControl is reserved for the migration sweep in plans 03-06..08"
  - "Per-column filter exposed as SetColumnFilter(name, predicate) API on the ViewModel rather than full WPF DataGrid header funnel chrome; visual funnel UI deferred to a v1.2 follow-up"
  - "ICollectionView wrapping owned by the ViewModel (PreviewView) rather than by ElementGridControl, so each screen owns its own sort/filter semantics"
  - "FilterCategory predicate moved out of SearchReplacePreviewService.ProcessPreview (no longer re-runs preview on filter change); collection layer (Plan 03-03) owns invariants, ICollectionView owns post-collection filtering"

patterns-established:
  - "ViewModel exposes ObservableCollection<T> as Items AND ICollectionView as ItemsView; XAML binds ItemsSource to the view to honor SortDescriptions/Filter"
  - "AND-combined filter: top-of-grid dropdown predicate AND-folded with a Dictionary<string, Predicate<TRow>> of column-level predicates registered via SetColumnFilter"
  - "ElementGridControl as the canonical shared grid for screens whose row shape matches Sel|Type|Category|Name|Status"

requirements-completed: [REQ-01]

duration: ~45min
completed: 2026-05-09
---

# Phase 03 Plan 05: ElementGridControl + Batch Rename Sort/Filter Summary

**Shared `ElementGridControl` UserControl shipped, Batch Rename grid wired to an ICollectionView with Category-ascending default sort and AND-combined filtering (FilterCategory dropdown + per-column predicate API), and visual correctness confirmed by user in Revit.**

## Performance

- **Duration:** ~45 min (across two agent sessions due to checkpoint pause)
- **Tasks:** 3 (2 auto + 1 human-verify checkpoint)
- **Files created:** 2
- **Files modified:** 3

## Accomplishments
- New shared `ElementGridControl` WPF UserControl rendering the standard `Sel | Type | Category | Name | Status` column set, wrapping `LecgDataGrid` so existing styling is inherited.
- `SearchReplaceViewModel.PreviewView` ICollectionView wraps `PreviewItems`, defaulting to `Category` ascending and AND-combining the `FilterCategory` dropdown with per-column predicates registered via `SetColumnFilter(string, Predicate<ElementRowViewModel>)`.
- `SearchReplaceView.xaml` rebound to `PreviewView` and column order locked to ☑ | Type | Category | Original | New per CONTEXT.md.
- `FilterCategory` filtering moved out of `SearchReplacePreviewService.ProcessPreview` (no longer re-runs preview on filter change).
- Two `3-W0-04` `SearchReplaceViewModelTests` flipped from Skip to GREEN; full unit suite 100/100 GREEN; `dotnet build LECG.sln -p:SkipRevitDeploy=true` clean (0 errors / 0 warnings).
- User performed in-Revit visual verification: Batch Rename grid column order, Category-asc default sort, sort flip, FilterCategory dropdown, no blank rows, FamilyParameter scope all confirmed correct.

## Task Commits

1. **Task 1: Build ElementGridControl UserControl** — `bbe1d67` (feat)
2. **Task 2: Wire SearchReplaceView Category column + ICollectionView** — `163653b` (feat)
3. **Task 3: Visual checkpoint — Batch Rename grid in Revit** — `fc2265c` (docs, approval marker)

## Files Created/Modified
- `src/Controls/ElementGridControl.xaml` — shared `LECG.Controls.ElementGridControl` UserControl wrapping `LecgDataGrid`, columns `Sel | Type | Category | Name | Status`.
- `src/Controls/ElementGridControl.xaml.cs` — `RowItems` (IEnumerable), `AdditionalColumns`, and `SelectionMode` dependency properties.
- `src/ViewModels/SearchReplaceViewModel.cs` — added `PreviewView` ICollectionView, `_columnFilters` dictionary, `MatchesAllFilters` predicate, `SetColumnFilter` API, `OnFilterCategoryChanged` partial calls `PreviewView.Refresh()`. `FilterCategory` filtering removed from preview pipeline.
- `src/Views/SearchReplaceView.xaml` — DataGrid rebound to `PreviewView`; columns locked to ☑ | Type | Category | Original | New with `SortMemberPath` set per column.
- `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs` — `3-W0-04` tests un-Skipped; assertions cover (a) Category-asc default sort, (b) FilterCategory + per-column AND, (c) `SetColumnFilter` API behavior.

## Decisions Made
- **Batch Rename keeps inline LecgDataGrid (not ElementGridControl):** the Batch Rename row shape includes `Original`/`New` editing columns that don't fit the generic `Sel | Type | Category | Name | Status` template. `ElementGridControl` is reserved for the migration sweep in Plans 03-06..08 where row shape matches.
- **Per-column filter API instead of full WPF funnel chrome:** Task 1's note explicitly allowed scoping down. We chose option (c): expose `SetColumnFilter(name, predicate)` on the ViewModel and prove the wiring with a unit test. The visual header funnel chrome (popup with distinct-value checkboxes) is deferred to a v1.2 follow-up — see Open Items below.
- **ICollectionView ownership lives on the ViewModel, not the control:** keeps each screen's sort/filter semantics local to its VM and avoids the control prescribing them.
- **No Revit API in the Filter lambda:** RESEARCH §Anti-Patterns guard is honored — predicates only read `ElementRowViewModel` fields.

## Deviations from Plan

None — plan executed as written. The per-column funnel-chrome scope-down is explicitly allowed by Task 1's action notes (option c) and is documented as an Open Item below.

## Issues Encountered
None.

## Open Items / Deferred
- **Per-column header funnel chrome (visual UI) deferred.** The functional plumbing is complete: `SetColumnFilter(string columnName, Predicate<ElementRowViewModel>)` is exercised by a unit test that confirms AND-combination with `FilterCategory`. What's deferred is the WPF header template that surfaces a popup of distinct values per column. Tracked for v1.2 follow-up. No REQ-01 acceptance impact since the plan's success criteria are met by the dropdown + programmatic per-column filter and were visually verified.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- Plan 03-06 unblocked: `ElementGridControl` is available for the text-summary screen migrations (DivideToposolid, FixPoints, ConvertCad).
- Plans 03-07, 03-08 can proceed in sequence using the same control.
- Phase-end Revit verification (Plan 03-09) inherits today's user-confirmed visual baseline for Batch Rename.

## Self-Check: PASSED

Verification of claims:
- `bbe1d67` exists in git log: FOUND
- `163653b` exists in git log: FOUND
- `fc2265c` exists in git log: FOUND
- `src/Controls/ElementGridControl.xaml`: FOUND
- `src/Controls/ElementGridControl.xaml.cs`: FOUND
- `src/ViewModels/SearchReplaceViewModel.cs`: present (modified)
- `src/Views/SearchReplaceView.xaml`: present (modified)
- `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs`: present (modified, tests un-Skipped)
- Build: clean (0 errors / 0 warnings) per prior agent's `dotnet build -p:SkipRevitDeploy=true`
- Tests: 100/100 GREEN per prior agent
- Visual verification: user-approved in Revit (column order, default sort, sort flip, FilterCategory, no blanks, FamilyParameter scope)

---
*Phase: 03-grid-collection-fixes*
*Completed: 2026-05-09*
