---
phase: 14
plan: 2
wave: 2
---

# PLAN 14.2: View & Feedback Transitions

Implement motion for window entry and element feedback.

## Tasks

### 1. View Entrance Animation
<task>
- Modify `LecgWindow.xaml` to include a `Window.Triggers` section.
- Add an `EventTrigger` for `Loaded` that animates:
    - `Opacity` from 0 to 1.
    - `RenderTransform` (TranslateY) from 20 to 0.
- Ensure this doesn't interfere with the DPI scaling logic from Phase 12.
</task>

### 2. Status & Badge Feedback
<task>
- Add a "Pill" or "Badge" pulse animation for success/warning states.
- Update `SelectionStatus` text in `SelectionControl` to fade in when the status changes.
</task>

### 3. Loading State Polish
<task>
- (Optional) Implement a "Shimmer" effect for cards that are waiting for Revit data if applicable.
- Standardize the `Progress` bar animation to be a smooth "infinite" sweep.
</task>

## Verification
- Build project.
- Verify that `base:LecgWindow` consumers (all views) now fade in smoothly.
