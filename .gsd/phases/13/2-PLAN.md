---
phase: 13
plan: 2
wave: 1
---

# Plan 13.2: Geometry & Alignment Batch

Objective: Convert the alignment and level-related views.

## Context
- src/Views/AlignDashboardView.xaml
- src/Views/AlignEdgesView.xaml
- src/Views/AlignElementsView.xaml
- src/Views/ChangeLevelView.xaml
- src/Views/OffsetElevationsView.xaml
- src/Views/ResetSlabsView.xaml

## Tasks

<task type="auto">
  <name>Batch Convert Geometry Views</name>
  <files>
    c:\LECG\RevitAddins\LECG\src\Views\AlignDashboardView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\AlignEdgesView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\AlignElementsView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\ChangeLevelView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\OffsetElevationsView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\ResetSlabsView.xaml
  </files>
  <action>
    For each view:
    - Change Window to base:LecgWindow.
    - Set appropriate WindowIcon (AlignCenter, Contours, Level, ArrowUpDown, etc.).
    - Remove redundant window properties (WindowStyle, AllowsTransparency, etc.).
    - Standardize main content container background and padding.
    - Ensure action buttons use standard button styles.
  </action>
  <verify>dotnet build</verify>
  <done>All 6 geometry views converted to premium style.</done>
</task>

## Success Criteria
- [ ] Views build correctly.
- [ ] Views have consistent chrome and spacing.
