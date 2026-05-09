---
phase: 03-grid-collection-fixes
plan: 02
subsystem: ui-grids
tags: [mvvm, viewmodel, datagrid, foundation]
requires: ["03-00"]
provides:
  - "LECG.ViewModels.Components.ElementRowViewModel"
affects:
  - "src/ViewModels/Components/ElementRowViewModel.cs"
tech_stack:
  added: []
  patterns:
    - "CommunityToolkit.Mvvm source-generator [ObservableProperty]"
    - "ObservableObject inheritance for reflection-driven Select-All/None in LecgDataGrid"
key_files:
  created:
    - "src/ViewModels/Components/ElementRowViewModel.cs"
  modified: []
decisions:
  - "Only IsChecked is observable; identity/display fields are plain auto-properties (set once at row creation; WPF DataGrid binds via reflection regardless)"
  - "ReplaceItem retained as-is — Plan 03-04 owns the SearchReplacePreviewService migration to avoid breaking compilation between waves"
  - "Used [ObservableProperty] source-generator pattern (not manual SetProperty) to match the rest of the codebase"
metrics:
  duration: "~6min"
  tasks_completed: 1
  files_changed: 1
  completed_date: "2026-05-09"
requirements:
  - "REQ-01"
---

# Phase 03 Plan 02: ElementRowViewModel Summary

Shared per-row ViewModel for every grid in the plugin — single MVVM model carrying identity, display, status, and SearchReplace carry-over fields, replacing scattered `ReplaceItem` and `ObservableCollection<string>` row representations across 7+ ViewModels.

## Schema

Namespace: `LECG.ViewModels.Components`
Base: `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` (partial class for source-generator)

| Field          | Type     | Observable | Purpose                                                                  |
| -------------- | -------- | ---------- | ------------------------------------------------------------------------ |
| `IsChecked`    | `bool`   | yes        | Drives LecgDataGrid Select-All / Select-None via reflection (default `true`) |
| `Id`           | `long`   | no         | Element id                                                               |
| `Name`         | `string` | no         | Display name (non-blank invariant via `ElementLabelService`)             |
| `Category`     | `string` | no         | Revit category label                                                     |
| `Type`         | `string` | no         | Discriminator: `Type` \| `Family` \| `FamilyParameter` \| `View` \| `Sheet` \| ... |
| `Status`       | `string` | no         | Command-specific outcome / skip reason                                   |
| `Family`       | `string` | no         | Populated for `FamilyParameter` rows                                     |
| `OriginalValue`| `string` | no         | SearchReplace: original parameter value                                  |
| `NewValue`     | `string` | no         | SearchReplace: replacement value                                         |
| `ParamGroup`   | `string` | no         | SearchReplace: parameter group label                                     |
| `IsInstance`   | `bool`   | no         | SearchReplace: instance vs type parameter                                |
| `IsReadOnly`   | `bool`   | no         | SearchReplace: parameter read-only flag                                  |

## Tasks Completed

| Task | Name                          | Commit  | Files                                               |
| ---- | ----------------------------- | ------- | --------------------------------------------------- |
| 1    | Create ElementRowViewModel    | 682411a | `src/ViewModels/Components/ElementRowViewModel.cs`  |

## Verification

- `dotnet build LECG.sln --no-restore` produced fresh `LECG.dll` (no CS#### compile errors).
- Build copy step to `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\` failed with MSB3027/MSB3021 file-lock errors (Autodesk Revit 36872 was running and holding loaded assemblies). Per environment note this is a known environmental blocker, not a code defect. Compilation + source-generator pass succeeded.
- File `src/ViewModels/Components/ElementRowViewModel.cs` exists and namespace resolves to `LECG.ViewModels.Components.ElementRowViewModel`.

## Deviations from Plan

None — plan executed exactly as written.

## Downstream Impact

- **Plan 03-04** (SearchReplacePreviewService) — will migrate `List<ReplaceItem>` to `List<ElementRowViewModel>`.
- **Plan 03-05** (ElementGridControl.xaml) — will bind columns to `ElementRowViewModel` properties (`{Binding Category}`, `{Binding IsChecked}`, etc.).
- `ReplaceItem` deliberately untouched this plan to keep wave-1 compilation clean.

## Known Environmental Blockers

- File-lock warnings on copy-to-Revit-Addins step: Revit (PID 36872) is running and holding `LECG.dll`, `LECG.Core.dll`, `CommunityToolkit.Mvvm.dll`, etc. Resolves automatically once Revit is closed or after a clean restart.

## Self-Check: PASSED

- File `src/ViewModels/Components/ElementRowViewModel.cs` — FOUND
- Commit `682411a` — FOUND
