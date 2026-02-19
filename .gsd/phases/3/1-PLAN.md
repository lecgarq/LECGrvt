---
phase: 3
plan: 1
wave: 1
---

# Plan 3.1: Selection Filter & Naming Safety

## Objective
Restrict family selection to safe types and prevent naming collisions during the conversion process.

## Context
- .gsd/SPEC.md
- src/Utils/FamilyInstanceFilter.cs
- src/Services/Interfaces/IFamilyConversionNamingService.cs
- src/Services/FamilyConversionNamingService.cs

## Tasks

<task type="auto">
  <name>Refine Selection Filter</name>
  <files>
    <file>src/Utils/FamilyInstanceFilter.cs</file>
  </files>
  <action>
    - Update `AllowElement` to cast to `FamilyInstance`.
    - Retrieve `instance.Symbol.Family`.
    - Return `true` only if the instance is NOT work-plane based (`!family.IsWorkPlaneBased`).
    - This prevents selecting complex families that require workplane context not yet handled by the geometry copy service.
  </action>
  <verify>Check method logic in file.</verify>
  <done>Filter restricts selection based on IsWorkPlaneBased.</done>
</task>

<task type="auto">
  <name>Implement Naming Collision Check</name>
  <files>
    <file>src/Services/Interfaces/IFamilyConversionNamingService.cs</file>
    <file>src/Services/FamilyConversionNamingService.cs</file>
  </files>
  <action>
    - Update `ResolveTargetFamilyName` to accept `Document doc`.
    - In implementation, use a `FilteredElementCollector` to check if a `Family` already exists with the proposed `customName`.
    - If it exists, return a name with a numerical suffix (e.g., _Converted_1) or handle as per SPEC.
  </action>
  <verify>Check for Collector usage in file.</verify>
  <done>Naming service prevents overwriting existing families by detecting name collisions.</done>
</task>

## Success Criteria
- [ ] Selection filter blocks work-plane based families.
- [ ] Naming service detects existing families in the document.
- [ ] Documented evidence of safe naming logic.
