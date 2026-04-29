---
phase: 1
plan: 2
wave: 1
---

# Plan 1.2: Formula Logic Foundation

## Objective
Implement the core logic for safely updating parameter formulas during renaming operations.

## Context
- .gsd/SPEC.md
- .gsd/phases/1/RESEARCH.md
- src/Services/Renaming/BatchRenameExecutionService.cs

## Tasks

<task type="auto">
  <name>Implement FormulaUpdateService</name>
  <files>
    <file>src/Services/Renaming/FormulaUpdateService.cs</file>
    <file>src/Services/Renaming/IFormulaUpdateService.cs</file>
  </files>
  <action>
    - Create `IFormulaUpdateService` with a method `string UpdateFormula(string formula, string oldName, string newName)`.
    - Implement `FormulaUpdateService` using Regex `\b` (word boundaries) to ensure only exact matches are replaced.
    - Example: Renaming "A" should update "A + B" but NOT "A1 + B".
  </action>
  <verify>dotnet build</verify>
  <done>Formula update logic is implemented with Regex safety.</done>
</task>

<task type="auto">
  <name>Unit Tests for Formula Update Logic</name>
  <files>
    <file>LECG.Tests/Renaming/FormulaUpdateServiceTests.cs</file>
  </files>
  <action>
    - Create unit tests covering:
        - Simple replacement.
        - Multiple replacements in one formula.
        - Word boundary protection (partial matches).
        - Case sensitivity (Revit formulas are case-sensitive for parameters).
  </action>
  <verify>dotnet test --filter Category=Renaming</verify>
  <done>Formula update logic is verified with unit tests.</done>
</task>

## Success Criteria
- [ ] Formula replacement handles word boundaries correctly.
- [ ] All unit tests for formula renaming pass.
