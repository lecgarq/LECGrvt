---
phase: 13
plan: 1
wave: 1
---

# SUMMARY 13.1: Core Dashboards & Components

Standardized core application views to the premium aesthetic.

## Changes:
- **HomeView**: Converted to `base:LecgWindow`, applied `Icons.Home`, and removed hardcoded startup location.
- **LogView**: Converted to `base:LecgWindow`, applied `Icons.Layers`, and updated the "Close" button to standard `SecondaryButtonStyle`.
- **SelectionControl**: Updated text styling to use `{DynamicResource TextMutedBrush}` for consistency with the design system.

## Verification:
- Build passes.
- XAML structure remains valid and follows the standard LecgWindow pattern.
