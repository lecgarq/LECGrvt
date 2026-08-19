# Requirements — Batch Rename UX

**Goal:** Make Batch Rename usable on a real-sized model — selection and counts that obey the active filter, filtering that reaches every column, a preview that stays responsive and never silently discards the user's checkboxes, and a layout that collapses to give the grid room.

**Opened:** 2026-08-18 · Source: user request, grounded in a read of `src/Views/SearchReplaceView.xaml` and `src/ViewModels/SearchReplaceViewModel.cs`

The rename **engine** is out of scope and stays as-is. `BatchRenameExecutionService` (1019 lines) already handles cross-batch collision detection, per-parameter `SubTransaction` rollback, formula rewriting and dimension-label reassignment. Every requirement below is in the interaction layer.

## In scope

### Selection

- R1: Select All / Select None act on the rows currently visible in the grid (the filtered `PreviewView`), never on rows the active filter has hidden. Today both iterate `PreviewItems` — the unfiltered collection (`src/ViewModels/SearchReplaceViewModel.cs:178-179`).
- R2: Select All never checks a row whose `IsRenameable` is `false`. The grid already disables those checkboxes; the command currently ticks them anyway.
- R3: An Invert Selection action exists and inverts only the visible, renameable rows.
- R4: The user can check or uncheck a contiguous range of rows in one gesture (shift-click or drag down the Sel column) rather than one click per row.
- R5: Manual check state survives a preview refresh. Changing a rule or a filter re-runs `ProcessPreview`, which today does `PreviewItems.Clear()` then re-adds (`:250-252`) — every manual deselection is lost. Rows that still exist after a refresh keep their `IsChecked`, keyed by element id plus the `Type` discriminator.

### Counts and feedback

- R6: The preview header and the status bar each report the **visible** row count and the **checked** row count, and both track the active filter. Today both bind `PreviewItems.Count` (`SearchReplaceView.xaml:404,470`) while the grid shows filtered rows — the number is wrong the moment a filter is applied.
- R7: An invalid regular expression in the Replace rule surfaces as a field-level validation message naming the pattern error. Today it is swallowed into the generic `"Error loading preview: {ex.Message}"` catch-all (`SearchReplaceViewModel.cs:258`).

### Filtering

- R8: Every grid column (Type, Category, Original, New, Status) exposes a per-column filter that routes through the existing `SetColumnFilter` and AND-combines with the others. `SetColumnFilter` is fully implemented and **no XAML calls it** (`SearchReplaceViewModel.cs:143`) — the feature is plumbed and unreachable.
- R9: The Category filter accepts more than one category at once and shows the row count for each.
- R10: The Category filter supports typeahead — a category is reachable by typing, without scrolling the list.
- R11: Clearing every active filter is one action, and the UI indicates when any filter is active.

### Responsiveness

- R12: Clicking a scope pill never blocks the UI thread. `RefreshScope()` calls `CollectBaseElements` synchronously on the dispatcher today (`SearchReplaceViewModel.cs:208`); it runs off-thread with a visible busy state.
- R13: A preview refresh replaces the grid contents without raising one collection-changed notification per row.
- R14: On a 5,000-row preview, a keystroke in the name filter produces an updated grid within 500 ms of the last keystroke, and no single UI-thread block exceeds 100 ms. The measured numbers are recorded, before and after.

### Layout

- R15: Scope, Filters and Operations are each collapsible, and their collapsed state persists for the session.
- R16: With all three sections collapsed, the preview grid occupies at least 70% of the window height. Today those sections are fixed-height and take roughly 60% of a 600 px window.
- R17: The grid can group rows by Category into expandable/collapsible groups, each with a row count and a group-level check toggle.
- R18: The dead "Extension (Coming Soon)" tile is removed (`SearchReplaceView.xaml:380-388`).
- R19: The scope control renders as what it is. Nine `CheckBox` pills currently behave as a radio group — `SetExclusiveScope` clears all nine on every click (`SearchReplaceViewModel.cs:198-207`) — so the control promises multi-select and does not deliver it.

### Validation

- R20: Every phase's UI changes are confirmed in live Revit — the dialog renders, bindings resolve, the changed interaction behaves — and the run is recorded in `docs/ai/revit-smoke-test.md`. Validation is per phase, not deferred to the end.

## Out of scope

- **Multi-scope renaming** (Types *and* Views in one pass). Decided 2026-08-18: R19 makes the control honest about being single-select instead. Real multi-scope would reach into `CollectBaseElements`, the nine-bool `SearchCriteria`/`RenameRuleContext` shape, category-list merging and the per-type rename dispatch — a milestone of its own.
- Any change to `BatchRenameExecutionService` rename semantics, collision detection, `SubTransaction` handling, formula rewriting or dimension-label reassignment.
- New rename rules. The Extension placeholder is deleted (R18), not implemented.
- Restyling `LecgTheme.xaml` or `LecgDataGrid` for other commands. Changes stay inside the Batch Rename view unless a shared control genuinely blocks a requirement — and then it is a plan deviation, stated.
- Profiling or optimising the rename execution itself. R14 measures the **preview** path only.
