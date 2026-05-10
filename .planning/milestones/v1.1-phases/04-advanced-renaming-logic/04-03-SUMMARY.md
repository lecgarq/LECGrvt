---
phase: 04-advanced-renaming-logic
plan: "03"
subsystem: BatchRenaming
tags: [formula-update, sub-transaction, tdd, REQ-02]
dependency_graph:
  requires: [04-00, 04-01, 04-02]
  provides: [IFormulaUpdateService-consumer, per-param-SubTransaction, CollectFormulaUpdates-pure-helper]
  affects: [BatchRenameExecutionService, FormulaUpdateServiceTests, BatchRenameSafeRenameTests]
tech_stack:
  added: []
  patterns: [SubTransaction-per-param, pure-data-helper-for-testability, TDD-RED-GREEN]
key_files:
  created: []
  modified:
    - src/Services/Renaming/BatchRenameExecutionService.cs
    - LECG.Tests/Services/FormulaUpdateServiceTests.cs
    - LECG.Tests/Services/BatchRenameSafeRenameTests.cs
decisions:
  - IFormulaUpdateService injected as last constructor parameter (after loadOptionsFactory) to minimize call-site churn
  - RenameFamilyParameters converted from static to instance method to access _formulaUpdateService field
  - CollectFormulaUpdates extracted as internal static pure-data helper for testability without RevitAPI.dll
  - LogRenameSuccess extracted as internal static helper to keep log-format consistent + unit-testable
  - Injectable constructor test uses reflection (typeof(BatchRenameExecutionService).GetConstructors()) rather than NSubstitute mocks for ITransactionService/IFamilyLoadOptionsFactory — those interfaces have Document parameters; Castle DynamicProxy fails to proxy them without RevitAPI.dll loaded
  - formulaReferenced.Contains opt-in guard skips CollectFormulaUpdates loop entirely for non-formula-referenced params (performance, no behaviour change)
  - Pitfall 3 outer-loop bug (count += renamedInFamily at line ~219 incremented before transaction confirmed) remains deferred to v1.2 Phase 6 per plan guidance
  - SubTransaction explicit RollBack() in catch rather than relying on using-dispose-only; subTx.Commit() is still inside try so on any exception the explicit RollBack fires before the catch-log
metrics:
  duration: 20min
  completed: "2026-05-10"
  tasks: 2
  files: 3
---

# Phase 04 Plan 03: IFormulaUpdateService Wiring + Per-Param SubTransaction Summary

**One-liner:** Constructor injection of IFormulaUpdateService into BatchRenameExecutionService with per-param SubTransaction wrapping RenameParameter + formula-rewrite loop, closing REQ-02.

## Tasks Completed

| Task | Commit | Description |
|------|--------|-------------|
| 1 — Inject IFormulaUpdateService + FormulaUpdateServiceTests GREEN | 7cdc10e | Constructor param added; 3 RED tests un-skipped and GREEN |
| 2 — Per-param SubTransaction + formula-update loop | fa83862 | RenameFamilyParameters rewritten; 4 REQ-02 tests GREEN |

## Constructor Signature Change

`BatchRenameExecutionService` constructor was:
```csharp
public BatchRenameExecutionService(ITransactionService transactionService, IFamilyLoadOptionsFactory loadOptionsFactory)
```

Now:
```csharp
public BatchRenameExecutionService(ITransactionService transactionService, IFamilyLoadOptionsFactory loadOptionsFactory, IFormulaUpdateService formulaUpdateService)
```

Parameter placed at the end. DI registration in `Bootstrapper.cs:131` is unchanged — `services.AddSingleton<IFormulaUpdateService, FormulaUpdateService>()` was already present; MS DI resolves the new parameter automatically.

## SubTransaction Pattern Location

`RenameFamilyParameters` (~line 270 in updated file) — inside the `_transactionService.RunConditional` lambda that wraps the EditFamily session. Each parameter's rename is independently:

1. `var subTx = new SubTransaction(famDoc); subTx.Start();`
2. `manager.RenameParameter(paramToRename, item.NewValue);`
3. Formula-update loop (gated by `formulaReferenced.Contains`)
4. `subTx.Commit();`
5. AFTER commit: `renamedCount++` + `LogRenameSuccess(logger, old, new, count)`
6. On exception: explicit `subTx.RollBack()` + `LogWarning` + no count increment

