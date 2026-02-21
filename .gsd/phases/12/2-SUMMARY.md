---
phase: 12
plan: 2
wave: 2
---

# SUMMARY 12.2: Scaling & Responsive Stability

Improved widow resizing behavior and content responsiveness.

## Changes:
- **Style Refactor**: Updated `LecgWindowStyle` in `Containers.xaml` to support manual resizing by removing `SizeToContent="Height"`.
- **Min Constraints**: Established `MinWidth="350"` and `MinHeight="200"` global defaults to prevent window collapse.
- **Filling Logic**: Ensured `ContentPresenter` uses `VerticalAlignment="Stretch"` so internal grids expand with the window.
- **DataGrid Fix**: Removed restrictive `MaxHeight` from `SearchReplaceView.xaml` and transitioned its main Grid to a responsive `*` row for the preview area.
- **Cleanup**: Removed hardcoded `WindowStartupLocation` from views to prefer the `LecgWindow.cs` orchestration.

## Verification:
- `dotnet build` passed.
- XAML structure verified for row expansion.
