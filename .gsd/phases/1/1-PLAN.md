---
phase: 1
plan: 1
wave: 1
---

# Plan 1.1: Service Layer Expansion

## Objective

{What this plan delivers and why}

## Context

- .gsd/SPEC.md
- .gsd/ARCHITECTURE.md
- src/Services/Interfaces/IBaseElementCollectionService.cs
- src/Services/BaseElementCollectionService.cs
- src/Services/BatchRenameExecutionService.cs
- src/Services/Interfaces/ISearchReplaceService.cs
- src/Services/SearchReplaceService.cs

## Tasks

<task type="auto">
  <name>Update Collection Logic</name>
  <files>
    <file>src/Services/Interfaces/IBaseElementCollectionService.cs</file>
    <file>src/Services/BaseElementCollectionService.cs</file>
  </files>
  <action>
    - Add parameters for materials, objectStyles, lineStyles, and fillPatterns to CollectBaseElements method signature.
    - Implement collection logic for Material (typeof(Material)).
    - Implement collection logic for FillPatternElement (typeof(FillPatternElement)).
    - Implement collection logic for GraphicsStyle (typeof(GraphicsStyle)).
    - Filter GraphicsStyle for "Line Styles" (parent category OST_Lines) and "Object Styles" (any other subcategory).
    - Ensure only Projection styles are collected to avoid duplicates (Projection/Cut).
  </action>
  <verify>Check that the methods compile and logic handles new types.</verify>
  <done>IBaseElementCollectionService and BaseElementCollectionService updated and compiling.</done>
</task>

<task type="auto">
  <name>Update Execution Logic</name>
  <files>
    <file>src/Services/BatchRenameExecutionService.cs</file>
    <file>src/Services/Interfaces/ISearchReplaceService.cs</file>
    <file>src/Services/SearchReplaceService.cs</file>
  </files>
  <action>
    - Update BatchRenameExecutionService to handle GraphicsStyle specifically by renaming its associated Category.
    - For other elements, continue using el.Name.
    - Update ISearchReplaceService and SearchReplaceService to propagate the new collection parameters.
    - Ensure constraint safety by relying on the existing per-element try-catch block.
  </action>
  <verify>Verify that BatchRenameExecutionService contains logic for GraphicsStyle.GraphicsStyleCategory.Name rename.</verify>
  <done>Renaming logic supports subcategories and patterns; interfaces are consistent.</done>
</task>

## Success Criteria

- [ ] Collection service supports 4 new categories.
- [ ] Execution service renames subcategories via Category object.
- [ ] Code compiles without errors.
