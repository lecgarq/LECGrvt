---
phase: 1
plan: 2
wave: 2
---

# Plan 1.2: Base Architecture for LecgDataGrid

## Objective
Establish the primary, custom generic interactive list system (`LecgDataGrid`) extending the standard WPF `DataGrid`. This component encapsulates professional interactions like Sort/Filter/Batch selections explicitly avoiding duplicated logic in feature ViewModels.

## Context
- .gsd/SPEC.md
- .gsd/DECISIONS.md
- src/Resources/Themes/LecgTheme.xaml

## Tasks

<task type="auto">
  <name>Create DataGrid UI Extension Class</name>
  <files>src/Controls/LecgDataGrid.cs</files>
  <action>
    - Extend `System.Windows.Controls.DataGrid`.
    - Override the basic visual properties (Background, BorderBrush) dynamically linking to `LecgColors.xaml` components via `SetResourceReference`.
    - Apply the anti-aliased visual quality standard explicitly within the control implementation (`RenderOptions.SetBitmapScalingMode` / `TextOptions.SetTextFormattingMode`).
    - Expose custom `SelectionMode="Extended"` strictly, avoiding breaking default Shift/Ctrl selection behaviors native to Windows.
  </action>
  <verify>Get-Content "src/Controls/LecgDataGrid.cs" | Select-String "public class LecgDataGrid"</verify>
  <done>Base control class created ready for event extensions.</done>
</task>

<task type="auto">
  <name>Implement Check/Expand Control Logic Hooks</name>
  <files>src/Controls/LecgDataGrid.cs</files>
  <action>
    - Inject routing mechanisms inside `LecgDataGrid` explicitly listening for the batch operations requested (Check All / Uncheck All / Expand All / Collapse All).
    - Since items may implement `ICheckable` or `IExpandable`, establish generic properties (like `CheckPropertyName`) allowing the grid to iterate items dynamically without forcing strong coupling.
    - Write the internal `CheckAll()` and `UncheckAll()` methods utilizing `Fast Reflection` or compiled expression trees to quickly modify properties down the `ItemsSource` without breaking UI Virtualization.
  </action>
  <verify>Select-String -Path src/Controls/LecgDataGrid.cs -Pattern "CheckAll"</verify>
  <done>The visual control inherently understands how to manipulate mass object boolean flags without the ViewModel needing distinct loop commands.</done>
</task>

## Success Criteria
- [ ] `LecgDataGrid` inherits from `DataGrid`.
- [ ] Batch boolean switching functions execute cleanly over a bounded generic property string.
