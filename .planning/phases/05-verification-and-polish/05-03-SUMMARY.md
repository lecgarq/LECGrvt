---
phase: 05-verification-and-polish
plan: 03
subsystem: testing
tags: [tdd, batch-rename, progress, dimension, family-parameter, csharp]

# Dependency graph
requires:
  - phase: 05-verification-and-polish
    provides: Wave 2 direct-coverage GREEN tests for BatchRenameExecutionService
  - phase: 04-safe-rename
    provides: ExecuteDimensionReassignments single-action overload, RunConditional transaction pattern

provides:
  - AccumulateCommittedFamilyCount pure-data helper (count only after committed)
  - ExecuteDimensionReassignments pair-action overload (null-clear before assign)
  - BuildProgressSequence pure-data helper (unified 0-100 progress arithmetic)
  - 3 production rewires (count update post-commit; pair-action for dims; family loop denominator)
  - Matrix row 6 fully green; all 6 matrix rows now ✅

affects:
  - 05-04 (manual Revit verification wave references these fixes)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TDD RED→GREEN for pure-data helper extraction: write tests against helper contract before adding helper"
    - "Pair-action pattern for (clear, assign) Revit property mutations — backward-compatible alongside single-action overload"
    - "Unified progress denominator: both standard and family loops use shared current/total for monotonic 0→100"

key-files:
  created: []
  modified:
    - src/Services/Renaming/BatchRenameExecutionService.cs
    - LECG.Tests/Services/BatchRenameExecutionServiceTests.cs
    - .planning/phases/05-verification-and-polish/05-VERIFICATION.md

key-decisions:
  - "[Phase 05-03]: AccumulateCommittedFamilyCount extracts count-update-on-commit as a 1-line expression method; count mutation moved from inside RunConditional lambda to post-commit site"
  - "[Phase 05-03]: Pair-action overload for ExecuteDimensionReassignments added alongside (not replacing) single-action overload — backward-compatible; existing 9 REQ-03 tests stay GREEN"
  - "[Phase 05-03]: BuildProgressSequence emits one step per standard item (weight 1) and one step per family group weighted by row count; max is guaranteed 100.0 by shared denominator"
  - "[Phase 05-03]: Family loop denominator unified with standard loop via Option A (share current/total); helper exists for unit-test verifiability but production uses direct arithmetic"

patterns-established:
  - "Polish #1 pattern: move side-effect (count increment) outside a conditional lambda to post-result observation — prevents phantom increments on rollback"
  - "Polish #2 pattern: clear-before-set for Revit properties that reject overwrite — pair-action tuple encapsulates both actions atomically"

requirements-completed:
  - REQ-05

# Metrics
duration: 12min
completed: 2026-05-10
---

# Phase 05-03: Polish Fixes (count-on-rollback, null-clear dim labels, progress capping) Summary

**3 pure-data helper extractions (AccumulateCommittedFamilyCount, ExecuteDimensionReassignments pair-action overload, BuildProgressSequence) with RED→GREEN TDD cycles and production rewires; matrix row 6 fully ✅; full suite 175 GREEN**

## Performance

- **Duration:** 12 min
- **Started:** 2026-05-10T22:06:30Z
- **Completed:** 2026-05-10T22:18:30Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- Polish #1: `AccumulateCommittedFamilyCount(bool, int, int) → int` extracted; `count += renamedInFamily` moved out of `RunConditional` lambda to post-commit site — prevents phantom count inflation on transaction rollback
- Polish #2: Pair-action `ExecuteDimensionReassignments(IReadOnlyList<(Action clear, Action assign)>, ...)` overload added; production dimension loop switched to pairs with null-clear before reassign (C1 follow-up from Phase 4)
- Polish #3: `BuildProgressSequence(int, IReadOnlyList<int>) → IReadOnlyList<double>` extracted; family loop denominator unified with standard loop (`current += kvp.Value.Count` / shared `total`); progress is monotonic 0→100 across all batch types
- Matrix row 6 flipped from "direct ✅, polish pending Wave 3" to fully ✅; all 6 matrix rows now ✅
- Full xUnit suite: 175 GREEN, 5 Skipped, 0 Failed (target was ≥ 165; previous was 170 GREEN)

## Task Commits

Each task was committed atomically (RED then GREEN per TDD):

