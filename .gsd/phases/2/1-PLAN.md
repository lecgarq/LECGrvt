---
phase: 2
plan: 1
wave: 1
---

# Plan 2.1: Audit & Settings Extension

## Objective
Update the UI and settings model to include the "Force Normal Map" option and implement the material compliance audit logic (skip logic).

## Context
- .gsd/SPEC.md
- src/Models/RenderAppearanceSettings.cs
- src/ViewModels/RenderAppearanceViewModel.cs
- src/Views/RenderAppearanceView.xaml
- src/Services/RenderMaterialSyncCheckService.cs

## Tasks

<task type="auto">
  <name>Extend Settings & UI for Normal Map</name>
  <files>
    - src/Models/RenderAppearanceSettings.cs
    - src/ViewModels/RenderAppearanceViewModel.cs
    - src/Views/RenderAppearanceView.xaml
  </files>
  <action>
    1. Update `RenderAppearanceSettings.cs` to include `ForceNormalMap` (bool).
    2. Update `RenderAppearanceViewModel.cs`:
       - Add `ForceNormalMap` property (ObservableProperty).
       - Ensure `ToSettings()` includes the new property and correctly mapped UVScale.
    3. Update `RenderAppearanceView.xaml`:
       - Add a CheckBox for "Force Normal Map property in Bump slots".
       - Ensure UI alignment with the existing UV Scale dropdown.
  </action>
  <verify>dotnet build LECG.sln</verify>
  <done>UI reflects the new checkbox and the project builds.</done>
</task>

<task type="auto">
  <name>Implement 4-Point Compliance Audit</name>
  <files>
    - src/Services/RenderMaterialSyncCheckService.cs
  </files>
  <action>
    Update `IsMaterialSynced` to check the 4-point standard:
    1. 'Use Render Appearance' is enabled (`mat.UseRenderAppearance`).
    2. Shading color matches render assets (already partially there).
    3. Surface Foreground/Background patterns are Solid Fill.
    4. Cut Foreground/Background patterns are Solid Fill.
    Return `true` only if all conditions are met (meaning it can be skipped).
  </action>
  <verify>
    # Check logic matches SPEC goals
    cat src/Services/RenderMaterialSyncCheckService.cs
  </verify>
  <done>Auditing logic correctly identifies non-compliant materials.</done>
</task>

## Success Criteria
- [ ] UI provides "Force Normal Map" option.
- [ ] Settings model supports the new flag.
- [ ] `IsMaterialSynced` implementation covers all 4 visual standard points.
