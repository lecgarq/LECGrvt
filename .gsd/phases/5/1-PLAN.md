---
phase: 5
plan: 1
wave: 1
description: "Implement Family Parameter renaming logic and advanced search filtering."
---

# Phase 5 Plan 1: Search & Rename Backend

## Goal
Implement the core logic for collecting Family Parameters (filtering out system/shared) and advanced string filtering for the search/replace utility.

## Tasks

<task>
<name>Update Collection Service for Family Parameters</name>
<files>
<file>src/Services/Interfaces/IBaseElementCollectionService.cs</file>
<file>src/Services/BaseElementCollectionService.cs</file>
</files>
<action>
1. Modify `IBaseElementCollectionService.CollectBaseElements` to accept a `familyParameters` boolean.
2. In `BaseElementCollectionService`, implement logic to scan loaded families.
3. For each family, iterate `FamilyManager.Parameters`.
4. Filter out `IsShared`, `IsReadOnly`, and built-in parameters.
5. Collect valid user-created parameters into `ElementData` list with `Type = "FamilyParameter"`.
</action>
<verify>
Unit test or debug log confirming only user-created parameters are collected.
</verify>
</task>

<task>
<name>Implement Advanced Search Filters</name>
<files>
<file>src/Services/SearchReplacePreviewService.cs</file>
<file>src/ViewModels/SearchReplaceViewModel.cs</file>
</files>
<action>
1. Update `SearchReplaceViewModel` to include a `FilterType` enum property (Contains, BeginsWith, EndsWith, DoesNotContain).
2. Update `SearchReplacePreviewService.ProcessPreview` to use this new filter logic when matching `OriginalValue`.
3. Ensure existing "Match Case" logic works with new filters.
</action>
<verify>
Unit test filter logic with various string combinations.
</verify>
</task>

<task>
<name>Implement "Replace Spaces" Utility</name>
<files>
<file>src/Services/RenameRulePipelineService.cs</file> 
<file>src/Services/Interfaces/IRenameRulePipelineService.cs</file>
</files>
<action>
1. Add a `ReplaceSpaces` boolean option to `RenameRule` or a separate method.
2. In `RenameRulePipelineService`, if `ReplaceSpaces` is true, replace all ` ` with `_` (or user defined char) in the target string *before* or *after* other replacements.
3. Need to decide if this is a separate action or part of the `Replace` logic. The user said "replace spaces for an input", implying a utility on the input field itself, or a rule. 
4. Let's implement it as a new `RuleType` or a modifier in the ViewModel first, but backend support in pipeline is good.
</action>
<verify>
Verify strings with spaces are correctly modified.
</verify>
</task>
