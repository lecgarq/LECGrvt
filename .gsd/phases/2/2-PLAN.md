---
phase: 2
plan: 2
wave: 2
---

# Plan 2.2: Purge UI and Exposure

## Objective
Expose the "Deep Purge" functionality in the UI and ensure clear feedback to the user during iterative cleaning.

## Context
- src/ViewModels/PurgeViewModel.cs
- src/Views/PurgeView.xaml
- src/Views/PurgeView.xaml.cs

## Tasks

<task type="auto">
  <name>Update Purge ViewModel</name>
  <files>
    <file>src/ViewModels/PurgeViewModel.cs</file>
  </files>
  <action>
    - Add `[ObservableProperty] private bool _isDeepPurge;`
    - Add logic to return `3` if `IsDeepPurge` is true, otherwise `1` (or bind this in the command).
  </action>
  <verify>Check property existence in VM.</verify>
  <done>ViewModel has IsDeepPurge property.</done>
</task>

<task type="auto">
  <name>Update Purge View</name>
  <files>
    <file>src/Views/PurgeView.xaml</file>
    <file>src/Views/PurgeView.xaml.cs</file>
  </files>
  <action>
    - Add a `CheckBox` or `Toggle` for "Deep Purge (3 Passes)" in the Options panel.
    - Update the `Run_Click` handler in `PurgeView.xaml.cs` to pass the `passCount` (1 or 3) to the `PurgeAll` service call.
  </action>
  <verify>Visual check of the UI layout.</verify>
  <done>UI allows selection of deep purge and passes it to the service.</done>
</task>

## Success Criteria
- [ ] UI shows "Deep Purge (3 Passes)" option.
- [ ] User can toggle between 1-pass and 3-pass cleaning.
- [ ] UI remains clean and functional.
