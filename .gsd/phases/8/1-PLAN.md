---
phase: 8
plan: 1
wave: 1
---

# Plan 8.1: Fix Parameter Collection

## Objective
Fix the bug where not all family parameters are being listed. The current code iterates only ONE FamilySymbol per family and uses `processedFamilies` to skip subsequent types. Parameters that exist on other FamilySymbols of the same family are missed. This plan fixes the collection logic to iterate ALL FamilySymbols per family and deduplicate parameters by name.

## Context
- src/Services/BaseElementCollectionService.cs
- src/Services/SearchReplaceService.cs (ElementData class)

## Tasks

<task type="auto">
  <name>Fix parameter collection to iterate all FamilySymbols and deduplicate</name>
  <files>src/Services/BaseElementCollectionService.cs</files>
  <action>
    In the `CollectBaseElements` method, inside the `if (familyParameters)` block:
    
    1. REMOVE the `processedFamilies` HashSet guard for the parameter loop. Instead, collect parameters from EVERY FamilySymbol.
    2. GROUP FamilySymbols by `fs.Family.Id` so we process each family once but collect params from ALL its symbols.
    3. For each family group, iterate ALL symbols and collect ALL unique parameters (deduplicate by `p.Definition.Name`).
    4. Use a `HashSet<string>` per family to track already-seen parameter names, avoiding duplicate entries.
    5. Keep the existing filters (no shared, no built-in) and the advanced property population (ParamGroup, IsInstance, IsReadOnly).
    
    The key insight: different FamilySymbols of the same family CAN expose different parameters (e.g., type parameters that vary by symbol). We want ALL unique params across ALL symbols.
    
    - Do NOT change the ElementData class
    - Do NOT change the filter logic in SearchReplacePreviewService
  </action>
  <verify>dotnet build LECG.csproj -c Release 2>&1 | Select-String "error CS|Build succeeded"</verify>
  <done>Build succeeds with 0 errors. Parameter collection iterates all FamilySymbols per family and deduplicates by name.</done>
</task>

## Success Criteria
- [ ] Build succeeds with 0 errors
- [ ] All FamilySymbols are iterated per family (no early exit via processedFamilies for parameter names)
- [ ] Parameters are deduplicated by Definition.Name per family
