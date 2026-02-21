---
phase: 13
plan: 1
wave: 1
---

# Plan 13.1: Core Dashboards & Components

Objective: Convert the main dashboard and auxiliary views to the new standard.

## Context
- .gsd/phases/13/RESEARCH.md
- src/Views/HomeView.xaml
- src/Views/LogView.xaml
- src/Views/Components/SelectionControl.xaml

## Tasks

<task type="auto">
  <name>Convert Dashboards (Home & Log)</name>
  <files>
    c:\LECG\RevitAddins\LECG\src\Views\HomeView.xaml
    c:\LECG\RevitAddins\LECG\src\Views\LogView.xaml
  </files>
  <action>
    - Ensure both use 'base:LecgWindow'.
    - Remove 'WindowStartupLocation="CenterScreen"'.
    - Set 'WindowIcon' using {x:Static icons:Icons.Home} (for Home) and {x:Static icons:Icons.Layers} (for Log).
    - In HomeView, ensure the main Grid uses standard margins.
    - In LogView, ensure buttons use the consolidated 'AccentButtonStyle' or 'SecondaryButtonStyle'.
  </action>
  <verify>dotnet build</verify>
  <done>Home and Log views use the new standard chrome and icons.</done>
</task>

<task type="auto">
  <name>Standardize Components</name>
  <files>c:\LECG\RevitAddins\LECG\src\Views\Components\SelectionControl.xaml</files>
  <action>
    - Audit SelectionControl for ad-hoc styles.
    - Reference global design tokens (Colors, Brushes) from Styles.xaml.
  </action>
  <verify>Visual inspection</verify>
  <done>SelectionControl is style-compliant.</done>
</task>

## Success Criteria
- [ ] HomeView and LogView have the premium title bar and correct icons.
- [ ] Build passes with 0 errors.
