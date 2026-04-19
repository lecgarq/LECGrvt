# Architecture Decisions Log

Format:
| Date | Decision | Context | Status |
|------|----------|---------|--------|
| {Date} | {Decision} | {Why we chose this} | Expected / Validated / Deprecated |

## Phase 1 Decisions

**Date:** 2026-03-14

### Scope
- Generic standard WPF UI controls (Buttons, TextBoxes, DataGrids, TreeViews).
- Specifically for Row/Column lists (DataGrids/TreeViews): Must implement column header clicking to sort.
- Specifically for lists: Must implement filtering actions by column.
- Specifically for row selection: Must implement Shift-click for interval multiple selection and Ctrl-click for discrete multiple selection.
- List controls must include global actions: Check All, Uncheck All, Expand All, Collapse All.

### Approach
- Chose: Centralized Custom UI Control extensions (e.g. extending `DataGrid` or `TreeView` to build `LecgDataGrid` and `LecgTreeView`) encapsulating the sorting, filtering, selection, and global action logic, so it doesn't leak into individual ViewModels.
- Reason: The goal is standardizing UI/UX. Writing these events individually across 36 views goes against the deduplication and professionalism goals.

### Constraints
- Must remain performant with large datasets in Revit (e.g. 5000+ views/sheets).
- Ensure `VirtualizingStackPanel` is utilized in all major lists.
- Avoid blocking the main UI thread during heavy filtering operations.

## Phase 2 Decisions

**Date:** 2026-03-14

### Scope
- Decouple `SearchReplacePreviewService` from `SearchReplaceViewModel`.
- Implement asynchronous, debounced filtering for real-time responsiveness.
- Standardize the "Filtering Logic" to be independent of the Revit API (utilizing cached `ElementData`).

### Approach
- **Chose:** Reactive Background Filtering with DTO criteria.
- **Rationale:** Prevents UI lag in large models while allowing the "blazing fast" identity for LECG. Decoupling ensures testability without WPF dependencies in the Services layer.

### Constraints
- All filtering must happen on background threads (`Task.Run`).
- Must handle rapid typing via `CancellationToken` cancellation of previous search tasks.
- Must not touch `Autodesk.Revit.DB` objects during the background task.
