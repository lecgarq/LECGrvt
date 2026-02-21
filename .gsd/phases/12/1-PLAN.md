---
phase: 12
plan: 1
wave: 1
---

# PLAN 12.1: Window State Persistence

Objective: Implement a robust system to remember window position and size across sessions using the existing `SettingsManager`.

## Context
- `src/Services/SettingsManager.cs`
- `src/Views/Base/LecgWindow.cs`

## Tasks

<task type="auto">
  <name>Create WindowSettings Model</name>
  <files>c:\LECG\RevitAddins\LECG\src\Models\WindowSettings.cs</files>
  <action>
    Create a new model class to store window bounds (Left, Top, Width, Height, State).
    Include a 'FileName' property or logic to derive a unique settings file based on the window's Type name.
  </action>
  <verify>Check file existence</verify>
  <done>Model class is defined and compiles.</done>
</task>

<task type="auto">
  <name>Implement Save/Load logic in LecgWindow</name>
  <files>c:\LECG\RevitAddins\LECG\src\Views\Base\LecgWindow.cs</files>
  <action>
    - Add logic to 'OnClosing' to save current bounds.
    - Add logic to 'OnSourceInitialized' to restore bounds.
    - IMPORTANT: Add a 'VirtualScreen' safety check to ensure the window is not restored off-screen (e.g., if a monitor was disconnected).
  </action>
  <verify>dotnet build</verify>
  <done>LecgWindow successfully saves and restores its state.</done>
</task>

## Success Criteria
- [ ] Closing a window and reopening it restores its previous position.
- [ ] Windows handle maximized/normal states correctly.
- [ ] Build passes with 0 errors.
