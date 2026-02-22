---
phase: 19
plan: 2
wave: 2
---

# Plan 19.2: Progress Reporting & Final Validation

## Objective
Implement a standard progress reporting mechanism for batch services and perform a final performance audit.

## Context
- src/Services/Interfaces/IFamilyConversionService.cs
- src/ViewModels/BaseViewModel.cs
- src/Views/Components/ProgressBarControl.xaml (To be created or updated)

## Tasks

<task type="auto">
  <name>Implement IProgressReporter Interface</name>
  <files>
    <file>src/Services/Interfaces/IProgressReporter.cs</file>
  </files>
  <action>
    Create diagnostic interface for tracking progress:
    - `void Report(string message, double percentage)`
    - Implement a `SimpleProgress` class that ViewModels can pass to services.
  </action>
  <verify>Check interface definition.</verify>
  <done>Progress reporting infrastructure ready.</done>
</task>

<task type="auto">
  <name>Integrate Progress into Batch Conversion</name>
  <files>
    <file>src/Services/Interfaces/IFamilyConversionService.cs</file>
    <file>src/Services/FamilyConversionService.cs</file>
    <file>src/ViewModels/ConvertFamilyViewModel.cs</file>
  </files>
  <action>
    1. Update `ConvertFamilyBatch` signature to accept `IProgress<ProgressReport>` or custom reporter.
    2. Call reporter during iteration of families and instances.
    3. Update `ConvertFamilyViewModel` to show progress in the UI (e.g. status message).
  </action>
  <verify>Run the tool and observe progress and timing logs.</verify>
  <done>UI provides feedback during long batch conversions.</done>
</task>

## Success Criteria
- [ ] UI shows "Processing instance 5 of 10..." or similar feedback.
- [ ] No UI hangs longer than 5 seconds without feedback.
