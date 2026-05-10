---
phase: 05-verification-and-polish
plan: 02
subsystem: testing

# Dependency graph
requires:
  - phase: 05-01
    provides: Wave 1 TDD — RenameRulePipelineService + BaseElementCollectionService helpers GREEN
provides:
  - 18 direct-coverage GREEN tests for BatchRenameExecutionService pure-data helpers
  - 4 GREEN tests for SearchReplaceService facade (GetUniqueCategories, ProcessPreview, ctor null-guards)
  - Constructor null-guards added to SearchReplaceService
  - Internal test fakes (SearchReplaceFakes.cs) in LECG project for Revit-API-bound interface delegation
  - RevitAPI compile-only reference in LECG.Tests.csproj
  - Matrix rows 3 and 6 updated in 05-VERIFICATION.md

affects:
  - 05-03-PLAN.md (Wave 3 polish helpers — 4 remaining BatchRenameExecution skip-gated rows)
  - 05-04-PLAN.md (manual Revit session verification)

tech-stack:
  added:
    - LECG.Services.TestHelpers namespace (internal test fakes in production project)
    - RevitAPI.dll reference in LECG.Tests.csproj (compile-time only, Private=false)
  patterns:
    - FakeXxx concrete test doubles in LECG production project (via InternalsVisibleTo) for Revit-API-bound interfaces — avoids Castle DynamicProxy RevitAPI probe issue
    - Reflection-based constructor null-guard test for services that take Revit-API-bound ctor args
    - Document-parametered facade tests skip-gated with manual Revit verification pointer

key-files:
  created:
    - LECG.Tests/Services/BatchRenameExecutionServiceTests.cs (replaced skeleton with 18 GREEN + 4 Wave-3 skip-gated)
    - src/Services/Renaming/TestHelpers/SearchReplaceFakes.cs (FakeBaseElementCollectionService, FakeSearchReplacePreviewService, FakeBatchRenameExecutionService)
  modified:
    - LECG.Tests/Services/SearchReplaceServiceTests.cs (replaced skeleton with GREEN delegation + ctor null-guard tests)
    - src/Services/Renaming/SearchReplaceService.cs (added ArgumentNullException null-guards for all 3 ctor deps)
    - LECG.Tests/LECG.Tests.csproj (added RevitAPI compile-only reference)
    - .planning/phases/05-verification-and-polish/05-VERIFICATION.md (matrix rows 3 and 6 updated)

key-decisions:
  - "RevitAPI.dll has native-code dependencies only available inside Revit — tests calling methods with Document parameter cannot run in xUnit runner; those 3 facade delegation tests (CollectBaseElements, ExecuteBatchRename x2) skip-gated with manual Revit verification pointer"
  - "FakeXxx test doubles defined in LECG production project (src/Services/Renaming/TestHelpers/) via InternalsVisibleTo — only way to implement Revit-API-bound interfaces without RevitAPI.dll in test project"
  - "Constructor null-guard test for BatchRenameExecutionService uses ctor.Invoke+TargetInvocationException unwrap pattern (Phase 04-03 reflection pattern extended) because ITransactionService/IFamilyLoadOptionsFactory cannot be NSubstituted without RevitAPI.dll"
  - "SearchReplaceService ctor null-guards added as deviation Rule 2 (missing critical functionality) — facade was a v1.2 deletion candidate but Phase 5 acceptance requires SearchReplaceServiceTests row green"

requirements-completed:
  - REQ-05

duration: 35min
completed: 2026-05-10
---

# Phase 05 Plan 02: Wave 2 TDD — BatchRenameExecutionService + SearchReplaceService GREEN Summary

**21 new GREEN tests: 18 direct-coverage for BatchRenameExecutionService pure-data helpers + 4 SearchReplaceService facade/ctor tests; SearchReplaceService constructor null-guards added; suite 170 GREEN / 9 Skipped / 0 Failed**

## Performance

- **Duration:** 35 min
- **Started:** 2026-05-10T21:54:23Z
- **Completed:** 2026-05-10T22:29:00Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- BatchRenameExecutionServiceTests: 19 GREEN tests — EvaluateFamilyParamSkipReason (4), EvaluateStandardItemSkipReason (6), FormatSafeRenameLog (4), GroupCheckedFamilyParameterItemsForTest (2), LegacyProgressReporter null-callback (1 fact/4 methods), Constructor null-guard via reflection (1). 4 Wave-3 polish rows remain Skipped naming plan 05-03.
- SearchReplaceServiceTests: 4 GREEN tests — GetUniqueCategories delegation, ProcessPreview + CancellationToken passthrough, Constructor null-guards (3 assertions). 3 Document-parametered delegation tests skip-gated (RevitAPI native constraint).
- SearchReplaceService: added ArgumentNullException null-guards for all 3 constructor dependencies (Rule 2 auto-fix).
- SearchReplaceFakes.cs: internal test fakes in LECG production project implementing Revit-API-bound interfaces via InternalsVisibleTo — enables delegation testing without Castle DynamicProxy/RevitAPI.dll.
- Matrix rows 3 ✅ and 6 "direct ✅ (Wave 2), polish pending Wave 3" updated in 05-VERIFICATION.md.

