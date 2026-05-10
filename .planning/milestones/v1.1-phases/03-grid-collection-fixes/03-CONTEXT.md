# Phase 3: Grid & Collection Fixes — Context

**Gathered:** 2026-05-09
**Status:** Ready for planning
**Source:** /gsd:discuss-phase 3 — interactive session with user

<domain>
## Phase Boundary

Fix blank Name/Category labels and inconsistent collection logic so every element the user expects to act on appears in the relevant UI with valid, readable Name and Category labels. Closes REQ-01 from v1.1 milestone audit (2026-05-09).

Scope expanded by user decision: this phase touches **every preview/selection grid in the plugin**, not only Batch Rename. Where today screens render `'ID 1234 | Floors | layers...'` text-summary lines, those become real interactive grids. The ground-truth fix is a shared row model + grid control that all command screens consume.

Anchor screens (must be in phase):
- Batch Rename (`SearchReplaceView` + `SearchReplaceViewModel`) — primary target of REQ-01
- Compact Styles preview grids
- All command screens currently using text-summary lines: ConvertFloorToToposolid, ConvertToposolidToFloor, DivideToposolid, FixPoints, SimplifyPoints, SplitBoundaries, etc.
- All Selection-component–backed screens (AlignEdges, AlignElements, AssignMaterial, CategoryChanger, ChangeLevel, ConvertCad, OffsetElevations, ResetSlabs, etc.) — `None — include all in phase`.

</domain>

<decisions>
## Implementation Decisions

### Grid Row Model & Control (shared, reusable)
- **Build a shared `ElementRowViewModel` and `ElementGridControl`** used by every screen with a per-element list. Avoids 6+ ad-hoc DataGrid implementations.
- Row carries at minimum: `Id` (long), `Name` (string), `Category` (string), `Type` (string discriminator), `Status` (string — for command-specific outcome/skip reasons), `IsChecked` (bool).
- Existing `ReplaceItem` (Batch Rename) extended (or replaced) to align with this model — `Category` column added to ReplaceItem.
- Where a screen needs extra columns (e.g., Layers count in DivideToposolid), the shared control supports additional column slots.

### Category Column in Batch Rename Grid
- **Add Category column.**
- Position: between `Type` and `OriginalValue` — order is `☑ | Type | Category | Original | New`.
- Default width: **auto-fit content, resizable** (matches WPF DataGrid familiarity).
- **Sortable** with standard header sort indicator (▲/▼).
- **Per-column filter** (funnel icon in header).
- Default sort on grid load: **by Category, ascending**.
- Per-column filter and the existing top-of-grid `FilterCategory` dropdown both apply, **AND-combined**.

### Family Parameter Category Label (Claude's discretion + edge-case rules)
- The `Type` column keeps the single label `FamilyParameter` (do not split into `FamilyParameter (Instance)` / `FamilyParameter (Type)`); the existing `IsInstance` filter remains the way users distinguish.
- Edge case: when a Family Parameter row has no resolvable Revit category for its parent family, **fallback to the family name** in the Category cell (do not hide the row, do not show `<Uncategorized>`). No-blanks invariant is preserved.
- **Filter UX for Family Parameter scope: cascading filters.** Outer dropdown = Revit category; inner dropdown = family name within that category. Two clicks, max precision.

### Migrated Text-Summary Screens
- All screens currently rendering `'ID … | Cat … | …'` strings switch to **interactive grids** with per-row **checkbox** + **sort** (same UX as the Batch Rename grid). Allows the user to deselect rows before commit.
- Status/skip-reason text (where today’s summaries embed it) becomes a `Status` column.

### Selection-Backed Screens (Align*, AssignMaterial, etc.)
- Included in phase. No screens excluded.
- Each `SelectionViewModel`-backed screen exposes an `ObservableCollection<ElementRowViewModel>` and renders `ElementGridControl`. Empty state stays as the "No X selected" message via existing SelectionViewModel hooks.

### No-Blanks Invariant (REQ-01 core)
- **Every row that reaches a grid must have a non-empty Name AND non-empty Category.** Both fields are guaranteed by the collection layer; UI does not guard.
- For Types whose `el.Category == null` today (silently skipped at `BaseElementCollectionService.cs:21`): collection now keeps them, with Category resolved via fallback chain (e.g., `BuiltInCategory` lookup, then element-class name as last resort). Researcher to enumerate the exact fallback chain.
- For elements whose `el.Name` is empty/whitespace: collection produces a stable display name (e.g., `<{Type} {Id}>`). No row shows blank Name. Researcher to validate which Revit element kinds actually produce empty names in localized installs.

