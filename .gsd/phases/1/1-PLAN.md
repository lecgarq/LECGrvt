---
phase: 1
plan: 1
wave: 1
---

# Plan 1.1: Service Consolidation & RenameRule Enhancement

## Objective
Simplify the renaming service architecture to reduce technical debt and prepare for advanced renaming logic.

## Context
- .gsd/SPEC.md
- .gsd/ARCHITECTURE.md
- src/Services/Renaming/BatchRenameExecutionService.cs
- src/Services/Renaming/RenameRules.cs

## Tasks

<task type="auto">
  <name>Consolidate Renaming Service Logic</name>
  <files>
    <file>src/Services/Renaming/BatchRenameExecutionService.cs</file>
  </files>
  <action>
    - Review `BatchRenameExecutionService.cs` for redundant logic or "legacy" fragments.
    - Ensure all helper methods are private and correctly scoped.
    - Improve logging consistency across the service.
  </action>
  <verify>dotnet build</verify>
  <done>Code is cleaner, builds successfully, and logging is improved.</done>
</task>

<task type="auto">
  <name>Enhance RenameRules Validation</name>
  <files>
    <file>src/Services/Renaming/RenameRules.cs</file>
  </files>
  <action>
    - Add basic validation to `ReplaceRule`, `RemoveRule`, and `AddRule` (e.g., prevent empty find strings in ReplaceRule).
    - Ensure consistency in how rules are applied via the `IRenameRule` interface.
  </action>
  <verify>dotnet build</verify>
  <done>Renaming rules have basic input validation.</done>
</task>

## Success Criteria
- [ ] Renaming services build without errors.
- [ ] Code structure is more maintainable.
