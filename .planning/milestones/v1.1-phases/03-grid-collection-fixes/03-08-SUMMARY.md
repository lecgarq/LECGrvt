---
phase: 03-grid-collection-fixes
plan: 08
subsystem: ui
tags: [wpf, mvvm, datagrid, selection, revit-2026]

requires:
  - phase: 03-grid-collection-fixes/01
    provides: ElementLabelService.GetLabels for non-blank Name/Category
  - phase: 03-grid-collection-fixes/02
    provides: ElementRowViewModel shared row model
  - phase: 03-grid-collection-fixes/05
    provides: ElementGridControl shared UserControl
provides:
  - SelectionViewModel.RowItems (ObservableCollection<ElementRowViewModel>)
  - SelectionViewModel.SetSelectionRows(IEnumerable<Reference>?, Document?) overload
  - SelectionViewModel.SetSelectionRows(IEnumerable<Element>?) overload
  - SelectionControl.xaml — embedded ElementGridControl below summary (Path A)
  - 8 selection-backed VMs wired to push rows on every mutation
affects: [03-09 (manual Revit verification), v1.2 follow-up screens]

tech-stack:
  added: []
  patterns:
    - "Path A — shared SelectionControl embeds ElementGridControl; consumers get the grid for free with zero per-View XAML edits"
    - "Dual SetSelectionRows overloads (Reference+Doc, Element) accommodate both ref-driven (7 VMs) and element-driven (ChangeLevel) selection paths"
    - "WPF Dispatcher marshalling inside SetSelectionRows guards against Revit non-UI-thread mutations (RESEARCH §Pitfall 3)"

key-files:
  created: []
  modified:
    - src/ViewModels/Components/SelectionViewModel.cs
    - src/Views/Components/SelectionControl.xaml
    - src/ViewModels/AlignEdgesViewModel.cs
    - src/ViewModels/AlignElementsViewModel.cs
    - src/ViewModels/AssignMaterialViewModel.cs
    - src/ViewModels/CategoryChangerViewModel.cs
    - src/ViewModels/ChangeLevelViewModel.cs
    - src/ViewModels/OffsetElevationsViewModel.cs
    - src/ViewModels/ResetSlabsViewModel.cs
    - src/ViewModels/SimplifyPointsViewModel.cs
    - src/Views/AlignEdgesView.xaml.cs
    - src/Views/AssignMaterialView.xaml.cs
    - src/Views/OffsetElevationsView.xaml.cs
    - src/Views/ResetSlabsView.xaml.cs
    - src/Views/SimplifyPointsView.xaml.cs
    - src/Commands/AlignEdgesCommand.cs
    - src/Commands/AssignMaterialCommand.cs
    - src/Commands/OffsetElevationsCommand.cs
    - src/Commands/ResetSlabsCommand.cs
    - src/Commands/SimplifyPointsCommand.cs

key-decisions:
  - "Path A chosen — embed ElementGridControl directly in SelectionControl.xaml; eliminates 8 per-View XAML edits and guarantees consistency"
  - "Two SetSelectionRows overloads (Reference+Document, Element) — picks the right one per call-site without forcing premature Reference-resolve at the View layer"
  - "Dispatcher.Invoke wrapping inside SetSelectionRows — defends against future Revit ExternalEvent callers without mandating a thread contract on consumers"
  - "Grid Visibility bound to HasSelection — empty-state stays clean (no empty grid below 'No X selected' summary)"
  - "Document parameter added to SetTargets/SetReferences/SetSelection of 5 VMs (AlignEdges, AssignMaterial, OffsetElevations, ResetSlabs, SimplifyPoints); call-sites in 5 Views + 5 Commands updated atomically in Task 2"

patterns-established:
  - "Selection grid embeds in SelectionControl.xaml (single SSoT) — future Selection-backed screens get the grid for free"
  - "VM SetXxxx mutators take (refs, Document) and call Selection.SetSelectionRows after UpdateSelection — uniform across all 7 ref-driven VMs"

requirements-completed: [REQ-01]

duration: 9min
completed: 2026-05-09
---

# Phase 03 Plan 08: Selection-Backed Screen Sweep Summary

**Eight Selection-backed screens (AlignEdges, AlignElements, AssignMaterial, CategoryChanger, ChangeLevel, OffsetElevations, ResetSlabs, SimplifyPoints) now render a per-element grid via shared `SelectionControl` — zero per-View XAML edits required.**

## Performance

- **Duration:** ~9 min
- **Started:** 2026-05-09T23:40:02Z
- **Tasks:** 2
- **Files modified:** 20

## Accomplishments
- `SelectionViewModel` upgraded with `RowItems` + `SetSelectionRows` (two overloads) + Dispatcher-marshalled mutations.
- `SelectionControl.xaml` embeds `ElementGridControl` (Path A) — visible only when `HasSelection`, hides cleanly in the empty state.
- 8 VMs + their View/Command call-sites pump rows on every selection mutation. ChangeLevel uses the Element overload; the other 7 use the Reference+Document overload.
- Build clean (0 errors, 0 warnings); 100/100 unit tests GREEN.

