---
phase: 5
plan: 2
wave: 2
description: "Update UI for Advanced Search and Parameters"
---

# Phase 5 Plan 2: Frontend & Integration

## Goal
Update the `SearchReplaceView.xaml` to surface the new capabilities: Parameter Scope, Filter dropdowns, and "Replace Spaces" input helper.

## Tasks

<task>
<name>Update Search/Replace UI</name>
<files>
<file>src/Views/SearchReplaceView.xaml</file>
<file>src/ViewModels/SearchReplaceViewModel.cs</file>
</files>
<action>
1. Add new Scope CheckBox: "Family Parameters".
2. Add new Dropdown (ComboBox) for `SearchFilterType`: "Contains", "Begins With", "Ends With", "Doesn't Contain".
3. Add a "Replace Spaces" button next to "Replace With" text box (or a toggle) to quickly sanitize input.
4. Ensure bindings to new ViewModel properties (Phase 5 Plan 1).
</action>
<verify>
UI controls appear and interactions update ViewModel state.
</verify>
</task>

<task>
<name>Integrate Parameter Renaming Logic</name>
<files>
<file>src/Services/Interfaces/IBatchRenameExecutionService.cs</file>
<file>src/Services/BatchRenameExecutionService.cs</file>
</files>
<action>
1. Update `BatchRenameExecutionService` to handle `ElementData.Type == "FamilyParameter"`.
2. Renaming logic for Parameters:
   - Identify the Family from the `ElementId` (Wait, elementId for parameter is not straightforward).
   - `ElementData` for parameters needs {FamilyId, ParameterName}. `ElementData` has a `Id` field that is expected to be a `Value` for an ElementId.
   - Family Parameters are *inside* a `Family` element but are not elements themselves (unless shared params, but user said "only family").
   - We likely need a special handling: The `ElementId` in `ElementData` will be the **Family** ID. The `OriginalValue` is the parameter name. The `Name` in data is the parameter name.
   - When executing rename:
     - Get Family from `Id`.
     - Open Family Document (Must be careful here! Editing families requires opening them).
     - Or just edit the loaded family parameters if possible? No, usually need to edit family definition.
     - **Constraint**: Editing loaded families parameters often requires reloading. 
     - **New Strategy**: We might need to iterate families in the project. Editing `FamilyParameter` usually means modifying the family document. This is heavy.
     - **Alternative**: Maybe user meant "Parameters of Project Families"? 
     - "family parametsr ... that you collect from every family" implies opening family docs or editing loaded content.
     - IF editing loaded family: `doc.EditFamily(family)` -> transaction -> rename param -> load back. This is slow and heavy.
     - IF editing project parameters: `doc.ParameterBindings` ... but user said "not project parameters".
     - **Assumption**: We will edit the Family Document for each selected family. This is an advanced operation.
     - **Simpler path**: Check if we can rename parameters of a loaded family directly without opening? Generally no, unless it's a Shared Parameter (but user excluded shared).
     - **Plan**: `BatchRenameExecutionService` will:
       - Group items by Family ID.
       - For each family: `EditFamily` -> Rename Param -> `LoadFamily` (overwrite).
       - This is risky and slow. We should warn user.
</action>
<verify>
Verify parameter renaming persists after family reload.
</verify>
</task>
