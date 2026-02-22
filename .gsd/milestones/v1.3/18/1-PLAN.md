---
phase: 18
plan: 1
wave: 1
---

# Plan 18.1: Service Layer & Replace Logic

## Objective
Enhance the conversion engine to support "In-Place Replacement" logic, preserving instance location, rotation, and parameter values.

## Context
- .gsd/SPEC.md
- .gsd/phases/18/RESEARCH.md
- src/Services/FamilyConversionService.cs
- src/Services/Interfaces/IFamilyConversionService.cs

## Tasks

<task type="auto">
  <name>Implement FamilyInstanceData Helper</name>
  <files>
    <file>src/Models/FamilyInstanceData.cs</file>
  </files>
  <action>
    Create a helper class `FamilyInstanceData` that can:
    1. Capture the state of a `FamilyInstance` (Location, Rotation, Hand/Facing flip, Level, Host, and Instance Parameters).
    2. Apply that state to a new `FamilyInstance` by matching parameter names.
    - Ensure it handles `LocationPoint` and `LocationCurve`.
    - Use `ArgumentNullException.ThrowIfNull` for inputs.
  </action>
  <verify>Check that the file exists and contains Capture/Apply methods.</verify>
  <done>FamilyInstanceData class implemented with Location and Parameter mapping logic.</done>
</task>

<task type="auto">
  <name>Update Conversion Interfaces & Implementation</name>
  <files>
    <file>src/Services/Interfaces/IFamilyConversionService.cs</file>
    <file>src/Services/FamilyConversionService.cs</file>
  </files>
  <action>
    1. Update `IFamilyConversionService` to include:
       - `ConvertFamilyBatch(Document doc, IEnumerable<FamilyInstance> instances, string customName, string templatePath, bool isTemporary, bool replaceInPlace)`
    2. Implement `ConvertFamilyBatch` in `FamilyConversionService`.
    3. If `replaceInPlace` is true:
       - Capture `FamilyInstanceData`.
       - Place new instance using `doc.Create.NewFamilyInstance`.
       - Apply captured data.
       - Delete original instance.
    - Ensure transactions are handled safely (either per element or batch).
  </action>
  <verify>Build the project and ensure no compiler errors.</verify>
  <done>Conversion service supports batching and in-place replacement logic.</done>
</task>

## Success Criteria
- [ ] `FamilyInstanceData` successfully maps parameters by name.
- [ ] `FamilyConversionService` can process a collection of instances.
- [ ] Build completes without errors.
