# RESEARCH 14: Interaction & Animation Polish

## Objective
Implement high-end micro-animations that complement the "Premium Liquid Glass" aesthetic without degrading Revit's performance or obstructing workflow.

## 1. Animation Principles (Liquid Glass)
- **Softness**: Avoid linear transitions. Use `QuarticEase` or `CubicEase` for state changes.
- **Glass Refraction**: Hover effects shouldn't just change color; they should "light up" the border or background opacity.
- **Responsiveness**: Animations must be fast (<250ms) to feel productive.
- **Subtlety**: Micro-animations are noticed when they are missing, not when they are present.

## 2. Technical Implementation (WPF)

### Button Hover Transitions
Instead of standard Triggers, use `VisualStateManager` or `EventTrigger` with `ColorAnimation` and `DoubleAnimation`.

**Target Properties:**
- `Background.Color` (if solid)
- `BorderBrush.Color`
- `Opacity` (for the "shimmer" effect)
- `Effect.BlurRadius` (for glow)

### Window Entrance
Standardize a "Slide + Fade" or "Scale + Fade" for view initialization in `LecgWindow.xaml`.

### Interaction Polish
- **Pill Badges**: Pulse animation on status change.
- **DataGrid Rows**: Subtle highlight transition.
- **Input Fields**: Border glow on focus.

## 3. Risks
- **DPI Scaling**: Animations involving manual pixel values must be checked against Phase 12 stability.
- **Software Rendering**: Some Revit environments may disable hardware acceleration. Use `Timeline.DesiredFrameRate` if necessary.
