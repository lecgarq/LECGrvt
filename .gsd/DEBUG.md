# Debug Session: Filter Built-in Object Styles from Batch Rename

## Symptom
The Batch Rename tool lists built-in Revit object styles (e.g., "Lines", "Thin Lines") which the user does not want to see or rename. Rationale: Users only care about user-created subcategories (e.g., "A-WALL-DEMO" or imported styles).

**When:** Opening the Batch Rename tool with "Object Styles" scope selected.
**Expected:** Only user-created subcategories/styles should appear in the list.
**Actual:** All built-in styles are listed.

## Evidence

### Code Check
- Need to check `SearchReplaceService.cs` or `BaseElementCollectionService.cs` (where elements are collected).
- `GraphicsStyle` elements often have a mapped `Category`.
- `Category` has an `Id` property. Built-in categories have integer IDs corresponding to `BuiltInCategory` enum.
- User-created subcategories usually have positive IDs that don't map to `BuiltInCategory`, but the best check is `Category.IsTag` (not relevant) or checking if the ID corresponds to a built-in category.
- **Key Check**: `Category.BuiltInCategory` property returns `BuiltInCategory.INVALID` for custom subcategories? Or we check if the ID is within a certain range?
- Better check: `Category.Parent != null` implies a subcategory, but some built-in subcategories exist.
- Standard approach: Filter out categories where `Enum.IsDefined(typeof(BuiltInCategory), cat.Id.IntegerValue)` is true? No, because `Category.Id` is an `ElementId`.
- Correct check: `Category.IsCuttable`, `Category.CanAddSubcategory`?
- **Hypothesis**: We can check if the underlying `Category` returns a valid `BuiltInCategory` other than `INVALID`. Custom subcategories might return `INVALID`.

## Hypotheses

| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `GraphicsStyle.GraphicsStyleCategory.BuiltInCategory` is `INVALID` (-1) for user-created styles. | 90% | UNTESTED |
| 2 | We need to filter by `Category.Parent != null` AND checks on the parent. | 50% | UNTESTED |

## Approach
1.  Locate the collection logic (`CollectBaseElements` or similar).
2.  Implement a filter to exclude `GraphicsStyle` elements that map to a valid `BuiltInCategory`.
3.  Specifically, we want to KEEP:
    *   Imported styles (Imports in Families).
    *   User-created subcategories.
4.  We want to EXCLUDE:
    *   Standard system Categories (Walls, Doors, etc. - usually not `GraphicsStyle` but their Object Style representation).
    *   Standard Line Styles (Thin Lines, Medium Lines, etc. might be built-in).

Let's check the code to see how they are currently collected.
