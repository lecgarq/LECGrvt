# Plan 1.1 Summary: Service Layer Expansion

## Objective
Extend the collection and renaming logic in the background services to support Materials, Object Styles, Line Styles, and Fill Patterns.

## Changes
- Updated `IBaseElementCollectionService` and `BaseElementCollectionService` to handle new element types including `Material`, `FillPatternElement`, and `GraphicsStyle`.
- Implemented specific filtering for `GraphicsStyle` to distinguish between "Line Styles" and "Object Styles" using their parent category.
- Updated `ISearchReplaceService` and `SearchReplaceService` interfaces to support the new collection parameters.
- Updated `BatchRenameExecutionService` to handle `GraphicsStyle` elements by renaming their underlying `Category` object, ensuring correct behavior for subcategories.

## Verification Results
- All services compile successfully.
- Logic correctly identifies "Line Styles" by parentage (OST_Lines).
- Batch renaming logic now branches for `GraphicsStyle` objects.

## Risks/Debt
- Renaming protected BuiltIn categories might fail (handled by existing try-catch).
- Large models might see slightly slower collection times due to 3 additional collectors.
