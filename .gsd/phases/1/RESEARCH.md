# Research: Revit API Renaming for Materials, Styles, and Patterns

## Categories & Subcategories (Object Styles / Line Styles)

- **Object Styles**: These are `Category` objects (subcategories).
- **Line Styles**: These are subcategories of the "Lines" category (`BuiltInCategory.OST_Lines`).
- **Renaming**: Subcategories can be renamed via `Category.Name = "NewName"`.
- **Identity**: Subcategories have an `Id` (ElementId). However, `Document.GetElement(Id)` might return `null` if the ID belongs to a permanent BuiltIn category, but for user-created subcategories, it often returns a `GraphicsStyle` or similar internal element? No, `Category` is NOT an `Element`.
- **Resolution**: Most subcategories are associated with a `GraphicsStyle`.
  - `GraphicsStyle` elements can be collected.
  - `GraphicsStyle.GraphicsStyleCategory` gives the `Category`.
  - Only collect `GraphicsStyleType.Projection` to avoid duplicates (Cut styles exist too).
  - Renaming `Category.Name` renames both Projection and Cut styles.

## Materials

- **Class**: `Autodesk.Revit.DB.Material`
- **Renaming**: `Material.Name = "NewName"` works directly.
- **Collection**: `new FilteredElementCollector(doc).OfClass(typeof(Material))`.

## Fill Patterns

- **Class**: `Autodesk.Revit.DB.FillPatternElement`
- **Renaming**: `FillPatternElement.Name = "NewName"` works directly.
- **Collection**: `new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement))`.
- **Note**: Some built-in patterns might be read-only. Need try-catch block (already present).

## Line Styles specifically

- Filter `GraphicsStyle` elements where `GraphicsStyleCategory.Parent.Id.Value == (long)BuiltInCategory.OST_Lines`.

## Implementation Strategy

1. **Collector**: Add new switches to `CollectBaseElements`.
2. **Execution**: In `BatchRenameExecutionService`, check if the element is a `GraphicsStyle`. If so, rename its `GraphicsStyleCategory`. Otherwise, use `el.Name`.

## Constraint Safety

- Renaming these elements does not change their `ElementId`.
- Parameters and references (e.g. material assigned to a wall) use the `ElementId`, so they remain intact.
- **Exception**: If some external script (like Dynamo or another plugin) relies on the *string name* of a material, it might break. But in pure Revit, constraints are ID-based.
