# Research: Selection & Selection Safety (Phase 3)

## Selection Filter Refinement
- **Current Filter**: `FamilyInstanceFilter.cs` allows any `FamilyInstance`.
- **Requirement**: Check `Family.IsWorkPlaneBased`.
- **Implementation**: `instance.Symbol.Family.IsWorkPlaneBased` must be checked. Work-plane based families can cause issues during certain types of conversion if not handled. The spec requires restricting selection to avoid complex host/plane issues in the first version.

## Naming Collisions
- **Current Naming**: `FamilyConversionNamingService.cs` just appends `_Converted`.
- **Requirement**: Pre-flight collision check.
- **Implementation**: Needs access to `Document` to check `new FilteredElementCollector(doc).OfClass(typeof(Family))` for name matches.

## Hosting Validation
- **Context**: Revit families can be hosted (Walls, Floors, Roofs, etc.) or non-hosted (Level-based, Work-plane based).
- **Complexity**: Replacing a hosted family instance with a new type might fail if the new type has different hosting requirements.
- **Goal**: Add a validation step in `FamilyConversionService` to warn or block if the instance has a host that might be lost or changed.