## Task Commits

1. **Task 1: Upgrade shared SelectionViewModel + SelectionControl** — `1b6e1e7` (feat)
2. **Task 2: Sweep 8 selection-backed VMs/Views/Commands** — `7cd895d` (feat)

## Files Created/Modified
- `src/ViewModels/Components/SelectionViewModel.cs` — added `RowItems`, two `SetSelectionRows` overloads (Reference+Document, Element), Dispatcher marshalling, `BuildRow` helper using `ElementLabelService.GetLabels`.
- `src/Views/Components/SelectionControl.xaml` — embedded `ElementGridControl` below the summary text; `BooleanToVisibilityConverter` declared in local resources; height 160, top-margin 8; visibility bound to `HasSelection`.
- 8 VMs (`AlignEdgesViewModel`, `AlignElementsViewModel`, `AssignMaterialViewModel`, `CategoryChangerViewModel`, `ChangeLevelViewModel`, `OffsetElevationsViewModel`, `ResetSlabsViewModel`, `SimplifyPointsViewModel`) — selection mutators now call `Selection.SetSelectionRows`; 5 added a `Document` parameter.
- 5 Views (`AlignEdgesView`, `AssignMaterialView`, `OffsetElevationsView`, `ResetSlabsView`, `SimplifyPointsView`) — pass `UiDocument.Document` into the new mutator signatures.
- 5 Commands (`AlignEdgesCommand`, `AssignMaterialCommand`, `OffsetElevationsCommand`, `ResetSlabsCommand`, `SimplifyPointsCommand`) — preselected-refs path now passes `doc`.

## Decisions Made
- **Path A vs Path B:** Path A. The 8 screens are structurally identical — they all want a vanilla Sel|Type|Category|Name|Status grid with `Status=""`. Embedding once in `SelectionControl.xaml` is strictly cleaner than 8 duplicate `<controls:ElementGridControl ... />` siblings. Path B would have been required only if a screen needed per-screen custom Status semantics (the 03-06/07 batch did, which is why those VMs kept their own `RowItems`).
- **Element overload added:** `ChangeLevel` already resolves refs → `List<Element>` before storing; forcing it through the Reference path would have meant re-resolving. The Element overload keeps the call-site honest.
- **Dispatcher guard:** existing call sites are UI-thread, but the cost is one nullable check; future ExternalEvent integrations will not need to think about it.
- **Visibility binding on the grid:** without it, the empty state shows an empty grid box below the "No X selected" line. Binding to `HasSelection` keeps the UI compact pre-selection.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Cascading Document parameter through 5 Commands**
- **Found during:** Task 2
- **Issue:** Adding `Document` to `SetTargets`/`SetSelection` of 5 VMs broke 5 RevitCommand call-sites (`AlignEdgesCommand`, `AssignMaterialCommand`, `OffsetElevationsCommand`, `ResetSlabsCommand`, `SimplifyPointsCommand`) which call these mutators on the preselected-refs path.
- **Fix:** Updated each Command's preselected-refs call to pass the existing local `doc` parameter. No new Document acquisition needed — every Command already had it from `Execute(uiDoc, doc)`.
- **Files modified:** `src/Commands/AlignEdgesCommand.cs`, `src/Commands/AssignMaterialCommand.cs`, `src/Commands/OffsetElevationsCommand.cs`, `src/Commands/ResetSlabsCommand.cs`, `src/Commands/SimplifyPointsCommand.cs`.
- **Verification:** Build clean; 100/100 tests GREEN.
- **Committed in:** `7cd895d` (Task 2).

---

**Total deviations:** 1 auto-fixed (1 blocking).
**Impact on plan:** Necessary signature-cascade fix required to compile. No scope creep — the plan's `files_modified` list omitted the Commands but the wiring was unavoidable. Documented here for traceability.

## Issues Encountered
None — Path A made the 8-screen sweep entirely a VM/Command exercise, not a XAML one.

## User Setup Required
None — manual Revit visual verification deferred to Plan 03-09 per CONTEXT.md.

## Next Phase Readiness
- Wave 5 complete. All 14 screens that participate in REQ-01 now expose `RowItems` rendered through `ElementGridControl`.
- Plan 03-09 (Wave 6: phase-end manual Revit verification) can run on a stable build.

## Self-Check: PASSED

- `src/ViewModels/Components/SelectionViewModel.cs`: FOUND
- `src/Views/Components/SelectionControl.xaml`: FOUND
- All 8 modified VMs: FOUND
- All 5 modified Views: FOUND
- All 5 modified Commands: FOUND
- Commit `1b6e1e7`: FOUND
- Commit `7cd895d`: FOUND
- Build: clean (0/0)
- Tests: 100/100 GREEN

---
*Phase: 03-grid-collection-fixes*
*Completed: 2026-05-09*
