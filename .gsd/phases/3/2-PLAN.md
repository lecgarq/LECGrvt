---
phase: 3
plan: 2
wave: 2
---

# Plan 3.2: Hosting Validation & Integrity

## Objective
Ensure that conversion only occurs for valid hosting scenarios to maintain project data integrity.

## Context
- src/Services/FamilyConversionService.cs
- src/Services/Interfaces/IFamilyConversionLoggingService.cs

## Tasks

<task type="auto">
  <name>Implement Hosting Validation</name>
  <files>
    <file>src/Services/FamilyConversionService.cs</file>
  </files>
  <action>
    - Before starting conversion, check `instance.Host`.
    - If the instance is hosted (Host != null), log a warning that hosting might be lost depending on the target template.
    - If hosting is complex (e.g. hosted on a face), consider blocking if it violates "Zero constraint breakage" safety.
  </action>
  <verify>Check for .Host check in service.</verify>
  <done>Conversion service validates hosting status before execution.</done>
</task>

<task type="auto">
  <name>Update Logging for Integrity</name>
  <files>
    <file>src/Services/Interfaces/IFamilyConversionLoggingService.cs</file>
  </files>
  <action>
    - Add a method to log hosting-specific warnings.
    - Ensure these warnings appear in the Log Window during execution.
  </action>
  <verify>Check interface signature.</verify>
  <done>User is notified of potential integrity risks via the log.</done>
</task>

## Success Criteria
- [ ] Hosted elements trigger an informative log warning.
- [ ] No conversions allowed on scenarios known to break constraints (as per research).
- [ ] User visibility into hosting status during conversion.
