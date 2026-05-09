---
phase: 03-grid-collection-fixes
plan: 04
subsystem: ui
tags: [batch-rename, mvvm, search-replace, element-row-viewmodel, refactor]

requires:
  - phase: 03-grid-collection-fixes
    provides: ElementRowViewModel shared row model (Plan 03-02)
provides:
  - SearchReplacePreviewService.ProcessPreview returns List<ElementRowViewModel> with Category propagated
  - Batch Rename pipeline (preview + execution) consumes ElementRowViewModel end-to-end
  - Legacy ReplaceItem class deleted from codebase
affects:
  - 03-05 (ICollectionView sort/filter wiring on PreviewItems)
  - 03-06+ (any remaining grid migrations to ElementRowViewModel)

tech-stack:
  added: []
  patterns:
    - "Single shared row model (ElementRowViewModel) across Batch Rename pipeline"
    - "Pure pass-through of upstream Category invariant from collection layer through preview into VM"

key-files:
  created: []
  modified:
    - src/Services/Renaming/ISearchReplacePreviewService.cs
    - src/Services/Renaming/SearchReplacePreviewService.cs
    - src/Services/Renaming/ISearchReplaceService.cs
    - src/Services/Renaming/SearchReplaceService.cs
    - src/Services/Renaming/IBatchRenameExecutionService.cs
    - src/Services/Renaming/BatchRenameExecutionService.cs
    - src/ViewModels/SearchReplaceViewModel.cs
    - src/Views/SearchReplaceView.xaml
    - LECG.Tests/Services/SearchReplacePreviewServiceTests.cs

key-decisions:
  - "Atomic ReplaceItem -> ElementRowViewModel migration in a single plan to keep project compilable end-of-plan; transient broken state between Task 1 and Task 2 is acceptable per RESEARCH §Pitfall 2"
  - "BatchRenameExecutionService and ISearchReplaceService consumers updated in this plan (scope expansion required to delete ReplaceItem); plan Task 2 step 5 explicitly anticipated this"
  - "ProcessPreview is a pure pass-through for Category — no fallback computation in the service; fallback is owned by Plan 03-03 collection layer"

patterns-established:
  - "ElementRowViewModel is the canonical row type for any preview/grid surface in the plugin"
  - "Field rename at consumers: ElementId -> Id, ElementName -> Name (single naming convention)"

requirements-completed: [REQ-01]

duration: 18min
completed: 2026-05-09
---

# Phase 03 Plan 04: SearchReplacePreviewService -> ElementRowViewModel Migration Summary

**Atomic ReplaceItem -> ElementRowViewModel swap across the Batch Rename pipeline (preview, execution, view-model, XAML) with Category, ParamGroup, IsInstance, IsReadOnly propagation now intact end-to-end.**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-05-09T18:46:24Z
- **Completed:** 2026-05-09T19:04:48Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- `SearchReplacePreviewService.ProcessPreview` now returns `List<ElementRowViewModel>` with Category, ParamGroup, IsInstance, IsReadOnly fields propagated from `ElementData`.
- Legacy `ReplaceItem` class deleted; zero code references remain in `src/` or `LECG.Tests/` (only historical comments retain the name).
- 3-W0-03 RED test scaffolds (Plan 03-00) un-Skipped and now GREEN; the Wave-0 anchor test was deleted per its DisplayName marker.
- Build clean (0 warnings, 0 errors); full unit suite: 98 passed, 2 skipped (Plan 03-05 reserved).

## Task Commits

1. **Task 1: Migrate ProcessPreview return type and Category propagation** — `8b17823` (feat)
2. **Task 2: Delete ReplaceItem; migrate consumers + XAML bindings** — `3a29be6` (refactor)

