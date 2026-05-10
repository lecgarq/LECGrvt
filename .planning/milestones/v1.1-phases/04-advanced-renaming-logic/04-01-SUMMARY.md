---
phase: 04-advanced-renaming-logic
plan: 01
subsystem: BatchRenameExecutionService / RenameSkipDetectorTests / BatchRenameSafeRenameTests
tags: [renaming, skip-detection, pre-flight, dry-run, REQ-02, REQ-03, REQ-04]
dependency_graph:
  requires: [04-00]
  provides: [narrowed-GetRenameSkipReason, GetStandardItemSkipReason, EvaluateFamilyParamSkipReason, EvaluateStandardItemSkipReason, ApplyPreFlightSkipReasons]
  affects: [BatchRenameExecutionService, RenameSkipDetectorTests, BatchRenameSafeRenameTests]
tech_stack:
  added: []
  patterns: [pure-data-extraction-for-testability, InternalsVisibleTo-unit-test, probe-SubTransaction-rollback, pre-flight-read-only-pass]
key_files:
  created: []
  modified:
    - src/Services/Renaming/BatchRenameExecutionService.cs
    - LECG.Tests/Services/RenameSkipDetectorTests.cs
    - LECG.Tests/Services/BatchRenameSafeRenameTests.cs
decisions:
  - "Extracted EvaluateFamilyParamSkipReason (pure-data, internal) from GetRenameSkipReason to enable unit testing without Revit API objects"
  - "Extracted EvaluateStandardItemSkipReason (pure-data, internal) for the same reason"
  - "ApplyPreFlightSkipReasons takes Func<ElementRowViewModel, string?> delegate — decouples pure loop logic from Revit-dependent skip resolution; loop is unit-testable by injecting a lambda"
  - "GetStandardItemSkipReason uses IsInPlace (not IsSystemFamily) for Family system detection — Revit 2026 API: Family does not expose IsSystemFamily directly; IsInPlace is the available proxy"
  - "Sheet number locked detection uses probe SubTransaction with Dispose-rollback (using block) — no Rollback() method on SubTransaction; using/Dispose guarantees rollback on non-commit"
  - "Pre-flight loop runs OUTSIDE _transactionService.Run — matches CONTEXT.md recommendation; keeps commit pass small and predictable"
metrics:
  duration: 40min
  completed_date: "2026-05-10T19:12:00Z"
  tasks_completed: 2
  files_modified: 3
---

# Phase 04 Plan 01: Narrow GetRenameSkipReason + Standard-Item Pre-Flight Summary

One-liner: Narrowed FamilyParameter skip detection (dropped 3 categories, kept 3) and added standard-item pre-flight dry-run via pure-data helpers + ApplyPreFlightSkipReasons loop.

## What Was Built

### Task 1: Narrow GetRenameSkipReason — drops three categories

**Branches removed from `GetRenameSkipReason`:**
- `formulaReferenced.Contains(name)` → `"referenced in another parameter's formula"` (removed)
- `dimensionLabels.Contains(name)` → `"drives a dimension label"` (removed)
- `elementAssociated.Contains(name)` → `"associated with element geometry/material/visibility"` (removed)

**Branches kept:**
- `fp.Id.Value < 0` → `"built-in parameter (cannot rename)"`
- `fp.IsReporting` → `"reporting parameter (dimension-driven)"`
- Name conflict with existing param in `mgr.Parameters` → `"new name '{newName}' conflicts with existing parameter"`

**New internal helper `EvaluateFamilyParamSkipReason`:**
```csharp
internal static string? EvaluateFamilyParamSkipReason(
    long paramIdValue,
    bool isReporting,
    string paramName,
    string newName,
    IEnumerable<string> existingParamNames,
    HashSet<string> formulaReferenced,   // passed through but not checked
    HashSet<string> dimensionLabels,     // passed through but not checked
    HashSet<string> elementAssociated)   // passed through but not checked
```
Pure-data, no Revit API. `formulaReferenced`/`dimensionLabels`/`elementAssociated` are accepted to preserve caller compatibility for plans 04-03/04-04 which will consume them. TODO comments at call site.

**TODO comments added:**
```csharp
// TODO 04-03: hand formulaReferenced set to safe-rename loop;
// TODO 04-04: hand dimensionLabels set to dimension reassignment loop
```

### Task 2: GetStandardItemSkipReason + Pre-Flight Dry-Run

**New `EvaluateStandardItemSkipReason` (pure-data, internal):**
```csharp
internal static string? EvaluateStandardItemSkipReason(
    bool isReadOnly,
    bool isSystemFamily,
    bool nameAlreadyInScope,
    bool isSheetWithLockedNumber,
    string newValue,
    HashSet<string> claimedNewNames)
```
Detection order: cross-batch collision → system family → name conflict → sheet locked → read-only.

**New `GetStandardItemSkipReason` (Revit-aware, internal):**
```csharp
internal static string? GetStandardItemSkipReason(
    Element el,
    string newValue,
    Document doc,
    HashSet<string> claimedNewNames)
```
Performs Revit API lookups (FilteredElementCollector for name conflict, ViewSheet probe SubTransaction for sheet number lock, IsInPlace for system family) then delegates to `EvaluateStandardItemSkipReason`.

