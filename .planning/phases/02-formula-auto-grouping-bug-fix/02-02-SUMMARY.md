---
phase: 02-formula-auto-grouping-bug-fix
plan: 02
subsystem: commands
tags: [revit-api, family-manager, formula-grouping, shared-parameters]

# Dependency graph
requires:
  - phase: 02-formula-auto-grouping-bug-fix
    plan: 01
    provides: EnsureCurrentType guard in TrySetFormula; SubTransaction loop in MoveParamsToGroup
provides:
  - Clear-replace-restore sequence in TryReplaceSharedParameterGroup for cross-parameter formula dependencies
  - FormulaNameUpdater.ContainsReference integration for detecting referencing parameters before ReplaceParameter
affects:
  - Manual Revit verification (FormulaAutoGrouping command with formula-bearing parameters)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Clear-replace-restore: scan for cross-references, clear formulas, call disruptive API, restore via FindParamByName

key-files:
  created: []
  modified:
    - src/Commands/FormulaAutoGroupingCommand.cs

key-decisions:
  - "Add using LECG.Core.Rename to FormulaAutoGroupingCommand.cs — FormulaNameUpdater lives in that namespace"
  - "Use string.Empty (not null) when calling TrySetFormula to clear a formula — parameter is non-nullable string"
  - "Cross-reference restore is best-effort: TrySetFormula with out _ so a single restore failure never aborts the parameter move"

patterns-established:
  - "Clear-replace-restore: before any Revit API call that throws on formula cross-references, scan referencing params with ContainsReference, clear, call API, restore via FindParamByName (live object after potential stale reference)"

requirements-completed: [REQ-07]

# Metrics
duration: 10min
completed: 2026-04-29
---

# Phase 2 Plan 2: FormulaAutoGrouping Clear-Replace-Restore Summary

**Cross-parameter formula dependency guard for TryReplaceSharedParameterGroup: scan-clear-replace-restore sequence using FormulaNameUpdater.ContainsReference and FindParamByName**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-04-29T05:52:11Z
- **Completed:** 2026-04-29T06:02:00Z
- **Tasks:** 1 of 2 automated (Task 2 is human-verify checkpoint — pending)
- **Files modified:** 1

## Accomplishments
- Added `using LECG.Core.Rename;` to FormulaAutoGroupingCommand.cs to access FormulaNameUpdater.ContainsReference
- Inserted clear phase before ReplaceParameter: scans all fm.Parameters (excluding the target), builds formulasToRestore list for any parameter whose formula references the target by name, clears those formulas using string.Empty
- Inserted restore phase after GUID and group persistence checks: calls EnsureCurrentType (best effort), then iterates formulasToRestore, looks up each parameter by name via FindParamByName (handles potentially stale FamilyParameter references after ReplaceParameter), and restores the saved formula
- Entire restore path is best-effort — a failed restore never causes TryReplaceSharedParameterGroup to return false; the replaced parameter's OWN formula restoration block remains the only hard-failure path
- Build: 0 errors, 0 warnings; test suite: 79 passed, 0 failed

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement clear-replace-restore sequence in TryReplaceSharedParameterGroup** - `049904e` (fix)

Task 2 (human-verify checkpoint) is pending manual Revit verification.

## Files Created/Modified
- `src/Commands/FormulaAutoGroupingCommand.cs` - Added using LECG.Core.Rename; clear-replace-restore sequence in TryReplaceSharedParameterGroup (30 lines inserted)

## Decisions Made
- `string.Empty` used to clear formula instead of `null` because TrySetFormula's `formula` parameter is non-nullable `string` — passing null would generate CS8625 warning and is semantically wrong
- Restore is best-effort by design: the goal is "move the parameter to Other group"; if a cross-referencing parameter's formula cannot be restored, the move itself should still be reported as successful
- `EnsureCurrentType` called at the start of the restore loop, not per-iteration, since it iterates fm.Types (cheap) and setting it once is sufficient for all TrySetFormula calls in the same transaction context

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Changed null to string.Empty in TrySetFormula clear call**
- **Found during:** Task 1 (build verification step)
- **Issue:** Plan code snippet used `null` but TrySetFormula's formula parameter is non-nullable `string`, producing CS8625 warning
- **Fix:** Changed `TrySetFormula(familyManager, fp, null, out _)` to `TrySetFormula(familyManager, fp, string.Empty, out _)`
- **Files modified:** src/Commands/FormulaAutoGroupingCommand.cs
- **Verification:** Rebuild produced 0 warnings, 0 errors
- **Committed in:** 049904e (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - type-correctness bug in plan snippet)
**Impact on plan:** Fix was necessary for a clean build; no behavioral change — Revit treats empty string the same as null for formula clearing.

## Issues Encountered
- `dotnet build ... -x64` flag is invalid for dotnet CLI on this platform (same issue as Plan 01); used `-p:Platform=x64` instead.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All automated implementation complete — clear-replace-restore sequence is compiled and tested
- Pending: Manual Revit verification (Task 2 checkpoint) to confirm formula parameters move to "Other" group without "Skipped" messages
- Full combined fix (Plans 01 + 02) is deployed to Revit addins folder at build time

---
*Phase: 02-formula-auto-grouping-bug-fix*
*Completed: 2026-04-29*
