# RESEARCH 18: In-Place Family Replacement Logic

## Objective
Determine the most robust method for replacing a family instance with a new one (potentially different category/template) while preserving location, orientation, and parameter data.

## Findings

### 1. Placement Methods
Depending on the source element's placement type, we need to use different `NewFamilyInstance` overloads:
- **Point-based**: `doc.Create.NewFamilyInstance(XYZ, FamilySymbol, Structure.StructuralType)`
- **Line-based**: `doc.Create.NewFamilyInstance(Curve, FamilySymbol, Structure.StructuralType)`
- **Hosted**: `doc.Create.NewFamilyInstance(Reference, XYZ, XYZ, FamilySymbol)`

### 2. Orientation & Rotation
- Point-based instances can have a rotation angle.
- We need to capture `(instance.Location as LocationPoint).Rotation`.
- After placement, use `ElementTransformUtils.RotateElement`.

### 3. Parameter Mapping
- Iterate all parameters of the source instance.
- For each parameter, check if the target instance has a parameter with the same name.
- If it exists and is not read-only, copy the value.
- Special handling for:
    - Shared Parameters.
    - Built-in Parameters.
    - Type Parameters (if we are also converting type info).

### 4. Hosting Preservation
- If the source is hosted (on a Wall, Floor, etc.), we should try to place the new instance on the same host.
- Use `instance.Host` and `instance.HostFace`.

## Proposed Algorithm

1. **Extraction**:
    - Get `LocationPoint` or `LocationCurve`.
    - Map all Instance Parameters to a `Dictionary<string, object>`.
    - Identify Host and Level.
2. **Generation**:
    - Run the existing Phase 17 conversion to get the new `FamilySymbol`.
3. **Placement**:
    - Place new instance using the extracted location/host data.
4. **Correction**:
    - Apply rotation/flip status.
    - Set parameters from the dictionary.
5. **Finalization**:
    - Collect IDs of old elements for deletion.
    - Verify new element is valid.

## Risks
- **Constraint Breakage**: Dimensions or tags referencing the old element will be lost unless we can re-host them (very hard).
- **Incompatible Templates**: Converting a Face-based family to a Level-based one will change behavior.

## Conclusion
The tool should focus on "Best Effort" preservation. We will warn the user about dimensions/tags.
