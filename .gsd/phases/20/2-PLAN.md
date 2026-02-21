---
phase: 20
plan: 2
wave: 2
---

# Plan 20.2: CAD Mapper UI & Command Integration

## Objective
Create the user interface for the CAD Mapper tool and integrate it with the extraction and mapping services.

## Context
- .gsd/SPEC.md
- src/ViewModels/
- src/Views/
- src/Commands/

## Tasks

<task type="auto">
  <name>Implement CadMapperViewModel</name>
  <files>
    - src/ViewModels/CadMapperViewModel.cs
  </files>
  <action>
    - Load available `ImportInstance` elements from the project.
    - Call `CadExtractionService` to preview unique block names in the selected CAD.
    - Manage a collection of mappings (CAD String -> Revit Family).
    - Implement the "Run" command to trigger the batch placement.
  </action>
  <verify>dotnet build</verify>
  <done>ViewModel manages CAD selection and mapping state correctly.</done>
</task>

<task type="auto">
  <name>Create CadMapperView</name>
  <files>
    - src/Views/CadMapperView.xaml
    - src/Views/CadMapperView.xaml.cs
  </files>
  <action>
    - Design a "Premium Liquid Glass" interface for CAD mapping.
    - Include a ComboBox for CAD selection.
    - Include a list of mappings with Family selection (perhaps a lookup searchable list).
    - Add a progress indicator using the new `IProgressReporter`.
  </action>
  <verify>dotnet build</verify>
  <done>View is visually consistent with the design system and binds to the ViewModel.</done>
</task>

<task type="auto">
  <name>Integrate CadMapperCommand</name>
  <files>
    - src/Commands/CadMapperCommand.cs
    - src/Core/RibbonFactory.cs
  </files>
  <action>
    - Create the Revit command to launch the `CadMapperView`.
    - Register the command in the Ribbon under the new "Design-to-Model" category (or similar).
  </action>
  <verify>dotnet build</verify>
  <done>Command appears in the ribbon and launches the tool.</done>
</task>

## Success Criteria
- [ ] User can select a CAD link, see unique blocks, and map them to Revit families.
- [ ] Tool successfully places Revit instances at CAD coordinates with correct orientation.
