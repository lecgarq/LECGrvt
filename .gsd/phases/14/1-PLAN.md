---
phase: 14
plan: 1
wave: 1
---

# PLAN 14.1: Component Transitions

Enhance core styles in `Styles.xaml` (specifically `Buttons.xaml` and `Controls.xaml`) with smooth transitions.

## Tasks

### 1. Unified Animation Resources
<task>
- Create `src/Resources/Animations.xaml` for common `Storyboard` templates and `EasingFunctions`.
- Define `StandardEase` (QuarticOut) and `InteractionDuration` (0:0:0.2).
- Wire into `Styles.xaml`.
</task>

### 2. Primary & Accent Button Polish
<task>
- Refactor `PrimaryButtonStyle` to use `EventTrigger` for MouseEnter/Leave.
- Animate `Background` color transition.
- For `AccentButtonStyle`, animate the `DropShadowEffect.Opacity` and `BlurRadius` to simulate a "glow" when hovered.
</task>

### 3. Secondary & Ghost Button Polish
<task>
- Implement subtle border color fading for `SecondaryButtonStyle`.
- Add a tiny "lift" effect using `TranslateTransform` (1px up) if it doesn't break alignment.
</task>

### 4. Input & Control Focus Glow
<task>
- Update `ModernTextBoxStyle` in `Controls.xaml` to include an outer glow or border color transition on `IsFocused`.
- Apply similar logic to `ComboBox` and `CheckBox`.
</task>

## Verification
- Run `dotnet build`.
- Manual visual check (if possible in sandbox): Buttons should transition colors smoothly over 200ms instead of snapping.
