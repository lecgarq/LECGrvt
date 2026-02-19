---
phase: 2
plan: 1
wave: 1
---

# Plan 2.1: Purge Iteration Logic

## Objective
Update the core purge services to support configurable pass counts and ensure maximum effectiveness through document regeneration.

## Context
- .gsd/SPEC.md
- src/Services/Interfaces/IPurgePassSequenceService.cs
- src/Services/PurgePassSequenceService.cs
- src/Services/PurgeExecutionCoordinatorService.cs
- src/Services/Interfaces/IPurgeService.cs
- src/Services/PurgeService.cs

## Tasks

<task type="auto">
  <name>Update Pass Sequence Service</name>
  <files>
    <file>src/Services/Interfaces/IPurgePassSequenceService.cs</file>
    <file>src/Services/PurgePassSequenceService.cs</file>
  </files>
  <action>
    - Update `GetPasses` to accept `int passCount`.
    - Modify the loop in `PurgePassSequenceService` to use the provided `passCount`.
  </action>
  <verify>Check if passes loop correctly in code.</verify>
  <done>Interface and implementation accept passCount.</done>
</task>

<task type="auto">
  <name>Enhance Coordinator and Service</name>
  <files>
    <file>src/Services/PurgeExecutionCoordinatorService.cs</file>
    <file>src/Services/Interfaces/IPurgeService.cs</file>
    <file>src/Services/PurgeService.cs</file>
  </files>
  <action>
    - Update `Execute` in coordinator to accept `passCount`.
    - Add `doc.Regenerate()` at the end of each pass loop inside `PurgeExecutionCoordinatorService`.
    - Update `PurgeAll` signature in `IPurgeService` and `PurgeService` to include `int passCount`.
  </action>
  <verify>Verify method signatures match across service layers.</verify>
  <done>Service layer supports configurable passes and document regeneration.</done>
</task>

## Success Criteria
- [ ] Service layer correctly propagates `passCount`.
- [ ] Document is regenerated between passes to catch deep dependencies.
- [ ] Code compiles without errors.
