---
phase: 05-verification-and-polish
plan: 01
subsystem: Testing / Renaming Services
tags: [tdd, unit-tests, refactor, pure-helpers, wave-1]
dependency_graph:
  requires: [05-00]
  provides: [RenameRulePipelineServiceTests GREEN, BaseElementCollectionService pure helpers, matrix rows 2+5 flipped]
  affects: [BaseElementCollectionService.cs, RenameRulePipelineServiceTests.cs, BaseElementCollectionServiceTests.cs]
tech_stack:
  added: [ScopeMask (Flags enum)]
  patterns: [internal static pure-data helpers, Func<string> delegate for testable try/catch extraction]
key_files:
  created: []
  modified:
    - LECG.Tests/Services/RenameRulePipelineServiceTests.cs
    - LECG.Tests/Services/BaseElementCollectionServiceTests.cs
    - src/Services/Renaming/BaseElementCollectionService.cs
    - .planning/phases/05-verification-and-polish/05-VERIFICATION.md
decisions:
  - TryGetGroupLabel uses Func<string> (not Func<ForgeTypeId> + Func<ForgeTypeId,string>) — single-delegate pattern keeps ForgeTypeId out of test signatures and is equally testable; both exception paths covered by throw-from-lambda
  - DispatchScopeFlags added as pure helper alongside (not replacing) production scope dispatch — production if-chain unchanged to avoid invasive refactor risk; helper mirrors the dispatch logic for direct testability
  - RenameRulePipelineServiceTests use concrete rule objects (not mocks) — service reads rules from RenameRuleContext, not constructor args; concrete NumberingRule/RemoveRule etc. produce deterministic outputs testable without NSubstitute
  - Constructor_NullRuleList_Throws renamed to ApplyRules_NullText_Throws — production service has no constructor args; null-text guard (ArgumentNullException.ThrowIfNull) is the equivalent correctness contract
metrics:
  duration: 5min
  completed: 2026-05-10
  tasks_completed: 2
  files_modified: 4
---

# Phase 5 Plan 01: Wave 1 TDD — Pipeline Coverage + BaseElementCollectionService Helpers Summary

One-liner: 5 GREEN pipeline tests + 3 pure-data helpers (TryGetGroupLabel/DispatchScopeFlags/MergeParamScanResults) extracted from BaseElementCollectionService enabling 10 new GREEN unit tests; matrix rows 2 and 5 flipped to ✅.

## What Was Built

### Task 1 — RenameRulePipelineServiceTests GREEN body (5 tests)

Un-skipped all 5 Wave-0 RED rows and wrote GREEN bodies covering:

- `ApplyRules_ComposesRulesInOrder`: concrete rule chain (Remove→Replace→Case→Add→Numbering) with observable, order-dependent transformations ("hello world" → "EARTH-x-1")
- `ApplyRules_PassesIndexArgumentToEveryRule`: NumberingRule with index=3, StartAt=1, Increment=1 produces suffix "-4" — only correct if index propagates
- `ApplyRules_NullContext_Throws`: ArgumentNullException guard on context param
- `ApplyRules_InactiveRules_PassesThroughUnchanged`: all rules IsActive=false; text returned verbatim
- `ApplyRules_NullText_Throws`: ArgumentNullException.ThrowIfNull(text) guard

**Result:** 6/6 GREEN (anchor + 5 behavioural), 0 Failed.

### Task 2 — 3 pure helpers + 10 new GREEN tests + matrix update

**Production changes (`BaseElementCollectionService.cs`):**

1. `ScopeMask` — `[Flags] internal enum` with 9 bits (Types/Families/Views/Sheets/Materials/ObjectStyles/LineStyles/FillPatterns/FamilyParameters)

2. `TryGetGroupLabel(Func<string> resolve)` (~10 LOC) — wraps single delegate in try/catch; returns "" on any exception. Replaces two inline try/catch blocks at Phase A and Phase B param scan sites.

3. `DispatchScopeFlags(bool types, ..., bool familyParameters) → ScopeMask` (~15 LOC) — pure flag→mask mapping; no Revit API dependency. Added alongside production scope dispatch (not replacing it) to avoid invasive refactor risk.

4. `MergeParamScanResults(IReadOnlyList<ElementData> scanA, IReadOnlyList<ElementData> scanB) → List<ElementData>` (~22 LOC) — deduplicates by (Id, Name) using Dictionary<long, HashSet<string>>; scanA rows take precedence.

**Test changes (`BaseElementCollectionServiceTests.cs`):**

- TryGetGroupLabel x3: resolve succeeds → returns label; resolve throws (GetId path) → ""; resolve throws (GetLabel path) → ""
- DispatchScopeFlags x5: Types-only, Families-only, Materials-only, FamilyParameters-only, multi-scope (Types+Families+Materials)
- MergeParamScanResults x2: dedup by (familyId, paramName) + empty inputs → empty output

**2 Revit-bound tests remain skip-gated** with reason "Revit FilteredElementCollector path — covered by manual Revit verification in 05-VERIFICATION.md".

**05-VERIFICATION.md:** Row 2 (RenameRulePipelineService) and Row 5 (BaseElementCollectionService) both flipped to ✅.

**Full suite result:** 149 GREEN, 30 Skipped, 0 Failed (was 134 GREEN, 45 Skipped before Wave 1).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] TryGetGroupLabel signature simplified to Func<string>**
- **Found during:** Task 2
- **Issue:** Plan specified `Func<ForgeTypeId> getId, Func<ForgeTypeId, string> getLabel` — but `ForgeTypeId` (Autodesk.Revit.DB) is not accessible in the test project's transitive references; the `using Autodesk.Revit.DB` import caused CS0246 build error in tests.
- **Fix:** Changed to `Func<string> resolve` — single delegate covers both getId() and getLabel() steps; test lambdas use `() => "label"` for success and `() => throw ...` for failure paths. Both exception-catching scenarios remain testable.
- **Files modified:** `src/Services/Renaming/BaseElementCollectionService.cs` (helper signature + 2 call sites)
- **Commit:** 86a448e

**2. [Rule 2 - Adaptation] Constructor_NullRuleList_Throws → ApplyRules_NullText_Throws**
- **Found during:** Task 1
- **Issue:** `RenameRulePipelineService` has no constructor args — rules come from `RenameRuleContext`. "Constructor null rule-list" guard does not apply.
- **Fix:** Renamed test to `ApplyRules_NullText_Throws` covering the `ArgumentNullException.ThrowIfNull(text)` guard — equivalent correctness contract that does exist in production.
- **Commit:** 8ebc3d4

## Commits

| Task | Commit | Message |
|------|--------|---------|
| 1 | 8ebc3d4 | test(05-01): implement RenameRulePipelineServiceTests GREEN body (5 tests) |
| 2 | 86a448e | feat(05-01): extract 3 pure helpers + flip BaseElementCollectionServiceTests RED rows GREEN |

## Self-Check: PASSED

- FOUND: LECG.Tests/Services/RenameRulePipelineServiceTests.cs
- FOUND: LECG.Tests/Services/BaseElementCollectionServiceTests.cs
- FOUND: src/Services/Renaming/BaseElementCollectionService.cs
- FOUND: .planning/phases/05-verification-and-polish/05-VERIFICATION.md
- FOUND commit 8ebc3d4 (Task 1)
- FOUND commit 86a448e (Task 2)
- Full suite: 149 GREEN, 30 Skipped, 0 Failed
