# Roadmap — Batch Rename UX

Coverage: 20/20 requirements mapped.

Sequenced by dependency, then by risk. Phase 1 goes first because it rebuilds the preview pipeline every later phase binds against — if that shape is wrong, the rest of the plan is wrong, and it is cheaper to learn that now.

Live-Revit validation (R20) is folded into every phase rather than stacked into a final one. That is the direct lesson of `warnings-review`, which closed with its whole runtime gate unexecuted because it lived in a phase of its own.

## Phase 1: Preview pipeline — stops losing state, stops blocking
**Goal:** Changing a rule or a scope no longer discards the user's checkboxes and no longer freezes the window. The visible behaviour change: deselect three rows, tweak the Replace text, and those three rows are still deselected.
**Covers:** R5, R12, R13, R14, R20
**Depends on:** none
**Done when:** `CollectBaseElements` runs off the UI thread behind a busy state; a preview refresh preserves `IsChecked` for surviving rows keyed by `Id` + `Type`; the refresh no longer raises a notification per row; the 5,000-row keystroke measurement is recorded before and after (< 500 ms to updated grid, no UI block > 100 ms); unit tests cover the preservation keying, including the case where a row disappears and returns; and the behaviour is confirmed in live Revit.

## Phase 2: Selection that obeys the filter
**Goal:** Filter to twelve rows, hit Select All, and twelve rows are selected — not four thousand.
**Covers:** R1, R2, R3, R4, R6, R20
**Depends on:** Phase 1
**Done when:** Select All / Select None / Invert all operate on `PreviewView`; no command ever checks an `IsRenameable == false` row; a contiguous range can be toggled in one gesture; the header and status bar each show visible-count and checked-count and both follow the filter; the selection helper is unit-tested against a filtered view; and the behaviour is confirmed in live Revit.

## Phase 3: Filtering that reaches every column
**Goal:** Every column filters, the Category dropdown handles multiple categories with counts and typeahead, and a bad regex says so instead of failing as a generic preview error.
**Covers:** R7, R8, R9, R10, R11, R20
**Depends on:** Phase 1 (shares `PreviewView`); Phase 2 (selection must already respect the filtered view, or per-column filters make R1 worse)
**Done when:** each of Type / Category / Original / New / Status filters through `SetColumnFilter` and the predicates AND-combine; the Category filter is multi-select with per-category counts and typeahead; one action clears all filters and an indicator shows when any is active; an invalid regex produces a field-level message naming the pattern error; filter-combination logic is unit-tested; and the behaviour is confirmed in live Revit.

## Phase 4: Layout — collapse, group, compact
**Goal:** The grid gets the window. Scope, Filters and Operations fold away; rows group under collapsible Category headers; the dead tile and the fake multi-select are gone.
**Covers:** R15, R16, R17, R18, R19, R20
**Depends on:** Phase 2 and Phase 3 — group-level check toggles need the selection semantics settled, and grouping interacts with the filter predicates
**Done when:** the three sections collapse and remember their state for the session; the grid is ≥ 70% of window height with all three collapsed; rows group by Category with a per-group count and a working group check toggle; the Extension tile is deleted; the scope control renders as single-select; and the whole dialog is confirmed in live Revit including the section-collapse and group-expand interactions.

## Notes for planning

- `SetColumnFilter` and the `ICollectionView` plumbing already exist (`SearchReplaceViewModel.cs:118-170`) — Phase 3 is mostly wiring XAML to code that is already written and tested-by-absence. Check before building anything new.
- `LecgDataGrid` already enables row and column virtualization with recycling (`src/Controls/LecgDataGrid.cs:23-26`). R13/R14 are about the *rebuild*, not about scrolling.
- Five tests in `BaseElementCollectionServiceTests` and `SearchReplaceServiceTests` skip outside Revit. They are in this milestone's blast radius — check what they would have covered before trusting a green suite here.
- Any view edit must keep the `LecgTheme.xaml` merge inside `<base:LecgWindow.Resources>` (`SearchReplaceView.xaml:15-22`). A declared `Resources` block replaces the constructor's dictionary; drop the merge and every `StaticResource` fails at runtime, invisibly to the compiler and the test suite.
