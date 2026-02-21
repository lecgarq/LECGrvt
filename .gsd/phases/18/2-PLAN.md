---
phase: 18
plan: 2
wave: 2
---

# Plan 18.2: UI & Multi-Selection

## Objective
Update the Convert Family UI to support multiple selection and the new "Replace In-Place" mode.

## Context
- src/ViewModels/ConvertFamilyViewModel.cs
- src/Views/ConvertFamilyView.xaml
- src/Commands/ConvertFamilyCommand.cs

## Tasks

<task type="auto">
  <name>Update ConvertFamilyViewModel</name>
  <files>
    <file>src/ViewModels/ConvertFamilyViewModel.cs</file>
  </files>
  <action>
    1. Replace `SelectedRef` with `ObservableCollection<Reference> SelectedRefs`.
    2. Add `bool ReplaceInPlace` property.
    3. Update `SetSelection` to accept `IList<Reference>`.
    4. Update `CanRun` to depend on `SelectedRefs.Count > 0`.
    5. Update `ExecuteRun` to properly coordinate with the new batch service method.
  </action>
  <verify>Check that ViewModel has the new properties and respects CanRun logic.</verify>
  <done>ViewModel supports multi-selection and Replace mode toggle.</done>
</task>

<task type="auto">
  <name>Enhance ConvertFamilyView UI</name>
  <files>
    <file>src/Views/ConvertFamilyView.xaml</file>
    <file>src/Views/ConvertFamilyView.xaml.cs</file>
  </files>
  <action>
    1. Update `SelectionControl` or add a ListBox to show selected families.
    2. Add a `CheckBox` for "Replace Existing Instances In-Place".
    3. Add a Toggle/ComboBox for "Operation Mode".
    4. Ensure the design follows the "Liquid Glass" premium system.
    5. Update code-behind to handle `PickObjects` for multi-selection.
  </action>
  <verify>Visual verification (screenshot) and build check.</verify>
  <done>UI reflects the new batch and replace capabilities.</done>
</task>

<task type="auto">
  <name>Refactor ConvertFamilyCommand</name>
  <files>
    <file>src/Commands/ConvertFamilyCommand.cs</file>
  </files>
  <action>
    1. Update `Execute` logic to handle multi-selection via `Selection.PickObjects`.
    2. Pass the collection of references to the ViewModel.
    3. Call the batch service method upon confirmation.
  </action>
  <verify>Build check and functional verification in Revit (if possible).</verify>
  <done>Command correctly initiates multi-selection and batch conversion.</done>
</task>

## Success Criteria
- [ ] User can select multiple family instances.
- [ ] User can toggle "Replace In-Place" mode.
- [ ] Tool processes all selected instances and logs results.
