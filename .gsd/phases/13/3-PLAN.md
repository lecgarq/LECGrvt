---
phase: 13
plan: 3
wave: 1
---

# Plan 13.3: Processing & Conversion Batch

Objective: Convert the material, graphic, and mesh processing views.

## Context
- src/Views/AssignMaterialView.xaml
- src/Views/RenderAppearanceView.xaml
- src/Views/SexyRevitView.xaml
- src/Views/SimplifyPointsView.xaml
- src/Views/UpdateContoursView.xaml

## Tasks

<task type="auto">
  <name>Batch Convert Processing Views</name>
  <files>
    c:\LECG\RevitAddins\LECG\src\Views\AssignMaterialView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\RenderAppearanceView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\SexyRevitView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\SimplifyPointsView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\UpdateContoursView.xaml
  </files>
  <action>
    - Repeat the LecgWindow conversion pattern for these 5 files.
    - Focus on 'SexyRevitView' and 'UpdateContoursView' as they have more complex internal layouts.
    - Ensure 'SimplifyPoints' and 'UpdateContours' use the correct mesh-related icons.
  </action>
  <verify>dotnet build</verify>
  <done>Processing views converted.</done>
</task>

## Success Criteria
- [ ] Build passes.
- [ ] Consistent premium look across the processing suite.