## Task Commits

1. **Task 1: BatchRenameExecutionServiceTests direct-coverage GREEN** - `f10470d` (feat)
2. **Task 2: SearchReplaceServiceTests facade delegation GREEN + ctor null-guards** - `315f3cc` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs` - 19 GREEN direct-coverage tests (Wave 2 rows un-skipped); 4 Wave-3 rows stay Skipped
- `LECG.Tests/Services/SearchReplaceServiceTests.cs` - 4 GREEN delegation/ctor tests; 3 Document-param tests skip-gated
- `src/Services/Renaming/SearchReplaceService.cs` - ArgumentNullException null-guards for all 3 ctor deps
- `src/Services/Renaming/TestHelpers/SearchReplaceFakes.cs` - Internal FakeBaseElementCollectionService / FakeSearchReplacePreviewService / FakeBatchRenameExecutionService
- `LECG.Tests/LECG.Tests.csproj` - RevitAPI compile-only reference (Private=false)
- `.planning/phases/05-verification-and-polish/05-VERIFICATION.md` - Matrix rows 3 and 6 updated

## Decisions Made

- RevitAPI.dll has native-code dependencies only available inside Revit — any method call with a `Document` parameter fails in the xUnit runner with `FileNotFoundException`. The 3 Document-parametered facade delegation tests (CollectBaseElements, ExecuteBatchRename IProgressReporter, ExecuteBatchRename Action) are skip-gated with a manual Revit verification pointer in 05-VERIFICATION.md. This is consistent with the existing pattern for Revit-bound behaviors throughout the project.
- FakeXxx test doubles placed in `src/Services/Renaming/TestHelpers/SearchReplaceFakes.cs` (internal, InternalsVisibleTo LECG.Tests) — the only way to implement `IBaseElementCollectionService` / `ISearchReplacePreviewService` / `IBatchRenameExecutionService` from test project without a direct RevitAPI.dll reference in LECG.Tests.
- Constructor null-guard test for BatchRenameExecutionService uses `ctor.Invoke()` + `TargetInvocationException.InnerException` unwrap pattern (extending Phase 04-03 reflection decision) — passing null for all 3 ctor args triggers ArgumentNullException on formulaUpdateService (last parameter, confirmed guard), passing through the null-guard on the first two Revit-API-bound args (no guards there, and null doesn't dereference early).
- SearchReplaceService constructor null-guards added as Rule 2 auto-fix (missing critical functionality) — guard was absent despite all dependencies being required for correct facade operation.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added null-guards to SearchReplaceService constructor**
- **Found during:** Task 2 (SearchReplaceServiceTests facade delegation)
- **Issue:** SearchReplaceService constructor assigned all 3 deps without null-checks — null dep would produce NullReferenceException deep in a Revit operation, not at construction time
- **Fix:** Added `?? throw new ArgumentNullException(nameof(...))` for all 3 ctor parameters
- **Files modified:** src/Services/Renaming/SearchReplaceService.cs
- **Verification:** Constructor null-guard tests GREEN; ctor null-guards assert correct parameter names
- **Committed in:** 315f3cc (Task 2 commit)

**2. [Rule 3 - Blocking] Created SearchReplaceFakes.cs in LECG project**
- **Found during:** Task 2 — could not implement Revit-API-bound interfaces in test project
- **Issue:** IBaseElementCollectionService / IBatchRenameExecutionService reference `Autodesk.Revit.DB.Document`; LECG.Tests has no direct RevitAPI.dll reference and Castle DynamicProxy can't proxy interfaces with RevitAPI method signatures
- **Fix:** Defined FakeXxx concrete classes in `src/Services/Renaming/TestHelpers/SearchReplaceFakes.cs` (internal, InternalsVisibleTo) and added RevitAPI compile-only reference to LECG.Tests.csproj
- **Files modified:** src/Services/Renaming/TestHelpers/SearchReplaceFakes.cs (created), LECG.Tests/LECG.Tests.csproj
- **Verification:** Compilation and test execution succeed
- **Committed in:** 315f3cc (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 missing critical, 1 blocking)
**Impact on plan:** Both auto-fixes required for correct test execution. Scope is contained — no new production logic added beyond the null-guards.

## Issues Encountered

- RevitAPI.dll has native-code (unmanaged) dependencies only resolvable inside the Revit process — even with the DLL present in the output directory, the runtime loader fails with `FileNotFoundException: The specified module could not be found` for native sub-dependencies. Tests calling methods with `Document` parameter are permanently constrained to skip-gated status with manual Revit verification pointer.

## Next Phase Readiness

- Wave 3 (plan 05-03): 4 BatchRenameExecutionServiceTests polish rows remain Skipped (AccumulateCommittedFamilyCount x2, ExecuteDimensionReassignments_PreClearsLabel, BatchRenameProgress_MixedBatch)
- Wave 4 (plan 05-04): Manual Revit session for the 3 Document-parametered facade delegation tests and the 3 polish fixes
- Suite at 170 GREEN / 9 Skipped / 0 Failed (target ≥ 165 at phase close)

---
*Phase: 05-verification-and-polish*
*Completed: 2026-05-10*
