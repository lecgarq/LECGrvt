---
phase: 4
plan: 1
wave: 1
---

# Plan 4.1: Logic Tests & Verification

## Objective
Empirically verify the logic of new features via unit tests.

## Context
- src/Services/FamilyConversionNamingService.cs
- src/Services/PurgePassSequenceService.cs
- src/Utils/FamilyInstanceFilter.cs
- LECG.Tests/

## Tasks

<task type="auto">
  <name>Test Family Conversion Naming</name>
  <files>
    <file>LECG.Tests/Services/FamilyConversionNamingServiceTests.cs</file>
  </files>
  <action>
    - Create a new test file for `FamilyConversionNamingService`.
    - Note: Since it uses `FilteredElementCollector`, I may need to mock the document or test the string resolution logic if decoupled.
    - If document is hard to mock, focus on the suffix incrementation logic (e.g. mock the collection of existing names).
  </action>
  <verify>Run dotnet test.</verify>
  <done>Suffix logic is verified to handle collisions.</done>
</task>

<task type="auto">
  <name>Test Purge Pass Sequence</name>
  <files>
    <file>LECG.Tests/Services/PurgePassSequenceServiceTests.cs</file>
  </files>
  <action>
    - Create tests for `PurgePassSequenceService`.
    - Verify that `GetPasses(3)` returns `[1, 2, 3]`.
    - Verify that `GetPasses(1)` returns `[1]`.
  </action>
  <verify>Run dotnet test.</verify>
  <done>Purge sequence logic is verified.</done>
</task>

<task type="auto">
  <name>Test Selection Filter Logic</name>
  <files>
    <file>LECG.Tests/Utils/FamilyInstanceFilterTests.cs</file>
  </files>
  <action>
    - Create tests for `FamilyInstanceFilter`.
    - Note: Requires mocking `FamilyInstance` and `Family`.
    - Verify `AllowElement` returns `false` for work-plane based families.
  </action>
  <verify>Run dotnet test.</verify>
  <done>Filter logic is verified.</done>
</task>

## Success Criteria
- [ ] New unit tests cover naming collisions.
- [ ] New unit tests cover purge iterations.
- [ ] New unit tests cover selection safety.
- [ ] All tests pass in the sandbox environment.
