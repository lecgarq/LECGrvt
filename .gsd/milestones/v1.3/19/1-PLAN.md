---
phase: 19
plan: 1
wave: 1
---

# Plan 19.1: Performance Monitoring & Transaction Optimization

## Objective
Implement performance tracking to identify slow operations and optimize transaction grouping in the conversion engine.

## Context
- .gsd/SPEC.md
- src/Services/FamilyConversionService.cs
- src/Services/Logging/Logger.cs

## Tasks

<task type="auto">
  <name>Implement ExecutionTimer Utility</name>
  <files>
    <file>src/Utils/ExecutionTimer.cs</file>
  </files>
  <action>
    Create a `IDisposable` utility `ExecutionTimer` that:
    1. Starts a `Stopwatch` on construction.
    2. Logs the elapsed time to `Logger.Instance` on disposal with a custom operation name.
    - Used like: `using (new ExecutionTimer("MyTask")) { ... }`
  </action>
  <verify>Check that the file exists and uses Stopwatch.</verify>
  <done>ExecutionTimer utility implemented.</done>
</task>

<task type="auto">
  <name>Optimize Transaction Grouping in Batch Conversion</name>
  <files>
    <file>src/Services/FamilyConversionService.cs</file>
  </files>
  <action>
    Review `ConvertFamilyBatch` and ensure:
    1. Transaction grouping minimizes the number of Start/Commit calls per instance.
    2. If `ReplaceInPlace` is true, all placement and parameter applications for a specific family group happen in a single SubTransaction or optimized block.
    3. Add `ExecutionTimer` blocks to key stages (Family Conversion, Instance Placement).
  </action>
  <verify>Build project and check logs for timing entries during operation.</verify>
  <done>Conversion service is instrumented and transaction-optimized.</done>
</task>

## Success Criteria
- [ ] Every major batch operation logs its execution time.
- [ ] Transaction overheard is minimized.
