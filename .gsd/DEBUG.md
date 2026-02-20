# Debug Session: Missing Parameters with Formulas

## Symptom
User reports that parameters with formulas are being skipped during the batch rename/search replace process.

**When:** During collection of Family Parameters in `SearchReplaceCommand`.
**Expected:** Parameters with formulas should be listed for renaming.
**Actual:** Parameters with formulas are excluded.

## Evidence
- Code review of `BaseElementCollectionService.cs` shows explicit filtering of `IsReadOnly` parameters.
- Usually `IsReadOnly` parameters (built-in) cannot be renamed or modified.
- However, Family Parameters driven by formulas are *also marked IsReadOnly* in the project context (FamilySymbol parameters).
- The `BatchRenameExecutionService` renames parameters by opening the family document and using `FamilyManager.RenameParameter`, which allows renaming definition even if it has a formula.

## Hypotheses

| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `BaseElementCollectionService` filters out `IsReadOnly` parameters, excluding formula-driven parameters. | 95% | CONFIRMED |

## Attempts

### Attempt 1
**Testing:** H1 — Code review.
**Action:** Checked `src/Services/BaseElementCollectionService.cs`.
**Result:** Found `!p.IsReadOnly` check at line 178.
**Conclusion:** CONFIRMED. This check is too aggressive for Family Parameters where we intend to rename definitions.

## Resolution

**Root Cause:** `BaseElementCollectionService` filters out all read-only parameters.
**Fix:** Removed `!p.IsReadOnly` check for Family Parameters in `BaseElementCollectionService.cs`.
**File:** `src/Services/BaseElementCollectionService.cs`
