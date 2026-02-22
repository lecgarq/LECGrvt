---
phase: 16
plan: 1
wave: 1
---

# PLAN 16.1: Silent Family Editor Service

Implement the core engine for background family modification.

## Tasks

### 1. Silent Options & Interfaces
<task>
- Create `IFamilyEditorService.cs` in `LECG.Services`.
- Implement `SelectionLoadOptions.cs` (IFamilyLoadOptions) to handle overwrite prompts silently.
</task>

### 2. Family Editor Implementation
<task>
- Implement `FamilyEditorService.cs`.
- Method `ChangeCategory(Family family, Category newCategory)`: handles the background edit/load cycle.
- Method `BatchProcess(IEnumerable<Family> families, Action<Document> modificationAction)`: allows bulk modifications with high performance.
</task>

### 3. Category Discovery Utility
<task>
- Create a utility in `RevitUtils` to fetch "Valid Family Categories".
- Filter: `c.IsFamilyCategory == true` and `c.CategoryType == CategoryType.Model`.
</task>

## Verification
- Build success.
- Unit test (or dry run) verification that `Family.EditFamily` can be called without UI interruption.
