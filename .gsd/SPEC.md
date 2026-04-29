# SPEC.md — Batch Rename Plugin Optimization

> **Status**: `FINALIZED`

## Vision
Transform the Batch Rename plugin into a robust, high-performance tool that handles all renameable element types and parameters without intermittent failures, blank fields, or unexplained skips.

## Goals
1. **Eliminate "Stubborn" Parameter Skips:** Refactor `GetRenameSkipReason` to allow renaming of associated parameters when safe (e.g., updating formula references).
2. **Fix "Blank Fields" & Incomplete Data:** Ensure `BaseElementCollectionService` captures all valid elements even if they lack a category, and improve metadata display.
3. **Enhance Error Reporting:** Instead of silently skipping, provide clear UI feedback on why a parameter cannot be renamed.
4. **Refactor Technical Debt:** Consolidate renaming logic and ensure full test coverage for the `Renaming` namespace.

## Non-Goals
- Adding new search/replace algorithms (Regex/Replace is sufficient for now).
- Changing the UI theme (Focus is on logic and stability).

## Users
BIM Managers and Revit Power Users who need to perform bulk renaming across complex models.

## Constraints
- **Revit 2026 API:** Must use the latest API patterns for Toposolids and other new elements.
- **Performance:** Must handle families with hundreds of parameters without hanging the UI.
- **Safety:** Must not break family integrity (formulas, associations) unless explicitly forced by user.

## Success Criteria
- [ ] 0% unexplained "skips" in batch rename logs.
- [ ] All elements in the grid have valid name and category labels.
- [ ] Unit tests covering 100% of renaming service logic.
