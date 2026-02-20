---
phase: 7
plan: 1
wave: 1
---

# Plan 7.1: UI/UX Improvements & Multi-Choice Filters

## Objective
Enhance the user experience by enabling multi-selection of element categories, fixing filtering update issues, adding "Select All/None" functionality, and introducing property-based parameter filtering.

## Context
- The user reported inability to select multiple categories (e.g., Types AND Families).
- Filter updates do not refresh the list correctly or allow interaction.
- Lack of "Check All / Uncheck All" buttons.
- Need for advanced parameter filtering (by Group, Type/Instance, Formula presence).

## Tasks

<task type="auto">
  <name>Enable Multi-Selection Scope</name>
  <files>src/ViewModels/SearchReplaceViewModel.cs</files>
  <action>
    Modify `SearchReplaceViewModel` to allow multiple booleans (`ScopeTypeName`, `ScopeFamilyName`, etc.) to be true simultaneously without disabling others.
    Ensure `RefreshScope()` aggregates results from all selected scopes instead of mutually exclusive logic if present.
    (Note: The current `CollectBaseElements` implementation already uses separate boolean flags, so this might be a binding or UI trigger issue ensuring `RefreshScope` is called correctly on each change and appends data.)
  </action>
  <verify>Code inspection confirming independent boolean properties trigger refresh.</verify>
  <done>Multiple scope checkboxes can be checked, and the DataGrid shows elements from all selected categories.</done>
</task>

<task type="auto">
  <name>Add Select All / Select None</name>
  <files>src/ViewModels/SearchReplaceViewModel.cs, src/Views/SearchReplaceView.xaml</files>
  <action>
    Add `RelayCommand`s for `SelectAll` and `SelectNone`.
    Add buttons to the UI (likely above the DataGrid) binding to these commands.
    Ensure these commands affect only the currently filtered/visible items in `PreviewItems`.
  </action>
  <verify>Buttons appear in UI and toggle `IsChecked` on items.</verify>
  <done>User can bulk select/deselect items.</done>
</task>

<task type="auto">
  <name>Advanced Parameter Property Filtering</name>
  <files>src/Services/BaseElementCollectionService.cs, src/Services/SearchReplacePreviewService.cs, src/Services/SearchReplaceService.cs, src/ViewModels/SearchReplaceViewModel.cs, src/Views/SearchReplaceView.xaml</files>
  <action>
    1.  Update `ElementData` to include extra properties: `ParamGroup`, `IsInstance`, `HasFormula` (populated in `BaseElementCollectionService`).
    2.  Add UI controls (likely an Expander or specific comboboxes) in `SearchReplaceView.xaml` to filter by these new properties.
    3.  Update `SearchReplacePreviewService.ProcessPreview` to filter candidates based on these new criteria.
  </action>
  <verify>Code review of filtering logic.</verify>
  <done>User can filter Family Parameters by their specific properties (e.g., "Show only Instance parameters").</done>
</task>

## Success Criteria
- [ ] Users can check "Types" AND "Families" and see both in the list.
- [ ] "Select All" and "Select None" buttons function correctly on the visible list.
- [ ] Changing filters updates the list in real-time and allows interaction.
- [ ] Family Parameters can be filtered by:
    - Parameter Group
    - Instance vs Type
    - ReadOnly / Formula driven status
