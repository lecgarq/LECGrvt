# Debug Session: Family Parameter Rename Crash

## Symptom
Batch rename crashes with "CRITICAL ERROR: The referenced object is not valid, possibly because it has been deleted from the database, or its creation was undone."

**When:** Applying batch rename with 1850 family parameters selected.
**Expected:** All family parameters are renamed successfully.
**Actual:** Crashes on the first operation.

## Evidence

- 1850 items, 0 standard — all family parameters.
- Error occurs only ~1 second after start (fails on first family or immediately).
- The Revit error "referenced object is not valid" typically means an API handle to an element is no longer valid.

## Hypotheses

| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `famDoc.LoadFamily(doc, ...)` invalidates existing `Family` element references, causing subsequent iterations to crash | 85% | CONFIRMED |
| 2 | Multiple items per family cause `EditFamily` to be called again on an already-open/reloaded family | 85% | CONFIRMED |
| 3 | `fs.Parameters` on FamilySymbol returns project-context params that don't exist in the family doc | 20% | PARTIALLY |

## Root Cause

`BatchRenameExecutionService` calls `doc.EditFamily(family)` → renames → `famDoc.LoadFamily(doc, ...)` → `famDoc.Close(false)`.

When `LoadFamily` is called, Revit **replaces** the `Family` element in the project document with a new version. Any previously-fetched references (like `family` held by a prior loop iteration) become **invalid**. If the next item in the loop targets the same family (which is likely with 1850 params across a limited number of families), the element reference is stale.

Additionally, calling `EditFamily` on the same family multiple times (once per parameter) is very expensive and fragile. It should be called once per unique family, renaming all target parameters in one session.

## Fix

Group `familyItems` by `ElementId` (Family ID). For each unique family:
1. Call `doc.EditFamily(family)` **once**.
2. Iterate all parameters to rename in that family.
3. Call `famDoc.LoadFamily(doc, ...)` **once** after all renames.
4. Call `famDoc.Close(false)` **once**.

## Resolution

**Root Cause:** Calling `EditFamily + LoadFamily + Close` once per parameter item (instead of once per family) causes element invalidation on reloads.
**Fix:** Group items by family ID, process all parameters per family in a single `EditFamily` session.
**File:** `BatchRenameExecutionService.cs`
