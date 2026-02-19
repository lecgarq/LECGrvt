---
phase: 6
plan: 2
wave: 1
---

# Plan 6.2: UI Refinements

## Objective
Update the "Replace Spaces" functionality visualization.

## Context
- `SearchReplaceView.xaml` has a button `_` on the `FilterName`.
- User requests "Remove Spaces" (sanitize utility).

## Tasks

<task type="auto">
  <name>Improve Replace Spaces UI</name>
  <files>
    <file>src/Views/SearchReplaceView.xaml</file>
  </files>
  <action>
    1. Update the `FilterName` (Search) input button to be clearer (or remove if confusing).
    2. Add a **"Replace Spaces"** button (or toggle) to the "Replace With" (Rename) input in the bottom panel.
    3. Ensure tooltip clearly states "Replace Spaces with Underscore".
  </action>
  <verify>
    UI Check.
  </verify>
  <done>
    Button is visible and functional for sanitizing inputs.
  </done>
</task>

## Success Criteria
- [ ] Explicit "Replace Space" utility clearly visible.
