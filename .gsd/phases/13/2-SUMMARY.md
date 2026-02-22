---
phase: 13
plan: 2
wave: 1
---

# SUMMARY 13.2: Geometry & Alignment Batch

Standardized alignment and level-related views.

## Changes:
- **Batch Chrome Cleanup**: Removed `WindowStartupLocation="CenterScreen"` and `SizeToContent="Height"` from 6 views to allow the base engine to handle placement and resizing.
- **Icon Synchronization**:
    - `AlignDashboardView`: `AlignCenter`
    - `AlignEdgesView`: `Contours`
    - `AlignElementsView`: `AlignCenter`
    - `ChangeLevelView`: `Level`
    - `OffsetElevationsView`: `ArrowUpDown`
    - `ResetSlabsView`: `ResetSlabs`
- **Namespace Hygiene**: Ensured `xmlns:icons` is correctly defined for all icon references.

## Verification:
- Build passes.
- Views correctly inherit the new premium title bar and responsive behavior.
