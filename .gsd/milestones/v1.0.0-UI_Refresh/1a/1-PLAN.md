---
phase: "1a"
plan: "fix-standard-controls"
wave: 1
gap_closure: true
---

# Fix: Implement Standard UI Controls (Button, TextBox)

## Problem
The Milestone Audit found that while the palette and DataGrid foundation are established, the unified `LecgUI` library lacks standard controls like `LecgButton` and `LecgTextBox`, which are essential for a complete "Standardized UI".

## Root Cause
Phase 1 focused heavily on the foundation and the complex list logic, leaving basic component styling as technical debt.

## Tasks

<task type="auto">
  <name>Implement LecgButton Style</name>
  <files>src/Resources/Themes/LecgTheme.xaml</files>
  <action>
    - Create a global `<Style TargetType="Button">` (or a keyed style `LecgButton`) in `LecgTheme.xaml`.
    - Apply the earth-tone palette: `<Setter Property="Background" Value="{DynamicResource LecgControlBackground}"/>` and `<Setter Property="Foreground" Value="White"/>`.
    - Implement professional hover/pressed states using `<ControlTemplate.Triggers>` to change background color slightly (using `#4E4B44` for hover).
    - Ensure minimalism: Remove thick borders, use subtle corner radius (e.g., 2).
  </action>
  <verify>Get-Content "src/Resources/Themes/LecgTheme.xaml" | Select-String "TargetType=\"Button\""</verify>
  <done>Button styling is standardized and responsive to interaction states.</done>
</task>

<task type="auto">
  <name>Implement LecgTextBox Style</name>
  <files>src/Resources/Themes/LecgTheme.xaml</files>
  <action>
    - Create a global `<Style TargetType="TextBox">` in `LecgTheme.xaml`.
    - Set base colors: Background `#E6E3DA` (LecgSurfaceBackground), Border `#C8C0B4` (LecgBorderLight).
    - Implement a "Focus" trigger that changes the BorderBrush to `#708452` (LecgAccent) and increases BorderThickness slightly.
    - Set default padding (e.g., 5,2) for professional spacing.
  </action>
  <verify>Get-Content "src/Resources/Themes/LecgTheme.xaml" | Select-String "TargetType=\"TextBox\""</verify>
  <done>TextBox styling supports professional focus states and palette consistency.</done>
</task>

## Success Criteria
- [ ] Buttons and TextBoxes across the app instantly adopt the LECG identity when rendered.
- [ ] No glassmorphism or performance-heavy effects are used.
