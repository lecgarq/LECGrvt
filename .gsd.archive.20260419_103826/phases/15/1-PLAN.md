---
phase: 15
plan: 1
wave: 1
---

# Plan 15.1: Geometry Conversion Stabilization

## Objective
Repair the Floor-to-Toposolid and Toposolid-to-Floor conversion tools by correcting the Z-coordinate math for sub-element points.

## Context
- .gsd/SPEC.md
- src/Services/ConversionService.cs
- src/Services/Interfaces/ISlabService.cs

## Tasks

<task type="auto">
  <name>Stabilize Toposolid to Floor Z-Coordinates</name>
  <files>src/Services/ConversionService.cs</files>
  <action>
    - Update `ConvertSingleToposolidToFloor` in `src/Services/ConversionService.cs`.
    - Correct the `relativeZ` calculation to use Z relative to the **target Floor level + its Height Offset**.
    - Formula: `relativeZ = vertexAbsoluteZ - (targetLevel.Elevation + targetHeightOffset)`.
    - This ensures shape points are placed correctly on the Floor's plane instead of being placed at absolute project Z.
  </action>
  <verify>dotnet build</verify>
  <done>Code compiles and logic uses relative subtraction for Floor shape points.</done>
</task>

<task type="auto">
  <name>Stabilize Floor to Toposolid Z-Coordinates</name>
  <files>src/Services/ConversionService.cs</files>
  <action>
    - Update `BuildAbsolutePoints` in `src/Services/ConversionService.cs`.
    - Ensure the translation from Floor `SlabShapeVertex.Position.Z` (which is relative to the Floor's plane) to Toposolid's input points (which are absolute) is consistent.
    - Toposolid points for `Toposolid.Create` must be in absolute project coordinate space.
    - Check if `levelElevation` and `heightOffset` are being applied correctly without double-offsetting.
  </action>
  <verify>dotnet build</verify>
  <done>Code compiles and logic correctly identifies absolute Z for Toposolid.Create.</done>
</task>

<task type="auto">
  <name>Robust Level and Offset Resolution</name>
  <files>src/Services/ConversionService.cs</files>
  <action>
    - Update `GetTargetHeightOffset` to handle cases where `BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM` might be missing or read-only (e.g., certain Toposolid types).
    - Ensure consistent base elevation logic between Floors and Toposolids.
  </action>
  <verify>dotnet build</verify>
  <done>Code correctly calculates offsets between relative source and target levels.</done>
</task>

## Success Criteria
- [ ] Z-coordinate mismatch for Toposolid-to-Floor is resolved (becomes relative).
- [ ] Floor-to-Toposolid preserves absolute vertex elevations.
- [ ] Project builds without errors.
