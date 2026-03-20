# Phase 15 Research: Geometry Operations Stabilization

> Investigating failures in core geometry tools targeting Revit 2026.

## Findings

### 1. Conversion Pair (Floor <-> Toposolid)
The current implementation in `ConversionService.cs` has a critical coordinate system mismatch.

- **Toposolid to Floor**: 
  - `ConvertSingleToposolidToFloor` retrieves absolute Z coordinates from the Toposolid vertices.
  - It then calls `editor.AddPoint(new XYZ(vertex.X, vertex.Y, vertex.Z))` on the new Floor.
  - **Issue**: `SlabShapeEditor.AddPoint` for Floors expects Z coordinates **relative to the Floor's base Level + Height Offset**. Passing absolute Z values causes the points to be displaced by hundreds or thousands of feet.
- **Floor to Toposolid**:
  - `Toposolid.Create` in Revit 2026 behaves differently depending on how `topoPoints` are defined.
  - Currently, we use `absoluteZ = levelElevation + heightOffset + vertex.Z`.
  - **Risk**: If the Toposolid is created at a Level, its internal coordinate system for sub-elements might be relative to that level, leading to double-offsetting.

### 2. Fix Points (Repair)
The `FixPointsService.cs` uses an Inverse Distance Weighting (IDW) algorithm.

- **Issue**: Revit uses Delaunay triangulation to define its surfaces. IDW generates a "smooth" average that rarely matches the existing planar triangulation.
- **Consequence**: Repairing a point using IDW actually *introduces* non-planarity relative to the immediate neighbors, causing more triangulation artifacts (spikes) rather than fixing them.
- **Proposed Fix**: Switch to a "Point-on-Triangle-Plane" logic or use Revit's built-in `SlabShapeEditor` behavior where possible.

### 3. Split Boundaries
`SplitBoundariesService.cs` uses a custom Point-in-Polygon (crossings) algorithm.

- **Issue**: The algorithm does not explicitly handle cases with multiple interior voids (islands) correctly for Toposolids, which have more restrictive boundary rules than Floors in 2026.
- **Risk**: Precision issues with tessellated arcs in `CurveLoop` lead to incorrect "Inside" or "OnEdge" detection.

## Proposed Strategy

### Plan 15.1: Conversion & Coordinate Fixes
- Correct the Z-coordinate math in `ConversionService.cs`.
- Relative Z for Floor `AddPoint`: `absoluteZ - (levelElevation + heightOffset)`.
- Verify `Toposolid.Create` absolute/relative behavior via a test script.

### Plan 15.2: Fix Points Algorithm Overhaul
- Replace IDW with a "Planar Smoothing" approach.
- Identify the plane formed by the 3 nearest neighbors (triangle) and project the flagged point onto that plane.

### Plan 15.3: Split Boundaries Robustness
- Use `BooleanOperationsUtils` or more robust `CurveLoop` intersection checks where available.
- Ensure Toposolid void handling follows 2026 requirements (no overlapping boundaries).