## Formula-Update Loop Guard

The `formulaReferenced` HashSet (built by `BuildFormulaReferencedNames` before the transaction opens) acts as an opt-in guard. If `item.OriginalValue` is not in the set, `CollectFormulaUpdates` is never called — no FamilyParameter iteration overhead for the common case.

## Pure-Data Helpers Added

- `CollectFormulaUpdates(IEnumerable<(string name, string formula)>, string oldName, string newName, IFormulaUpdateService)` — returns `List<(string name, string updatedFormula)>`. No Revit API; fully testable.
- `LogRenameSuccess(ILogger, string oldName, string newName, int formulaCount)` — emits `"Renamed '{old}' to '{new}' (updated N formulas)"` when N > 0, plain `"Renamed '{old}' to '{new}'"` otherwise.
- `GroupCheckedFamilyParameterItemsForTest` — thin test wrapper delegating to the existing private `GroupCheckedFamilyParameterItems`.

## Logging Format

| Condition | Format |
|-----------|--------|
| formulaCount > 0 | `Renamed 'Width' to 'PanelWidth' (updated 2 formulas)` |
| formulaCount == 0 | `Renamed 'Width' to 'PanelWidth'` |
| SubTransaction throws | `Skipped 'Width' in 'MyFamily': {ex.Message}` (LogWarning) |

## Test Count Flipped RED → GREEN

| Fixture | Tests | Before | After |
|---------|-------|--------|-------|
| FormulaUpdateServiceTests | 3 | Skip-gated (RED) | GREEN |
| BatchRenameSafeRenameTests — REQ-02 | 4 | Skip-gated (RED) | GREEN |

Total: **7 tests flipped RED → GREEN**. REQ-03 tests (3) remain Skipped — owned by Plan 04-04.

## Pitfall 3 Status

The inner `renamedCount++` is now post-commit-guarded (fixed in this plan). The outer `count += renamedInFamily` at the call site (inside `RunConditional`) is also fine since it reads `renamedInFamily` after `RenameFamilyParameters` returns. The pre-existing v1.2-deferred Pitfall 3 refers to the outer `count += renamedInFamily` which happens inside a `RunConditional` that could itself roll back — that outer-loop concern is out of scope for this plan per plan guidance.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] NSubstitute cannot proxy Revit-API-dependent interfaces in test runner**
- **Found during:** Task 1 — `IFormulaUpdateService_IsInjectableIntoBatchRenameExecutionService_ViaConstructor`
- **Issue:** `ITransactionService` has `Document` parameters; Castle DynamicProxy tries to load RevitAPI.dll to inspect method signatures and throws `FileNotFoundException` at proxy generation time
- **Fix:** Used reflection (`typeof(BatchRenameExecutionService).GetConstructors()`) to verify the parameter list instead of constructing an instance. Test proves the constructor accepts `IFormulaUpdateService` without touching Revit types.
- **Files modified:** `LECG.Tests/Services/FormulaUpdateServiceTests.cs`
- **Commit:** 7cdc10e

**2. [Rule 1 - Design] TryRenameFamilyParameter replaced by inline SubTransaction pattern**
- **Found during:** Task 2 implementation
- **Issue:** The old `TryRenameFamilyParameter` helper could not participate in the SubTransaction (it called `manager.RenameParameter` directly with no transaction context return). The new per-param SubTransaction wraps rename + formula-update atomically, so the helper was superseded.
- **Fix:** Removed `TryRenameFamilyParameter`; all rename logic is now inline inside the SubTransaction try/catch block.
- **Files modified:** `src/Services/Renaming/BatchRenameExecutionService.cs`
- **Commit:** fa83862

## Self-Check: PASSED

- FOUND: `src/Services/Renaming/BatchRenameExecutionService.cs`
- FOUND: `LECG.Tests/Services/FormulaUpdateServiceTests.cs`
- FOUND: `LECG.Tests/Services/BatchRenameSafeRenameTests.cs`
- FOUND: commit `7cdc10e` (Task 1)
- FOUND: commit `fa83862` (Task 2)
