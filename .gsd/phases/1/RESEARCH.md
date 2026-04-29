# Phase 1 Research — Safe Renaming Logic

## Objectives
1. Determine how to safely rename parameters referenced in formulas.
2. Determine how to handle shared parameters which `RenameParameter` doesn't support.
3. Validate dimension label behavior.

## Findings

### 1. Formula Safety
- **Issue:** Revit does NOT auto-update formulas when a parameter name changes.
- **Solution:** 
    - Before renaming, scan all parameters for formulas.
    - After renaming, use Regex `\bOLD_NAME\b` to replace the name in all stored formulas.
    - Re-apply formulas using `FamilyManager.SetFormula()`.
    - **Note:** Must handle the order of updates if multiple renamed parameters refer to each other.

### 2. Dimension Labels
- **Finding:** Dimension labels are linked by internal ID. `RenameParameter` automatically updates labels. No manual action needed.

### 3. Shared Parameters
- **Issue:** `RenameParameter` throws an exception for shared parameters.
- **Solution:** 
    - Shared parameters in families should generally NOT be renamed because their identity is based on GUID, not name.
    - However, if renaming is required, the standard pattern is:
        1. Add new parameter with correct name.
        2. Copy value/formula from old to new.
        3. Delete old parameter.
    - **Risk:** This breaks external references in the project.
    - **Decision:** For now, we will mark Shared Parameters as "Read Only" or "Manual Replace Required" in the UI to avoid data loss.

### 4. Implementation Plan for Core Renaming
- Create a `RenamingCoordinator` that:
    - Collects all formulas.
    - Performs the renames.
    - Updates the formulas with regex replacement.
    - Validates with `IsValidFormula` before applying.

## Roadmap Implications
- **Phase 3** will need a robust Regex-based formula updater.
- **Phase 2** must ensure `BaseElementCollectionService` correctly flags Shared Parameters.
