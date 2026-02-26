---
phase: 22
plan: 1
wave: 1
---

# Plan 22.1: Rebuilding Convert Family Logic (V2)

## Objective
Replace the fragmented, silent-failing family conversion services with a consolidated, robust "Workaround Engine" that reliably transfers geometry from hosted templates to workplane-based generic models with explicit user feedback.

## Context
- `src/Services/FamilyConversionService.cs` (Current fragmented entry point)
- `src/Services/FamilyConversionExecutionService.cs` (Complex nesting/copy logic)
- `src/Services/FamilyGeometryCollectionService.cs` (Limited element collection)
- `src/Commands/ConvertFamilyCommand.cs` (Command gate)

## Tasks

<task type="auto">
  <name>Consolidate Conversion Service (V2)</name>
  <files>
    <file>src/Services/FamilyConversionExecutionService.cs</file>
    <file>src/Services/FamilyGeometryCollectionService.cs</file>
  </files>
  <action>
    1. Refactor `FamilyGeometryCollectionService` to collect ALL relevant elements (Model Lines, Reference Planes, Reference Lines, Forms) to ensure nothing is left behind.
    2. Simplify `FamilyConversionExecutionService.Execute` to skip the fragile 'Nesting' strategy by default and use a robust 'Direct Transfer' (CopyElements) approach as the primary method.
    3. Add explicit `Logger.Instance.Log()` calls after EVERY step (Document Create, Collection, Copy, Save, Load).
  </action>
  <verify>Build the project and ensure 0 errors.</verify>
  <done>Execution service uses consolidated logic with broader element collection.</done>
</task>

<task type="auto">
  <name>Enhance Command Feedback & Error Handling</name>
  <files>
    <file>src/Commands/ConvertFamilyCommand.cs</file>
  </files>
  <action>
    1. Remove the 'V8 Gate Check' diagnostic TaskDialogs and replace with a single 'Pre-flight Confirmation' showing element count and target template.
    2. Ensure `ShowLogWindow` is called BEFORE the heavy processing starts.
    3. Wrap the batch call in a way that captures and logs every instance failure individually, instead of dying silently.
  </action>
  <verify>Run the command in Revit and observe the log window opening and showing steps.</verify>
  <done>Command provides clear feedback and logs every step of the process.</done>
</task>

## Success Criteria
- [ ] User sees a log window with real-time progress steps.
- [ ] Hosted geometry (forms + lines + ref planes) is correctly transferred to a new Workplane-based family.
- [ ] Original instances are swapped with new ones at the same coordinates.
