---
phase: 1
plan: 1
wave: 1
---

# Plan 1.1: UI Foundation & Wiring

## Objective
Implement the UI controls for UV Scale selection and update the ViewModel/Command to pass these settings to the Service layer.

## Context
- .gsd/SPEC.md
- src/ViewModels/RenderAppearanceViewModel.cs
- src/Views/RenderAppearanceView.xaml
- src/Commands/RenderAppearanceMatchCommand.cs
- src/Services/Interfaces/IMaterialService.cs

## Tasks

<task type="auto">
  <name>Create Settings Model & Update ViewModel</name>
  <files>
    - src/Models/RenderAppearanceSettings.cs
    - src/ViewModels/RenderAppearanceViewModel.cs
  </files>
  <action>
    1. Create `src/Models/RenderAppearanceSettings.cs` with `UVScale` (double), `SkipCompliant` (bool), and typical 4-point standard flags.
    2. Update `RenderAppearanceViewModel.cs`:
       - Add `UvScaleOptions` (List<double> with 0.5, 1.0, 2.0, 5.0).
       - Add `SelectedUvScale` property with default 1.0.
       - Initialize settings model based on chosen UI values.
  </action>
  <verify>dotnet build</verify>
  <done>ViewModel compiles and contains the scale options and selection state.</done>
</task>

<task type="auto">
  <name>Update RenderAppearanceView.xaml</name>
  <files>
    - src/Views/RenderAppearanceView.xaml
  </files>
  <action>
    1. Add a ComboBox to `RenderAppearanceView.xaml` bound to `UvScaleOptions` and `SelectedUvScale`.
    2. Add appropriate labels for "UV Scale (m)".
    3. Ensure the UI fits within the 380x350 window size.
  </action>
  <verify>
    # Visual check of XAML structure
    Get-Content src/Views/RenderAppearanceView.xaml
  </verify>
  <done>ComboBox is correctly placed and bound in the XAML.</done>
</task>

<task type="auto">
  <name>Update Service Interface & Command</name>
  <files>
    - src/Services/Interfaces/IMaterialService.cs
    - src/Commands/RenderAppearanceMatchCommand.cs
    - src/Services/MaterialService.cs
  </files>
  <action>
    1. Update `IMaterialService` methods (`BatchSyncWithRenderAppearance`) to accept `RenderAppearanceSettings`.
    2. Update `MaterialService.cs` implementation (stub the logic for now, just accept the param).
    3. Update `RenderAppearanceMatchCommand.cs` to retrieve settings from the VM and pass them to the service.
  </action>
  <verify>dotnet build</verify>
  <done>Command correctly passes VM-defined settings to the service layer without build errors.</done>
</task>

## Success Criteria
- [ ] RenderAppearanceView displays the UV Scale dropdown.
- [ ] User selection in the UI is propagated to the MaterialService.
- [ ] Project builds successfully.
