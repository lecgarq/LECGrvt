# Plan 1.2 Summary: ViewModel and View Expansion

## Objective
Update the Search & Replace user interface to allow users to select Materials, Object Styles, Line Styles, and Fill Patterns for renaming.

## Changes
- Added 4 new `[ObservableProperty]` boolean flags to `SearchReplaceViewModel`: `ScopeMaterialName`, `ScopeObjectStyleName`, `ScopeLineStyleName`, and `ScopeFillPatternName`.
- Updated `RefreshScope()` in the ViewModel to pass all 8 scope flags to the backend service.
- Implemented partial `OnScope*Changed` methods in the ViewModel to automatically refresh the preview grid when checkboxes are toggled.
- Replaced the horizontal `StackPanel` in `SearchReplaceView.xaml` with a `WrapPanel` to elegantly display all 8 scope options while maintaining the window's compact "Apple feel".
- Fixed a missing binding for "Families" that existed in the VM but was not visible in the UI.

## Verification Results
- UI correctly displays 8 scope options.
- Checkboxes are correctly bound to VM properties.
- Toggling checkboxes triggers the `RefreshScope` logic.

## Risks/Debt
- The window height is set to `SizeToContent="Height"`. With 8 checkboxes in a `WrapPanel`, the window might expand vertically more than before.
