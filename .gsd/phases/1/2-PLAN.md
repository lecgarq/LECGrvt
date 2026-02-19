---
phase: 1
plan: 2
wave: 2
---

# Plan 1.2: ViewModel and View Expansion

## Objective

Update the Search & Replace user interface to allow users to select Materials, Object Styles, Line Styles, and Fill Patterns for renaming.

## Context

- .gsd/SPEC.md
- src/ViewModels/SearchReplaceViewModel.cs
- src/Views/SearchReplaceView.xaml

## Tasks

<task type="auto">
  <name>Update SearchReplaceViewModel</name>
  <files>
    <file>src/ViewModels/SearchReplaceViewModel.cs</file>
  </files>
  <action>
    - Add [ObservableProperty] bool properties for ScopeMaterialName, ScopeObjectStyleName, ScopeLineStyleName, and ScopeFillPatternName.
    - Update RefreshScope() to pass these new values to the service.
    - Add partial OnScope*Changed methods to trigger RefreshScope() when checkboxes are toggled.
  </action>
  <verify>Check that RefreshScope now passes 8 boolean scope flags instead of 4.</verify>
  <done>ViewModel handles the new scope flags and refreshes correctly.</done>
</task>

<task type="auto">
  <name>Update SearchReplaceView</name>
  <files>
    <file>src/Views/SearchReplaceView.xaml</file>
  </files>
  <action>
    - Locate the "Scope" section in the XAML (usually a WrapPanel or StackPanel with CheckBoxes).
    - Add 4 new CheckBoxes for Materials, Object Styles, Line Styles, and Fill Patterns.
    - Bind their IsChecked property to the new ViewModel properties.
    - Ensure the layout remains clean and follows the "Apple feel/minimalistic" style mentioned in conversation logs.
  </action>
  <verify>Visual check of the XAML structure for new bindings.</verify>
  <done>UI displays the new scope options and they are bound to the VM.</done>
</task>

## Success Criteria

- [ ] UI contains 8 scope options (Types, Families, Views, Sheets, Materials, Object Styles, Line Styles, Fill Patterns).
- [ ] Toggling a new checkbox triggers a data refresh in the preview grid.
- [ ] UI remains aesthetically consistent with the existing toolkit.
