---
phase: 04-advanced-renaming-logic
plan: "00"
subsystem: test-scaffolds
tags: [wave-0, tdd, renaming, REQ-02, REQ-03, REQ-04]
dependency_graph:
  requires: []
  provides: [wave-0-red-fixtures, IsRenameable-property, validation-map]
  affects: [04-01-PLAN, 04-02-PLAN, 04-03-PLAN, 04-04-PLAN]
tech_stack:
  added: []
  patterns: [skip-gated-RED-tests, anchor-per-fixture]
key_files:
  created:
    - LECG.Tests/Services/RenameSkipDetectorTests.cs
    - LECG.Tests/Services/FormulaUpdateServiceTests.cs
    - LECG.Tests/Services/BatchRenameSafeRenameTests.cs
  modified:
    - src/ViewModels/Components/ElementRowViewModel.cs
    - LECG.Tests/Services/SearchReplacePreviewServiceTests.cs
    - .planning/phases/04-advanced-renaming-logic/04-VALIDATION.md
decisions:
  - "[Phase 04-00]: Wave 0 skip-gated RED pattern reused from Phase 03-00 — one anchor test per fixture, all behavioural tests skip-gated naming the implementing plan ID"
  - "[Phase 04-00]: IsRenameable is a plain auto-property (NOT [ObservableProperty]) — follows Phase 03 decision that only IsChecked is observable"
  - "[Phase 04-00]: FormulaUpdateServiceTests and BatchRenameSafeRenameTests are separate fixtures — FormulaUpdate covers wiring (04-03), BatchRenameSafeRename covers execution paths (04-03 REQ-02 + 04-04 REQ-03 + 04-01 REQ-04)"
metrics:
  duration: "~3min"
  completed_date: "2026-05-10"
  tasks_completed: 5
  files_changed: 6
---

# Phase 4 Plan 0: Wave 0 Test Scaffolds Summary

Wave 0 skip-gated RED test scaffolds for REQ-02/03/04 + IsRenameable ViewModel property — establishes the Nyquist-compliant feedback loop that every Phase 4 implementing plan flips from RED to GREEN.

---

## What Was Done

### Task 1 — IsRenameable Property
Added `public bool IsRenameable { get; set; } = true;` to `ElementRowViewModel` adjacent to the `Status` property. Plain auto-property (not `[ObservableProperty]`), default `true`. XML doc comment explains WPF checkbox IsEnabled / muted-row Style trigger usage.

File: `src/ViewModels/Components/ElementRowViewModel.cs`
Commit: `8798347`

### Task 2 — RenameSkipDetectorTests.cs (new fixture)
Created Wave 0 RED fixture with 1 anchor + 11 skip-gated tests:
- 6 tests for narrowed `GetRenameSkipReason` (REQ-02/03/04) — Skip: "Implement in plan 04-01"
- 5 tests for new `GetStandardItemSkipReason` (REQ-04) — Skip: "Implement in plan 04-01"

File: `LECG.Tests/Services/RenameSkipDetectorTests.cs`
Commit: `e0a4fec`
Flipped by: plan 04-01

### Task 3 — FormulaUpdateServiceTests.cs + BatchRenameSafeRenameTests.cs (new fixtures)
Created two Wave 0 RED fixtures:

**FormulaUpdateServiceTests**: 1 anchor + 3 skip-gated tests for IFormulaUpdateService wiring
- Skip: "Implement in plan 04-03"

**BatchRenameSafeRenameTests**: 1 anchor + 9 skip-gated tests:
- 4 REQ-02 tests — Skip: "Implement in plan 04-03" (formula-referenced rename + cross-batch collision)
- 3 REQ-03 tests — Skip: "Implement in plan 04-04" (dimension-label rename + stale-ref pitfall)
- 2 REQ-04 tests — Skip: "Implement in plan 04-01" (pre-flight dry-run)

Files: `LECG.Tests/Services/FormulaUpdateServiceTests.cs`, `LECG.Tests/Services/BatchRenameSafeRenameTests.cs`
Commit: `7c346f5`
Flipped by: plans 04-01, 04-03, 04-04

### Task 4 — SearchReplacePreviewServiceTests.cs (extended)
Appended 5 skip-gated RED tests for REQ-04 IsRenameable propagation:
- `ProcessPreview_FamilyParameterSkipReason_PopulatesStatus_WithReasonString`
- `ProcessPreview_FamilyParameterSkipReason_SetsIsRenameableFalse_AndIsCheckedFalse`
- `ProcessPreview_SafeRenameRow_PopulatesStatus_WithSideEffectCount`
- `ProcessPreview_RenameableRow_LeavesIsRenameableTrue_AndKeepsUserCheckedState`
- `ProcessPreview_CrossBatchNameCollision_FlipsSecondRowToSkip_WithReason` (Pitfall 5)

All Skip: "Implement in plan 04-02". Pre-existing 3 tests remain GREEN (no modifications).

File: `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs`
Commit: `e4efa5b`
Flipped by: plan 04-02

### Task 5 — VALIDATION.md updated
- Frontmatter: `status: ready`, `nyquist_compliant: true`, `wave_0_complete: true`
- Per-task verification map: concrete rows for plans 04-01..04-05 (no TBD rows)
- Wave 0 Requirements: all 4 items checked
- Validation Sign-Off: all 6 boxes checked
- Approval: approved 2026-05-10 — Wave 0 complete

File: `.planning/phases/04-advanced-renaming-logic/04-VALIDATION.md`
Commit: `21d6d83`

---

## Final Verification

Build command: `dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true --nologo -v minimal`
Build result: **0 errors, 0 warnings**

Test suite: `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true`
Result: **103 Passed, 28 Skipped, 0 Failed** (was 100/0/0 before plan)

New test breakdown:
- 3 new anchor tests (GREEN)
- 28 new skip-gated RED stubs (Skipped with plan ID in reason)

---

## Deviations from Plan

None — plan executed exactly as written.

---

## Self-Check: PASSED

Files created/modified:
- `src/ViewModels/Components/ElementRowViewModel.cs` — FOUND
- `LECG.Tests/Services/RenameSkipDetectorTests.cs` — FOUND
- `LECG.Tests/Services/FormulaUpdateServiceTests.cs` — FOUND
- `LECG.Tests/Services/BatchRenameSafeRenameTests.cs` — FOUND
- `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — FOUND
- `.planning/phases/04-advanced-renaming-logic/04-VALIDATION.md` — FOUND

Commits:
- `8798347` — feat(04-00): add IsRenameable property
- `e0a4fec` — test(04-00): RenameSkipDetectorTests
- `7c346f5` — test(04-00): FormulaUpdateServiceTests + BatchRenameSafeRenameTests
- `e4efa5b` — test(04-00): extend SearchReplacePreviewServiceTests
- `21d6d83` — docs(04-00): VALIDATION.md wave 0 complete
