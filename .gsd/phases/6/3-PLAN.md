---
phase: 6
plan: 3
wave: 1
---

# Plan 6.3: Include Formula Parameters in Batch Rename

## Objective
Enable renaming of Family Parameters that are driven by formulas (IsReadOnly = true).
Currently, these are skipped by `BaseElementCollectionService`.
The renaming logic in `BatchRenameExecutionService` handles them via `FamilyManager`, which allows renaming definitions regardless of formula status.

## Context
- src/Services/BaseElementCollectionService.cs
- src/Services/BatchRenameExecutionService.cs

## Tasks

<task type="auto">
  <name>Allow ReadOnly Family Parameters</name>
  <files>src/Services/BaseElementCollectionService.cs</files>
  <action>
    Modify `CollectBaseElements` method:
    - Remove `!p.IsReadOnly` check in the Family Parameter collection loop (line ~178).
    - Add a comment explaining that we allow ReadOnly because we are renaming definitions, not values, and `FamilyManager` supports this.
  </action>
  <verify>
    Check code to ensure `IsReadOnly` is no longer a filter condition for Family Parameters.
  </verify>
  <done>
    Parameters with formulas are now included in the collection list.
  </done>
</task>

## Success Criteria
- [ ] Family Parameters with formulas appear in the Batch Rename list.
- [ ] Renaming them works (handled by existing `BatchRenameExecutionService`).
