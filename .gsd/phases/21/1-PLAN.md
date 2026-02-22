---
phase: 21
plan: 1
wave: 1
---

# Plan 21.1: Category Creator Engine (Transplantation)

## Objective
Create a "Creator Engine" that bypasses Revit's category replacement limits by extracting geometry from an existing family and injecting it into a fresh family of the desired category.

## Context
- `src/Services/Interfaces/IFamilyEditorService.cs`
- `src/Services/FamilyEditorService.cs`
- `src/ViewModels/CategoryChangerViewModel.cs`

## Tasks

<task type="auto">
  <name>Implement Geometry Harvesting & Injection</name>
  <files>
    <file>c:\LECG\RevitAddins\LECG\src\Services\Interfaces\IFamilyEditorService.cs</file>
    <file>c:\LECG\RevitAddins\LECG\src\Services\FamilyEditorService.cs</file>
  </files>
  <action>
    1. Update `IFamilyEditorService` with `Family RecreateAs(Family sourceFamily, Category targetCategory)`.
    2. Implement `RecreateAs` in `FamilyEditorService`:
       - Identify if it is a 2D to 3D jump.
       - Use `projectDoc.Application.NewFamilyDocument(templatePath)` to create a new, blank 3D family.
       - Use `ElementTransformUtils.CopyElements` to move lines/regions from source to target.
       - Load the new family into the project with a `[TRANSPLANTED]` suffix.
  </action>
  <verify>dotnet build</verify>
  <done>Service core supports programmatic family re-creation across category bounds.</done>
</task>

<task type="auto">
  <name>Wire Creator Engine into UI</name>
  <files>
    <file>c:\LECG\RevitAddins\LECG\src\ViewModels\CategoryChangerViewModel.cs</file>
    <file>c:\LECG\RevitAddins\LECG\src\Commands\CategoryChangerCommand.cs</file>
  </files>
  <action>
    1. Update the `CategoryChangerEventHandler` to check the result of `ChangeCategory`.
    2. If `ChangeCategory` fails due to a "Revit Platform Limit" (the error we just saw), automatically call `RecreateAs`.
    3. Log the "Transplantation" process to the user so they know a new family was created.
  </action>
  <verify>Manual observation in Revit: "Detail Item" becomes a "Furniture" family with a new name.</verify>
  <done>User can click a single button to convert a Detail Item bed into a Furniture bed.</done>
</task>

## Success Criteria
- [ ] Detail Item geometry is preserved in the new Model Category.
- [ ] No more "Input category id cannot be assigned" errors.
- [ ] The engine automatically handles the 2D->3D gap.
