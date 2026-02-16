---
name: Interaction Standards
description: Rules for User Interaction, Command Logic, and UI Consistency.
---

# Interaction Standards

To ensure a consistent, premium user experience, all Revit commands must adhere to the following interaction rules.

## 1. No Direct Selection (Zero-Touch Entry)
Commands must **NEVER** start by asking the user to select elements directly in the model.
- **Forbidden**: Calling `uidoc.Selection.PickObject()` immediately inside `Execute()`.
- **Required**: The command must immediately open a UI configuration window (`LecgWindow`).

## 2. The Configuration Window Pattern
Every command follows this flow:
1. **Open UI**: User clicks ribbon button -> Window opens immediately.
2. **Configure**: User sees settings and a **Selection Section**.
3. **Select (Optional)**: If the user needs to select elements, they click a "Select" button in the UI. The window hides, selection happens, window reappears.
4. **Execute**: User clicks "Run"/"Apply". The window closes (or stays open for batch), and logic executes.

## 3. Modular Selection Component
Every UI that requires element selection must use the standard **Selection Component** (Conceptually).

### UI Layout
The selection section must appear at the top of the window (or logical position) and contain:
- **Status Indicator**: "No items selected" / "5 Walls selected".
- **Action Button**: A button labeled "Select..." or similar (`SelectCommand` in logic).
- **Filter Description**: (Optional) Text describing what filters are active (e.g., "Filtering by: Walls").

### Behavior
- Initial state: "0 Selected" (unless `ActiveSelection` was passed on load).
- On Click "Select":
  - Hide Window.
  - Prompt user in Revit Status Bar.
  - Allow Escape to cancel (restore window).
  - On Finish: Restore window, update Status Indicator.

## 4. Visual Consistency
- All windows use `base:LecgWindow`.
- The "Run" button is always `PrimaryButtonStyle`.
- The "Cancel"/"Close" button is `SecondaryButtonStyle`.
- Selection buttons use `SecondaryButtonStyle` (usually) or a specialized "Icon Button" style.

## 5. Feedback Loop
- **Short Ops**: Show `TaskDialog` or Toast at end.
- **Long Ops**: Open `LogView` / `LogViewModel` immediately and show progress.
