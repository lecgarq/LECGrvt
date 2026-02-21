---
phase: 11
id: 11.1
wave: 1
---

# PLAN 11.1: UI Baseline & Design System Audit

Objective: Consolidate ad-hoc styles and establish a unified baseline for all Revit command views.

## Task 1: Audit & Styles Consolidation
- Read `src/Resources/Styles.xaml` to identify existing design tokens.
- Identify ad-hoc brushes and styles in `SearchReplaceView.xaml` (the current premium target).
- Extract "Premium Liquid Glass" tokens into `Styles.xaml` for global use.

## Task 2: Standardize Window Chrome
- Audit `LecgWindow.xaml` and its code-behind.
- Ensure the header region is reusable and consistent.

## Verification
- Run build to ensure `Styles.xaml` remains valid.
- Manually check `Styles.xaml` contents for unified color tokens.