## Files Created/Modified
- `src/Services/Renaming/ISearchReplacePreviewService.cs` — return type swap to `List<ElementRowViewModel>`.
- `src/Services/Renaming/SearchReplacePreviewService.cs` — constructs `ElementRowViewModel` rows propagating `Category`, `ParamGroup`, `IsInstance`, `IsReadOnly`, `Type`, `Id`, `OriginalValue`, `NewValue`.
- `src/Services/Renaming/ISearchReplaceService.cs` — facade interface migrated to `ElementRowViewModel`.
- `src/Services/Renaming/SearchReplaceService.cs` — proxy implementation migrated.
- `src/Services/Renaming/IBatchRenameExecutionService.cs` — consumer interface migrated.
- `src/Services/Renaming/BatchRenameExecutionService.cs` — `ReplaceItem` -> `ElementRowViewModel`; `item.ElementId` -> `item.Id`; `item.ElementName` -> `item.Name`.
- `src/ViewModels/SearchReplaceViewModel.cs` — `ReplaceItem` class deleted; `PreviewItems` is `ObservableCollection<ElementRowViewModel>`; added `using LECG.ViewModels.Components;`.
- `src/Views/SearchReplaceView.xaml` — `{Binding ElementId}` -> `{Binding Id}`, `SortMemberPath="ElementId"` -> `SortMemberPath="Id"`. (Other column bindings — `OriginalValue`, `NewValue`, `Type`, `IsChecked` — already match `ElementRowViewModel` field names; no rebind needed.)
- `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — anchor test deleted; 3 RED tests un-Skipped and rewritten to assert `ElementRowViewModel` shape (Category, ParamGroup, IsInstance, IsReadOnly, Id).

## ReplaceItem Reference Removal
- **Before:** 30 code references across 7 source files + 5 test references.
- **After:** 0 code references in `src/` or `LECG.Tests/`. Remaining mentions are non-code comments only:
  - `src/ViewModels/Components/ElementRowViewModel.cs:7` — historical XML doc note ("Replaces the legacy `ReplaceItem`").
  - `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs:5` — Wave-0 file-header comment (will be cleaned up in Plan 03-05 when sort/filter assertions are filled in).
  - `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs:5,7` — file-header context describing what Plan 03-04 migrated (intentional historical record).

## Consumer Services Updated (Plan Scope Expansion)
The plan's Task 2 step 5 explicitly anticipated this: deleting `ReplaceItem` required signature updates to:

- `IBatchRenameExecutionService` (both overloads)
- `BatchRenameExecutionService` (both overloads + private helpers `GroupCheckedFamilyParameterItems`, `RenameFamilyParameters`, `TryRenameFamilyParameter`)
- `ISearchReplaceService` (`ProcessPreview` + 2 `ExecuteBatchRename` overloads)
- `SearchReplaceService` (proxy methods)

All consumers swapped atomically in Task 2. Field-rename at call sites: `item.ElementId` -> `item.Id`, `item.ElementName` -> `item.Name`. No semantic changes to rename logic.

## Decisions Made
- **Atomic swap, not gradual:** Followed RESEARCH §Pitfall 2 — kept `ReplaceItem` and `ElementRowViewModel` from coexisting. Project is briefly broken between Task 1 and Task 2 commits; this is acceptable for a single-plan atomic refactor.
- **Pure pass-through for Category:** Did not add a fallback computation in `ProcessPreview`. The collection layer (Plan 03-03) owns the non-blank Category invariant; `ProcessPreview` propagates whatever it receives unchanged. Test 2 (`fallback_Category_when_ElementData_Category_is_blank`) was rewritten to assert pass-through behavior given a non-blank synthetic value (since 03-03 guarantees no blanks reach this layer).
- **No ICollectionView wiring yet:** Reserved for Plan 03-05 per plan's anti-pattern note.

## Deviations from Plan

None — plan executed as written. Task 2 step 5 explicitly listed `BatchRenameExecutionService` consumer migration; that work was anticipated and is part of the planned scope.

## Issues Encountered
- One transient git index lock during the Task 1 commit (`fatal: Unable to create '.git/index.lock'`). Resolved by removing the stale lock file; commit succeeded on retry. No code impact.
- LF/CRLF line-ending warnings on Task 2 commit (cosmetic; git auto-normalized on stage).

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- Plan 03-05 unblocked: `PreviewItems` is now `ObservableCollection<ElementRowViewModel>`, ready for `CollectionViewSource.GetDefaultView` sort/filter wiring (Category-ascending default + AND-combined per-column filters).
- 3-W0-04 RED scaffolds (`SearchReplaceViewModelTests`) remain Skip-gated for Plan 03-05 — anchor test still passes.
- Manual Revit verification of the Batch Rename window at phase-end session (REQ-01) — the XAML rebind and end-to-end Category flow are ready for visual smoke-test.

## Self-Check: PASSED

Verification of claims:
- `8b17823` exists in git log: FOUND
- `3a29be6` exists in git log: FOUND
- `src/Services/Renaming/SearchReplacePreviewService.cs` returns `List<ElementRowViewModel>`: VERIFIED via grep + build
- `ReplaceItem` removed from `src/` code: VERIFIED (only XML doc comment remains)
- `dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true`: 0 errors, 0 warnings
- `dotnet test`: 98 passed, 0 failed, 2 skipped (Plan 03-05)

---
*Phase: 03-grid-collection-fixes*
*Completed: 2026-05-09*
