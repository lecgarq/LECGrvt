---
phase: 6
plan: 1
wave: 1
---

# Plan 6.1: Backend Stability Reforms

## Objective
Refactor `BatchRenameExecutionService` to correctly handle transactions for Family Parameter renaming (which requires `EditFamily` and no active transaction). Also fix Object/Line Style collection logic.

## Context
- `BatchRenameExecutionService.cs` attempts to call `doc.EditFamily` inside a transaction.
- `BaseElementCollectionService.cs` uses `Enum.IsDefined` check which may incorrectly filter user styles or fail on logic.

## Tasks

<task type="auto">
  <name>Refactor Family Renaming Transaction Logic</name>
  <files>
    <file>src/Services/BatchRenameExecutionService.cs</file>
  </files>
  <action>
    1. Modify `ExecuteBatchRename` to separate items into two lists: `standardItems` and `familyItems`.
    2. Process `standardItems` inside the `using (Transaction t = ...)` block.
    3. Process `familyItems` **AFTER** the transaction block.
    4. For `familyItems`, iterate and perform `EditFamily` -> `Transaction` (on family doc) -> `LoadFamily` -> `Close`.
    5. Ensure strict error handling for family editing.
  </action>
  <verify>
    Code review verifying `EditFamily` is called outside the main `doc` transaction.
  </verify>
  <done>
    `ExecuteBatchRename` handles both types successfully without "Modifiable Document" exception.
  </done>
</task>

<task type="auto">
  <name>Fix Object/Line Style Collection</name>
  <files>
    <file>src/Services/BaseElementCollectionService.cs</file>
  </files>
  <action>
    1. In `CollectBaseElements`, update `objectStyles` and `lineStyles` logic.
    2. Replace `System.Enum.IsDefined` check with simple `cat.Id.Value < 0` check for Built-In Categories.
    3. Verify logic correctly adds user-created subcategories (Id > 0).
  </action>
  <verify>
    Code review.
  </verify>
  <done>
    Styles collection uses robust ID check.
  </done>
</task>

## Success Criteria
- [ ] No "Document is modifiable" error when renaming family parameters.
- [ ] User-created Object/Line styles appear in the list.