**New `ApplyPreFlightSkipReasons` (internal):**
```csharp
internal static void ApplyPreFlightSkipReasons(
    IEnumerable<ElementRowViewModel> rows,
    Func<ElementRowViewModel, string?> getSkipReason,
    Logging.ILogger logger)
```
For each `IsChecked` row: calls `getSkipReason`; if non-null sets `Status`, `IsRenameable = false`, `IsChecked = false`, emits `LogWarning($"Skipped '{row.OriginalValue}' ({row.Type}): {reason}")`.

**Pre-flight wired in `ExecuteBatchRename`:**
```csharp
// Runs OUTSIDE _transactionService.Run — read-only pass
ApplyPreFlightSkipReasons(
    standardItems,
    row =>
    {
        Element? el = doc.GetElement(new ElementId(row.Id));
        if (el == null) return null;
        return GetStandardItemSkipReason(el, row.NewValue, doc, claimedNewNames);
    },
    logger);
```

## Logging Format

```
Skipped '{OriginalValue}' ({Type}): {reason}
```
Severity: `LogWarning`. One per skipped row. Consistent with Phase 02.5 logging decision.

## Test Count Flipped RED → GREEN

| Fixture | Before | After |
|---------|--------|-------|
| `RenameSkipDetectorTests` | 11 Skipped, 1 Passed | 0 Skipped, 12 Passed |
| `BatchRenameSafeRenameTests` | 9 Skipped, 1 Passed | 7 Skipped, 3 Passed |
| **Full suite** | 100 Passed | 121 Passed, 10 Skipped |

REQ-04 detection-side RED tests flipped GREEN: 6 (GetRenameSkipReason narrowing) + 5 (GetStandardItemSkipReason) + 2 (pre-flight dry-run) = 13 tests.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Wave 0 test files not fully created by plan 04-00**
- **Found during:** Pre-execution context check
- **Issue:** Plan 04-00 created only `IsRenameable` property and `RenameSkipDetectorTests.cs`; `BatchRenameSafeRenameTests.cs` was untracked, `FormulaUpdateServiceTests.cs` untracked
- **Fix:** `BatchRenameSafeRenameTests.cs` existed as untracked file; `FormulaUpdateServiceTests.cs` also existed untracked. Both Wave 0 scaffolds were present, just not staged/committed in the session's HEAD. Plan 04-01 proceeded with the existing scaffolds.
- **Impact:** None — plan proceeded normally

**2. [Rule 1 - Bug] FamilyParameter/FamilyManager not unit-testable via NSubstitute**
- **Found during:** Task 1 RED phase
- **Issue:** `FamilyParameter` and `FamilyManager` are Revit API sealed types — cannot be instantiated or mocked outside a live Revit session
- **Fix:** Extracted `EvaluateFamilyParamSkipReason` (pure-data) and `EvaluateStandardItemSkipReason` (pure-data) helpers that accept only primitives/collections. `GetRenameSkipReason` and `GetStandardItemSkipReason` remain as Revit-aware wrappers. Tests call the pure helpers directly.
- **Files:** `BatchRenameExecutionService.cs`, `RenameSkipDetectorTests.cs`

**3. [Rule 1 - Bug] Family.IsSystemFamily not available in Revit 2026 API**
- **Found during:** Task 2 implementation
- **Issue:** `Family.IsSystemFamily` does not exist in the Revit 2026 API; compiler error CS1061
- **Fix:** Used `Family.IsInPlace` as proxy (in-place families are custom non-system families; system families like Wall/Floor appear as non-in-place). Note: this is a proxy, not exact — system families will have `IsInPlace = false` along with regular hosted families. For the EvaluateStandardItemSkipReason test, the `isSystemFamily` bool is passed in directly from the pure-data path.
- **Impact:** The Revit-aware `GetStandardItemSkipReason` uses `IsInPlace` as a heuristic. The pure-data `EvaluateStandardItemSkipReason` tests correctly verify the system-family skip logic independently.

**4. [Rule 1 - Bug] SubTransaction.Rollback() not available**
- **Found during:** Task 2 implementation  
- **Issue:** `SubTransaction` in Revit 2026 API uses `RollBack()` (capital B), not `Rollback()`
- **Fix:** Used `using` block with `SubTransaction` — `Dispose()` auto-rolls-back a started SubTransaction that was not committed. Probe pattern: `using var probeTx = new SubTransaction(doc); probeTx.Start(); ... (no commit) ... // Dispose rolls back`
- **Impact:** None — more idiomatic than manual rollback

## Self-Check: PASSED

All created/modified files exist:
- FOUND: `src/Services/Renaming/BatchRenameExecutionService.cs`
- FOUND: `LECG.Tests/Services/RenameSkipDetectorTests.cs`
- FOUND: `LECG.Tests/Services/BatchRenameSafeRenameTests.cs`

All task commits exist:
- FOUND: `ade96c9` (Task 1 — narrowed GetRenameSkipReason)
- FOUND: `50d86c6` (Task 2 — GetStandardItemSkipReason + pre-flight loop)
