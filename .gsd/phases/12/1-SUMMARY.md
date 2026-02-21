---
phase: 12
plan: 1
wave: 1
---

# SUMMARY 12.1: Window State Persistence

Implemented robust window state persistence for all Revit command windows.

## Changes:
- **Model**: Created `LECG.Models.WindowSettings` to store bounds and state.
- **Persistence Logic**: Added `LoadWindowState` and `SaveWindowState` to `LecgWindow.cs` using the global `SettingsManager`.
- **Automatic Setup**: Windows use their type name (e.g., `SearchReplaceView_Settings.json`) for unique persistence.
- **Off-screen Safety**: Implemented `EnsureVisible()` to prevent windows from opening on disconnected monitors.
- **Improved Centering**: Adjusted centering logic to only trigger if no saved position exists.

## Verification:
- Build passes.
- Code logic reviewed for multi-monitor edge cases.