### Logging
- All silent-skip paths (today’s `if (el.Category == null) continue;` and similar) replaced with **LogView entries** when an element is included with a fallback label. No new log surfaces — keep `LogView` only (carried forward from Phase 02.5).

### Locale Safety (carried forward from Phase 02.5)
- Any Revit API access used to derive Category labels must be locale-safe. Prefer `BuiltInCategory` IDs and `LabelUtils` over English category-name string comparisons.

### Claude's Discretion
- **Family Parameter Category cell default value** when a Revit category exists: choose between the Revit category name (e.g., "Doors"), the family name, or a combined `"Doors / Door-Single-Flush"` form. User trusted Claude here. Recommendation to validate during planning: **show the Revit category** (consistency with Type rows) and surface the family name in a dedicated `Family` sub-column or tooltip — but planner should reconsider if this conflicts with the cascading-filter UX.
- Sort/filter UI implementation details on the shared `ElementGridControl` (header chrome, filter popup behavior beyond "funnel icon + popup with checkbox list").
- How the shared control composes with existing `LecgDataGrid` styling (inherit, restyle, or replace).
- Migration ordering across the 6+ screens (one plan per screen group, or one big sweep). Planner to break into waves.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/Controls/LecgDataGrid.cs` — existing base control to extend or wrap into `ElementGridControl`.
- `src/Resources/Controls.xaml` + `LecgTheme.xaml` — established WPF styling. New grid must inherit visual conventions.
- `src/ViewModels/Components/SelectionViewModel.cs` — provides `ElementName` and "X selected" summary. Surface where the new `ObservableCollection<ElementRowViewModel>` plugs in.
- `src/Services/Renaming/BaseElementCollectionService.cs` — the canonical collector for Batch Rename. The `if (el.Category == null) continue;` at line 21 is the load-bearing silent-skip to fix.
- `src/Services/Renaming/SearchReplacePreviewService.cs:110-118` — the only place `ReplaceItem` is constructed; must add `Category = el.Category`.
- `src/Services/Renaming/ElementData` — already carries `Category` end-to-end; just not propagated to the row model or XAML yet.

### Established Patterns
- CommunityToolkit MVVM `ObservableObject` / `ObservableProperty` (already used by `ReplaceItem`, `SearchReplaceViewModel`).
- `RelayCommand` for grid actions (SelectAll/SelectNone exists in SearchReplaceViewModel — reuse pattern).
- Locale-safe lookups: `BuiltInParameter.TEXT_ALIGNMENT` + `LabelUtils.GetLabelForGroup` already used in Phase 02.5 for similar concerns.
- LogView-only logging surface (Phase 02.5 decision).

### Integration Points
- Each command’s VM (`*ViewModel.cs`) is where `ObservableCollection<ElementRowViewModel>` replaces today’s `SelectedElementSummaries` `ObservableCollection<string>` lists.
- Each command’s View (`*View.xaml`) replaces the `ItemsControl`/`ListBox` over text strings with `ElementGridControl`.
- `BaseElementCollectionService` and any per-command collection method (e.g., `DivideToposolidViewModel` building summary strings inline at lines 81-82) become callers of a normalization helper that guarantees the no-blanks invariant.

### Creative Options
- A small `ElementLabel` helper service could centralize the "give me a non-blank Name and Category for any Element" logic, used by all collectors and migration paths. Researcher should evaluate this vs inlining the fallback per-collector.

</code_context>

<specifics>
## Specific Ideas

- "Type 12345"-style fallback for blank names (mentioned in question framing, not strictly mandated — Claude's discretion on exact format).
- Cascading category filters under Family Parameter scope: `Revit Category → Family Name`.
- Default grid sort: Category ascending; sort indicator visible in header.

</specifics>

<deferred>
## Deferred Ideas

- None at this time — user opted to include all candidate screens in this phase.
- Future possibility (NOT this phase): persist user's per-grid sort/filter state across sessions.

</deferred>

---

*Phase: 03-grid-collection-fixes*
*Context gathered: 2026-05-09 via /gsd:discuss-phase*
