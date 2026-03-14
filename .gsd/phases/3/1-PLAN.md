---
phase: 3
plan: 1
wave: 1
---

# Plan 3.1: LecgTreeView and Global UI Re-integration

## Objective
Finalize the `LecgUI` library with a custom `TreeView` and begin the global rollout of the new UI standards across the application's windows. This includes implementing batch expansion/collapse for TreeViews and replacing legacy styles.

## Context
- .gsd/SPEC.md
- .gsd/ROADMAP.md
- .gsd/TODO.md
- src/Resources/Themes/LecgTheme.xaml

## Tasks

### Wave 1: LecgTreeView Foundation
<task type="auto">
  <name>Implement LecgTreeView Custom Control</name>
  <files>src/Controls/LecgTreeView.cs</files>
  <action>
    - Create `LecgTreeView.cs` inheriting from `System.Windows.Controls.TreeView`.
    - Implement `ExpandAll()` and `CollapseAll()` logic.
    - Use reflection or a standard interface (e.g., `IExpandable`) to handle hierarchical data expansion without blocking the UI.
  </action>
  <verify>dotnet build LECG.csproj</verify>
  <done>LecgTreeView supports batch expansion and collapse logic.</done>
</task>

<task type="auto">
  <name>Define LecgTreeView Style</name>
  <files>src/Resources/Themes/LecgTheme.xaml</files>
  <action>
    - Define a global style for `TreeView` and `TreeViewItem` in `LecgTheme.xaml`.
    - Apply the earth-tone palette (`LecgSurfaceBackground`, `LecgAccent`).
    - Standardize the "Expander" (arrow) icon and hover states for items.
    - Ensure clean indentations and minimalist look.
  </action>
  <verify>Get-Content "src/Resources/Themes/LecgTheme.xaml" | Select-String "TargetType=\"{x:Type TreeView}\""</verify>
  <done>TreeView aesthetics are aligned with the LECG brand identity.</done>
</task>

### Wave 2: Global Rollout (Phase A)
<task type="auto">
  <name>Audit and Patch Primary Views</name>
  <files>src/Views/HomeView.xaml, src/Views/SearchReplaceView.xaml</files>
  <action>
    - Identify hardcoded colors or legacy styles (e.g., `ModernTextBoxStyle`).
    - Replace them with the new global resources (e.g., `DynamicResource LecgBaseBackground`).
    - Swap native `DataGrid` and `TreeView` usage for `LecgDataGrid` and `LecgTreeView`.
  </action>
  <verify>grep -r "ModernTextBoxStyle" src/Views/ | Measure-Object</verify>
  <done>Key entry-point views are fully converted to the new UI standards.</done>
</task>

## Success Criteria
- [ ] `LecgTreeView` exists and supports `ExpandAll/CollapseAll`.
- [ ] Primary views (Home, Batch Rename) no longer reference legacy styles.
- [ ] No regression in Revit UI performance or threading behavior.
