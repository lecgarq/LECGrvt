---
phase: 1
plan: 1
wave: 1
---

# Plan 1.1: Colors and Dictionary Initialization

## Objective
Establish the foundational ResourceDictionary for the LECG UI framework, specifically implementing the exact 9-color HEX earth-tone palette requested as explicit SolidColorBrush static resources for broad consumption.

## Context
- .gsd/SPEC.md
- .gsd/ARCHITECTURE.md
- .gsd/DECISIONS.md

## Tasks

<task type="auto">
  <name>Create LECG Colors Dictionary</name>
  <files>src/Resources/Themes/LecgColors.xaml</files>
  <action>
    - Create a new valid WPF ResourceDictionary mapped to the application namespace.
    - Implement exactly the 9 hex tones (`#E6E3DA`, `#C8C0B4`, `#A89D8E`, `#7A634F`, `#E9E9E6`, `#96938C`, `#4E4B44`, `#323130`, `#708452`) as SolidColorBrush resources.
    - Give them strictly professional semantic names like `LecgBackground`, `LecgBorder`, `LecgAccent` mapped explicitly to those values instead of arbitrary color names so they scale cleanly.
    - Create a `src/Resources/Themes` target directory securely without destroying existing layout.
  </action>
  <verify>Get-Content "src/Resources/Themes/LecgColors.xaml" | Select-String "#7A634F"</verify>
  <done>The 9 palette colors are available generically via DynamicResource/StaticResource bindings.</done>
</task>

<task type="auto">
  <name>Initialize Component Dictionary Registry</name>
  <files>src/Resources/Themes/LecgTheme.xaml, src/App.cs</files>
  <action>
    - Create `LecgTheme.xaml` as a MergedDictionary that includes `LecgColors.xaml`.
    - Modify the application initialization logic (usually inside `src/App.cs` or the main Revit `App` `IExternalApplication` instantiation) to dynamically load `LecgTheme.xaml` into `Application.Current.Resources.MergedDictionaries` at startup so all Windows inherently inherit these colors.
  </action>
  <verify>Select-String -Path src/App.cs -Pattern "LecgTheme.xaml"</verify>
  <done>All future spawned views will automatically digest the LECG palette dictionary without local redefinition.</done>
</task>

## Success Criteria
- [ ] XAML dictionaries successfully compile with MSBuild in `net8.0-windows`.
- [ ] No explicit hex codes exist outside of `LecgColors.xaml`.
