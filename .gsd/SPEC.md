# SPEC.md — Project Specification

> Status: FINALIZED

## Vision
Enhance the "Render Appearance Match" tool to provide intelligent material standardization. It will automatically detect and fix non-compliant materials, correctly map Normal Map properties, and allow user-controlled UV scaling via a new UI dropdown.

## Goals
1. **Intelligent Skip/Update**: Automatically identify and skip materials that already meet the 4-point standard.
2. **4-Point Standardization**:
    - "Use Render Appearance" enabled for Graphics Shading.
    - Graphics Shading color precisely matched to Render Appearance average color.
    - Surface patterns (Foreground & Background) set to Solid Fill.
    - Cut patterns (Foreground & Background) set to Solid Fill.
3. **Normal Map Correction**: Fix the Revit API property mapping for Bump assets to ensure they are explicitly defined as "Normal Maps".
4. **Custom UV Scaling**: Provide a dropdown in the UI (0.5m, 1m, 2m, 5m) to define the texture scale during synchronization.

## Success Criteria
- [ ] Log output identifies only modified (non-compliant) materials.
- [ ] Materials updated by the tool have "Normal Map" successfully checked in the Revit Appearance Editor.
- [ ] Materials updated reflect the UV scale chosen in the UI.
- [ ] Shading color, surface/cut patterns are consistent across all "Matched" materials.

## Users
BIM Managers and Architects who need to standardize project material graphics for consistent documentation and visualization without manual property auditing.

## Constraints
- **Target**: Revit 2026 / .NET 8.0.
- **Architecture**: Must integrate into the existing `RenderAppearanceMatchCommand` and its associated Service/VM.
- **Performance**: Must remain efficient for projects with thousands of materials.

## Non-Goals
- Modifying custom textures (beyond scale and property assignment).
- Creating new materials from scratch.
