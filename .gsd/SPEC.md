# SPEC.md — Project Specification

> **Status**: `FINALIZED`

## Vision

Enhance the LECG Revit toolkit with robust administrative and conversion features, focusing on systematic renaming of core Revit elements, deeper purging capabilities, and safer family conversion workflows.

## Goals

1. **Extended Search & Replace**: Implement renaming logic for Materials, Object Styles, Line Styles, and Fill Patterns.
2. **Triple Purge**: Add functionality to execute the "Purge Unused" logic three consecutive times to clear nested dependencies, mirroring Revit's native "Purge" behavior.
3. **Restricted Family Selection**: Update the "Convert Family" tool to filter out work plane-based families, allowing only element-based/host-based families for selection.
4. **Constraint Safety**: Ensure all renaming and conversion operations maintain existing element IDs and parameters to avoid breaking model constraints or hosted elements.
5. **Advanced Parameter Renaming**: Implement renaming for Family Parameters across all families, filtering out shared/project/built-in parameters.
6. **Enhanced Search Capabilities**: Add advanced text filters (Contains, Begins/Ends With) and utility functions (Replace Spaces).
17. **Full UI Consistency & Stability**: Standardize all command interfaces to the "Premium Liquid Glass" design system and fix window resizing/DPI scaling bugs.

## Non-Goals (Out of Scope)

- Global renaming of Revit Categories or Subcategories (beyond Custom Object Styles).
- Modification of built-in/hardcoded Revit Line Styles or Fill Patterns that cannot be renamed via API.
- Automated merging of materials with identical names during rename.

## Users

BIM Managers and Power Users who need to clean, standardize, and migrate project data without manually repeating operations or selecting invalid family types.

## Constraints

- **Revit API**: Must comply with Revit 2026 API limitations regarding renaming system elements.
- **Performance**: Triple purge must be optimized to avoid long UI hangs during large model cleanups.
- **Architecture**: Must follow the existing MVVM + Service Layer patterns documented in `ARCHITECTURE.md`.

## Success Criteria

- [ ] Search & Replace successfully renames Materials, Object Styles, Line Styles, and Fill Patterns without losing data.
- [ ] Purge Unused can be run 3 times in a single operation, resulting in 0 or fewer purgable items than a single pass.
- [ ] Convert Family selection filter prevents selecting work plane-based families via `ISelectionFilter`.
- [ ] No hosted elements (e.g., dimensions, tags) are lost during renaming/conversion.
- [ ] 100% of command views follow the Premium Design System with zero layout collapse on resize.
