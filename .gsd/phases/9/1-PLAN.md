---
phase: 9
plan: 1
wave: 1
---

# Plan 9.1: Scope Exclusivity & Instance Parameter Collection

## Objective
Two critical fixes:
1. Make scopes mutually exclusive (radio behavior) — selecting one scope deactivates all others
2. Fix instance parameter collection — also scan FamilyInstance elements to capture instance-only parameters

## Context
- src/ViewModels/SearchReplaceViewModel.cs — Scope properties & change handlers
- src/Services/BaseElementCollectionService.cs — Parameter collection logic
- src/Views/SearchReplaceView.xaml — UI pill badges (already styled, just need radio behavior)

## Tasks

<task type="auto">
  <name>Make scopes mutually exclusive (radio behavior)</name>
  <files>src/ViewModels/SearchReplaceViewModel.cs, src/Views/SearchReplaceView.xaml</files>
  <action>
    In SearchReplaceViewModel.cs:
    1. Add a private bool `_isSettingScope` guard flag to prevent recursive calls
    2. Change each `OnScope*Changed` handler to:
       - If guard is active, return early
       - If value is `true`, set guard, turn OFF all other scopes, clear guard, then call RefreshScope()
       - If value is `false` and NO other scope is active, re-set this one to true (at least one must be active)
    
    This gives radio-group behavior: clicking a pill activates it and deactivates all others.
    
    In the XAML, the pills are already CheckBoxes with PillCheckboxStyle — no XAML changes needed for this behavior since it's driven by the ViewModel.
  </action>
  <verify>dotnet build LECG.csproj -c Release</verify>
  <done>Build succeeds; selecting one scope pill in the UI deactivates all others</done>
</task>

<task type="auto">
  <name>Fix instance parameter collection</name>
  <files>src/Services/BaseElementCollectionService.cs</files>
  <action>
    The current code only scans FamilySymbol elements (type elements). FamilySymbol.Parameters
    does contain both instance and type parameters for the FAMILY, but the ParameterBindings
    lookup is unreliable for family-specific custom parameters.
    
    Fix: After scanning all FamilySymbols, also scan ONE FamilyInstance per family:
    1. After the symbolsByFamily loop, create a new collector for FamilyInstance elements
    2. Group by Family.Id (via fs.Symbol.Family.Id)
    3. For each family, take the FIRST instance, scan its Parameters
    4. Apply same filter (not shared, not built-in)
    5. Use seenParamNames from the SAME family to deduplicate against type params already found
    6. Mark IsInstance = true for params found only on instances
    
    This ensures instance-only parameters (that don't appear on the FamilySymbol) are collected.
  </action>
  <verify>dotnet build LECG.csproj -c Release</verify>
  <done>Build succeeds; instance-only parameters appear in the preview when Parameters scope is selected</done>
</task>

## Success Criteria
- [ ] Build succeeds with zero errors
- [ ] Clicking a scope pill deactivates all others (radio behavior)
- [ ] Instance-only family parameters are collected and shown in preview
