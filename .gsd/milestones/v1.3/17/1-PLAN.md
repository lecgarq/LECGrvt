---
phase: 17
plan: 1
wave: 1
---

# PLAN 17.1: Category Changer Tool

Create the UI and command to switch family categories in-place.

## Tasks

### 1. View & ViewModel
<task>
- Create `CategoryChangerView.xaml` with a searchable `ListBox` or `ComboBox` for categories.
- Create `CategoryChangerViewModel.cs`.
- Populate categories using the utility from Phase 16.
</task>

### 2. Logic Integration
<task>
- Implement `ExecuteChangeCommand` in the ViewModel.
- Call `FamilyEditorService.ChangeCategory` for all selected element types.
- Add success/failure logging to the unified `LogView`.
</task>

### 3. Entry Point
<task>
- Create `CategoryChangerCommand.cs`.
- Add "Category Changer" button to the "Project Health" or a new "Family Support" panel in `RibbonFactory`.
</task>

## Verification
- UI loads without resizing issues.
- Category change is verified in Revit by checking the element's category after operation.
