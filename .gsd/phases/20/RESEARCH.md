# RESEARCH 20: CAD-to-Model Automation (Point-Based)

## Objective
Establish a reliable method for extracting AutoCAD block insertion points and names from Revit ImportInstances to automate family placement.

## Findings

### 1. Geometry Extraction Pipeline
- **Target**: `ImportInstance` (Linked or Imported DWG).
- **Process**:
    1. Collect `ImportInstance` elements.
    2. Iterate through `get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })`.
    3. Recursively search for `GeometryInstance` objects.
    4. Combine `ImportInstance.GetTransform()` and `GeometryInstance.Transform` to find the world-space insertion point.

### 2. Identifying Block Names
- For CAD-resident blocks, `GeometryInstance.Symbol`'s `Name` property typically contains the AutoCAD Block Name (e.g. "Desk_1200x600").
- Note: This name may be prefixed or modified by Revit during import (e.g. "filename.dwg.BlockName").

### 3. Family Mapping Strategy
- **Mapping Logic**: A simple dictionary or Regex to match CAD Block Names to project `FamilySymbols`.
- **Search**: `FilteredElementCollector` for `FamilySymbol` matching the name or a mapping rule.

### 4. Placement Logic
- **Method**: `doc.Create.NewFamilyInstance(XYZ, FamilySymbol, StructuralType.NonStructural)`.
- **Considerations**:
    - **Rotation**: `GeometryInstance.Transform.BasisX` can be used to determine rotation.
    - **Scale**: Revit typically imports CAD at 1:1, but block references can have scale. `Transform.Scale` should be checked if precision is required.

## Risks
- **Exploded CAD**: If the CAD is exploded, block information is lost.
- **Nested Blocks**: Deeply nested blocks may require multiple levels of recursion in the geometry iterator.
- **Coordinate Systems**: Mixed internal/shared coordinates can shift points. Always multiply transformations.

## Implementation Plan (Draft)
- `ICadMappingService`: Handles the logic for mapping CAD strings to Revit families.
- `ICadExtractionService`: Handles the complex geometry iteration logic.
- `CadMapperViewModel`: UI for selecting the CAD link and defining mappings.
