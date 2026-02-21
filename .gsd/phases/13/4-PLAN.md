---
phase: 13
plan: 4
wave: 1
---

# Plan 13.4: Complex Batch Tools

Objective: Convert the most complex remaining views.

## Context
- src/Views/ConvertCadView.xaml
- src/Views/ConvertFamilyView.xaml
- src/Views/FilterCopyView.xaml
- src/Views/PurgeView.xaml

## Tasks

<task type="auto">
  <name>Convert Complex Views</name>
  <files>
    c:\LECG\RevitAddins\LECG\src\Views\ConvertCadView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\ConvertFamilyView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\FilterCopyView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\PurgeView.xaml
  </files>
  <action>
    - 'FilterCopyView' is large and has many controls; pay extra attention to grid row/column definitions after removing the header.
    - 'PurgeView' should use the 'AlertBrush' (red) for its primary delete action if applicable, or the 'AccentButtonStyle' if it's the main safe action.
    - Reference specific icons for CAD, Family, Filter, and Trash/Purge.
  </action>
  <verify>dotnet build</verify>
  <done>Final batch of complex views converted.</done>
</task>

## Success Criteria
- [ ] No layout breakage in high-complexity views (FilterCopy).
- [ ] All Revit command windows are now standardized.
- [ ] Build passes with 0 errors.
