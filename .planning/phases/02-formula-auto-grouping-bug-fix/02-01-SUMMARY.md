---
phase: 02-formula-auto-grouping-bug-fix
plan: 01
subsystem: commands
tags: [revit-api, family-manager, subtransaction, formula-grouping]

# Dependency graph
requires: []
provides:
  - Per-parameter SubTransaction loop in MoveParamsToGroup (one failure logs and continues)
  - Removal of unreliable in-transaction GetGroupTypeId() false-negative check
  - EnsureCurrentType guard in TrySetFormula preventing SetFormula crash on typeless families
  - EnsureParametersPersistInGroup restricted to post-reload verification only
  - xUnit test scaffold covering FormulaNameUpdater.ContainsReference word-boundary logic
affects:
  - 02-formula-auto-grouping-bug-fix/02-02 (Plan 02 will call EnsureCurrentType via TrySetFormula)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - SubTransaction per-item for partial-success loops inside a parent Transaction
    - EnsureCurrentType guard as precondition before FamilyManager.SetFormula

key-files:
  created:
    - LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs
  modified:
    - src/Commands/FormulaAutoGroupingCommand.cs

key-decisions:
  - "Use SubTransaction per parameter in MoveParamsToGroup so a single unsupported move logs and continues rather than aborting all params"
  - "Remove GetGroupTypeId() post-call check from TrySetParameterGroup — in-transaction read is a false-negative; group persistence is verified post-reload by EnsureParametersPersistInGroup"
  - "EnsureParametersPersistInGroup now only checks group membership, not formula presence — formula preservation is a Revit API concern outside this command's enforcement boundary"

patterns-established:
  - "SubTransaction-per-item: wrap each risky API call in its own SubTransaction; commit on success, RollBack and log on failure, continue loop"
  - "EnsureCurrentType guard: call before any FamilyManager.SetFormula to prevent crash on families with no current type"

requirements-completed: [REQ-07]

# Metrics
duration: 15min
completed: 2026-04-28
---

# Phase 2 Plan 1: FormulaAutoGrouping Structural Fixes Summary

**SubTransaction per-parameter loop, EnsureCurrentType guard, and removal of in-transaction false-negative group check to eliminate silent rollbacks and single-param abort failures**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-04-28T00:00:00Z
- **Completed:** 2026-04-28T00:15:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Created xUnit test scaffold (4 tests, Category=FormulaGrouping) covering ContainsReference word-boundary logic used by Plan 02's clear-replace-restore trigger
- Replaced all-or-nothing throw in MoveParamsToGroup with per-parameter SubTransaction — one unsupported parameter now logs and continues instead of aborting the entire batch
- Removed the unreliable in-transaction GetGroupTypeId() persistence check from TrySetParameterGroup that was generating false-negatives (Revit reflects group changes post-commit, not mid-transaction)
- Added EnsureCurrentType guard to TrySetFormula so SetFormula is never called on a family with no current type; added the EnsureCurrentType helper method
- Removed EnsureParametersPersistInGroup from the live transaction paths in ExecuteInFamilyDocument and ProcessProjectFamily; removed formula-presence check from the method — post-reload verification now only confirms group membership

## Task Commits

Each task was committed atomically:

1. **Task 1: Create test scaffold for FormulaAutoGroupingCommand pure-logic paths** - `efc110e` (test)
2. **Task 2: Apply four structural fixes to FormulaAutoGroupingCommand** - `36481c3` (fix)

**Plan metadata:** (docs commit follows)

_Note: Task 1 used TDD flow — tests written and verified green before Task 2 implementation_

## Files Created/Modified
- `LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs` - xUnit tests for ContainsReference word-boundary logic under Category=FormulaGrouping
- `src/Commands/FormulaAutoGroupingCommand.cs` - Four structural fixes: SubTransaction loop, no in-transaction group check, EnsureCurrentType guard, EnsureParametersPersistInGroup out of live transaction

## Decisions Made
- SubTransaction per parameter chosen over try/catch per parameter because SubTransaction gives Revit a clean rollback point per item, not just exception swallowing.
- Formula-presence check removed from EnsureParametersPersistInGroup because the method is now a post-reload verifier for group membership only; formula preservation responsibility belongs to the Revit API round-trip, not this command.
- EnsureCurrentType iterates fm.Types and sets the first found type, matching the existing TrySetCurrentType helper pattern already in the file.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- `dotnet test ... -x64` flag is not valid for `dotnet test` on this platform; replaced with `-p:Platform=x64`. Build and tests passed on first corrected invocation.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 02 (TryReplaceSharedParameterGroup clear-restore sequence) can now call EnsureCurrentType via TrySetFormula safely
- All four prerequisite structural fixes from research Failure Paths 1, 3, and 4 are in place
- Full test suite is green (79 passed, 0 failed)

---
*Phase: 02-formula-auto-grouping-bug-fix*
*Completed: 2026-04-28*
