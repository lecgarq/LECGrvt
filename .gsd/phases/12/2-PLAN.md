---
phase: 12
plan: 2
wave: 2
---

# PLAN 12.2: Scaling & Responsive Stability

Objective: Fix resizing bugs and ensure components fill the available space dynamically.

## Context
- `src/Resources/Containers.xaml`
- `src/Views/SearchReplaceView.xaml`

## Tasks

<task type="auto">
  <name>Refactor Responsive Grid Template</name>
  <files>c:\LECG\RevitAddins\LECG\src\Resources\Containers.xaml</files>
  <action>
    - Change 'SizeToContent' in 'LecgWindowStyle' from 'Height' to 'Manual' (or handle it dynamically).
    - Ensure 'ContentPresenter' in the template has 'VerticalAlignment="Stretch"'.
    - Add a 'MinHeight' to ensure windows don't collapse to thin bars.
  </action>
  <verify>Visual inspection of XAML</verify>
  <done>The window template supports stretching content to fill the actual window size.</done>
</task>

<task type="auto">
  <name>Stabilize Scroll & Filling Logic</name>
  <files>c:\LECG\RevitAddins\LECG\src\Views\SearchReplaceView.xaml</files>
  <action>
    - Remove fixed 'MaxHeight' or restrictive height bindings on DataGrids.
    - Wrap large content areas in 'Grid' rows with '*' height to ensure they expand with the window.
    - Ensure 'SizeToContent' is only used if no saved size exists.
  </action>
  <verify>dotnet build</verify>
  <done>SearchReplaceView expands correctly when the window is resized larger.</done>
</task>

## Success Criteria
- [ ] Windows can be manually resized larger without leaving dead space.
- [ ] DataGrids take up available vertical space.
- [ ] No "black boxes" appear during resizing.
