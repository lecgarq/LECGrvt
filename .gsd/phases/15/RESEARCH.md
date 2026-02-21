# RESEARCH 15: Verification & Stress Testing

## Objective
Validate the stability of the new "Premium Liquid Glass" design system across varying DPI settings, screen resolutions, and complex view interactions.

## 1. Stress Test Scenarios
- **DPI Simulation**: Validate margins and corner radii at 100%, 150%, and 200% scale (simulated via XAML analysis).
- **Responsive Collapse**: Shrink windows to `MinWidth`/`MinHeight` to ensure no overlapping of controls.
- **Large Dataset Scroll**: Test `DataGrid` virtualization and scrollbar styling in `ConvertCadView` and `BatchRenameView`.
*   **Animation Overload**: Rapidly hover/unhover multiple buttons to ensure no storyboard leaks or jitter.

## 2. Technical Checks
- **Resource Dictionary Leak**: Ensure all `DynamicResource` references resolve correctly without fallback bottlenecks.
- **Parent Centering**: Verify `LecgWindow.cs` centering logic doesn't crash if the Revit handle is null.

## 3. Success Criteria
- [ ] No visual artifacts on resize.
- [ ] DPI-aware sizing in `LecgWindow` verified.
- [ ] All views build and load without resource errors.
