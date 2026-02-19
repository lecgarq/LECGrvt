# Debug Session: Force Rename of Object Styles / Subcategories

## Symptom
User insists on renaming `GraphicsStyle` elements like `RD_...` which fail with `InvalidOperationException: This element does not support assignment of a user-specified name.`

**When:** Batch Renaming specific styles (likely imported or system-locked).
**Expected:** The styles MUST be renamed.
**Actual:** API blocks direct renaming.

## New Strategy: "Swap & Replace"
Since direct property setting is blocked, we will implement a destructive workaround:
1.  **Create New Subcategory**: Create a new subcategory under the same parent with the desired name.
2.  **Clone Properties**: Copy LineWeight, LineColor, LinePattern from old to new.
3.  **Migrate Elements**: Find all `CurveElement` (lines) and potentially other elements using the old style and reassign them to the new style.
4.  **Delete Old Style**: Remove the original style if possible.

## Risks
-   **Limited Scope**: Only safe for `CurveElement` (Model/Detail Lines). Might miss complex usages (Filled Regions, Imports).
-   **Destructive**: Deleting a category might break external references or view templates if not perfectly mapped.
-   **Imported Categories**: Creating subcategories under "Imports in Families" is generally restricted. If `RD_` are imported CAD layers, we might fail to create the *exact* same structure.

## Hypotheses

| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | "Swap & Replace" works for standard Line Styles (Subcategories of Lines). | 80% | UNTESTED |
| 2 | "Swap & Replace" fails for Imported Categories (cannot create subcat under Import). | 60% | UNTESTED |
| 3 | User accepts partial success (lines migrated) even if original imported category remains (empty). | 50% | UNTESTED |

## Approach
1.  Extend `BatchRenameExecutionService` to attempt "Swap & Replace" when direct rename fails.
2.  Implement `SwapStyle` helper method.
3.  Focus on `CurveElement` migration first.