1. **Task 1 RED: AccumulateCommittedFamilyCount failing tests** — `0cb16a7` (test)
2. **Task 1 GREEN: AccumulateCommittedFamilyCount helper + production rewire (Polish #1)** — `64f409d` (feat)
3. **Task 2 RED: pair-action ExecuteDimensionReassignments failing test** — `fad6a20` (test)
4. **Task 2 GREEN: pair-action overload + null-clear production (Polish #2)** — `2a41aca` (feat)
5. **Task 3 RED: BuildProgressSequence failing tests** — `ddf31d0` (test)
6. **Task 3 GREEN: BuildProgressSequence + unified denominator + matrix close (Polish #3)** — `93a2c30` (feat)

_Note: TDD tasks have two commits each (RED test → GREEN implementation)_

## Helper Signatures

```csharp
// Polish #1 — src/Services/Renaming/BatchRenameExecutionService.cs
internal static int AccumulateCommittedFamilyCount(bool committed, int renamedInFamily, int currentCount)
    => committed ? currentCount + renamedInFamily : currentCount;

// Polish #2 — new overload (existing IReadOnlyList<Action> overload retained)
internal static void ExecuteDimensionReassignments(
    IReadOnlyList<(Action clear, Action assign)> pairs,
    string oldName,
    string newName,
    out int dimCount);

// Polish #3
internal static IReadOnlyList<double> BuildProgressSequence(
    int standardCount,
    IReadOnlyList<int> familyGroupRowCounts);
```

## Production Call-Site Diffs

### Polish #1 — count update relocated post-commit
**Before:**
```csharp
bool committed = _transactionService.RunConditional(famDoc, "Rename Parameters", _ =>
{
    renamedInFamily = RenameFamilyParameters(...);
    count += renamedInFamily;  // BUG: inside lambda, fires even on rollback
    return renamedInFamily > 0;
});
```
**After:**
```csharp
bool committed = _transactionService.RunConditional(famDoc, "Rename Parameters", _ =>
{
    renamedInFamily = RenameFamilyParameters(...);
    return renamedInFamily > 0;
});
count = AccumulateCommittedFamilyCount(committed, renamedInFamily, count);  // post-commit
```

### Polish #2 — null-clear before reassign
**Before:**
```csharp
reassignActions.Add(() => { capturedDim.FamilyLabel = capturedRef; });
ExecuteDimensionReassignments(reassignActions, item.OriginalValue, item.NewValue, out dimCount);
```
**After:**
```csharp
reassignPairs.Add((
    clear:  () => { capturedDim.FamilyLabel = null; },
    assign: () => { capturedDim.FamilyLabel = capturedRef; }
));
ExecuteDimensionReassignments(reassignPairs, item.OriginalValue, item.NewValue, out dimCount);
```

### Polish #3 — family loop denominator unified
**Before:**
```csharp
int familyIndex = 0;
foreach (var kvp in byFamily)
{
    familyIndex++;
    double percent = (double)familyIndex / byFamily.Count * 100;  // BUG: separate denominator
```
**After:**
```csharp
foreach (var kvp in byFamily)
{
    current += kvp.Value.Count;
    double percent = (double)current / total * 100;  // shared denominator with standard loop
```

## Polish-Fix Scorecard

| Fix | Helper | Production Site | Tests | Status |
|-----|--------|----------------|-------|--------|
| #1 count-on-rollback | AccumulateCommittedFamilyCount | :222 (post-commit) | 2 GREEN | ✅ |
| #2 null-clear dim label | ExecuteDimensionReassignments (pairs) | :347-363 (pair-action) | 1 GREEN + 9 existing | ✅ |
| #3 progress capping | BuildProgressSequence | :171-175 (denominator) | 2 GREEN | ✅ |

## Files Created/Modified

- `src/Services/Renaming/BatchRenameExecutionService.cs` — 3 new helpers, 3 production rewires, in-code comments anchoring each polish to 05-03-PLAN
- `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs` — 5 new GREEN tests (2 for #1, 1 for #2, 2 for #3); all 3 Skip attributes removed
- `.planning/phases/05-verification-and-polish/05-VERIFICATION.md` — matrix row 6 updated to ✅ (all 6 rows now ✅)

## Decisions Made

- `AccumulateCommittedFamilyCount` is a 1-line expression method (3 LOC) — extracted specifically to make the count-after-commit contract testable without Revit API
- Pair-action overload added alongside (not replacing) single-action overload — 9 existing REQ-03 tests stay GREEN (backward-compatible)
- Family loop uses Option A (minimal: direct current/total arithmetic) instead of Option B (helper-indexed); helper exists for unit-test verifiability of the arithmetic contract, not production call path

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All 6 matrix rows ✅; suite at 175 GREEN (target ≥ 165)
- Plan 05-04 (manual Revit verification wave) is the final plan in Phase 5
- REQ-05 will be flipped to Complete after 05-04 manual verification sign-off

---
*Phase: 05-verification-and-polish*
*Completed: 2026-05-10*
