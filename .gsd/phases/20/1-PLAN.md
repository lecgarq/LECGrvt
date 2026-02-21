---
phase: 20
plan: 1
wave: 1
---

# Plan 20.1: Point-Based CAD Mapping Services

## Objective
Implement the core logic for extracting block data from linked CAD files and mapping them to Revit FamilySymbols.

## Context
- .gsd/SPEC.md
- .gsd/ARCHITECTURE.md
- .gsd/phases/20/RESEARCH.md
- src/Services/Interfaces/

## Tasks

<task type="auto">
  <name>Implement CadExtractionService</name>
  <files>
    - src/Models/CadBlockData.cs
    - src/Services/Interfaces/ICadExtractionService.cs
    - src/Services/CadExtractionService.cs
  </files>
  <action>
    - Create `CadBlockData` model to hold `Name`, `InsertionPoint` (XYZ), and `Rotation`.
    - Implement `CadExtractionService` to iterate through `ImportInstance` geometry.
    - Use a recursive helper to find all `GeometryInstance` blocks.
    - Properly combine parent and instance transforms as documented in RESEARCH.md.
  </action>
  <verify>dotnet build</verify>
  <done>Service compiles and contains logic to walk the geometry tree of an ImportInstance.</done>
</task>

<task type="auto">
  <name>Implement CadMappingService</name>
  <files>
    - src/Models/CadMappingItem.cs
    - src/Services/Interfaces/ICadMappingService.cs
    - src/Services/CadMappingService.cs
  </files>
  <action>
    - Create `CadMappingItem` to link a CAD block name to a Revit `FamilySymbol` ID.
    - Implement `CadMappingService` to orchestrate the placement of instances based on extraction results and user-defined mappings.
    - Automate the placement using `doc.Create.NewFamilyInstance`.
  </action>
  <verify>dotnet build</verify>
  <done>Mapping service implemented and integrated into the service layer.</done>
</task>

## Success Criteria
- [ ] CAD extraction logic correctly identifies insertion points and block names from DWG links.
- [ ] Core services follow the existing dependency injection patterns.
