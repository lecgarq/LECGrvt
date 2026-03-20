---
phase: 15
plan: 2
wave: 1
---

# Plan 15.2: Fix Points and Split Boundaries Stabilization

## Objective
Overhaul the "Fix Points" repair logic and improve the robustness of boundary splitting for Floors and Toposolids.

## Context
- .gsd/SPEC.md
- src/Services/FixPointsService.cs
- src/Services/SplitBoundariesService.cs
- src/Services/Interfaces/IGeometryBoundaryService.cs

## Tasks

<task type="auto">
  <name>Overhaul Fix Points IDW to Planar Smoothing</name>
  <files>src/Services/FixPointsService.cs</files>
  <action>
    - Replace the `ComputeWeightedAverageZ` (IDW) in `src/Services/FixPointsService.cs`.
    - Implement a "Point-on-Plane" approach:
      1. Find the **3 nearest neighbors** for a flagged point (forming a candidate triangle).
      2. Construct the Plane containing these 3 neighbors.
      3. Project the flagged point onto this plane to fix its Z.
    - If non-planar geometry (curved transitions) is detected (e.g., neighbor distance > threshold), fallback to a more local smoothing.
    - This ensures repaired points sit exactly on the triangles Revit uses for rendering.
  </action>
  <verify>dotnet build</verify>
  <done>Z-adjustment logic follows planar projection instead of weighted average smoothing.</done>
</task>

<task type="auto">
  <name>Stabilize Split Boundaries Tolerance & Winding</name>
  <files>src/Services/SplitBoundariesService.cs</files>
  <action>
    - Refine `IsPointInsideLoop` in `src/Services/SplitBoundariesService.cs`.
    - Improve tolerance handling for points exactly on edges (`IsPointOnLoop`).
    - Handle complex island/void scenarios for Toposolids by ensuring `Toposolid.Create` receives loops with normalized winding orders (outer CCW, void CW).
  </action>
  <verify>dotnet build</verify>
  <done>Split Boundaries correctly handles island containment and edge point precision.</done>
</task>

<task type="auto">
  <name>Verify Toposolid Sub-element Adjustment</name>
  <files>src/Services/SplitBoundariesService.cs</files>
  <action>
    - Refine `TryAdjustBoundaryVertex` behavior.
    - Ensure `ModifySubElement` is called with correctly calculated `deltaZ` (absolute vs relative).
  </action>
  <verify>dotnet build</verify>
  <done>Sub-element adjustments for split fragments are consistently applied.</done>
</task>

## Success Criteria
- [ ] Fix Points no longer creates triangulation spikes (planar projection used).
- [ ] Split Boundaries correctly fragments multi-boundary elements without losing interior points.
- [ ] Project builds without errors.
