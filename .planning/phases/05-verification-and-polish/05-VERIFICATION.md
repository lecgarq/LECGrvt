---
phase: 05-verification-and-polish
status: scaffold
created: 2026-05-10
---

# Phase 5 — Verification

## Service -> Fixture Coverage Matrix

| # | Service | Fixture | Scenarios | Status |
|---|---------|---------|-----------|--------|
| 1 | FormulaUpdateService | FormulaUpdateServiceTests | delegation x 3; empty no-throw; ctor injection | ✅ |
| 2 | RenameRulePipelineService | RenameRulePipelineServiceTests | rule order; index propagation; null-throw guards; inactive-rules pass-through | ❌ -> Wave 1 (05-01) |
| 3 | SearchReplaceService | SearchReplaceServiceTests | 4-method delegation; ctor null-guards | ❌ -> Wave 2 (05-02) |
| 4 | SearchReplacePreviewService | SearchReplacePreviewServiceTests | scope/name filter; status; side-effect count; collision (8 tests) | ✅ |
| 5 | BaseElementCollectionService | BaseElementCollectionServiceTests | no-blanks invariant (4 GREEN); TryGetGroupLabel; DispatchScopeFlags; MergeParamScanResults; Revit branches skip-gated | ⚠ -> Wave 1 (05-01) |
| 6 | BatchRenameExecutionService | BatchRenameExecutionServiceTests (new) + BatchRenameSafeRenameTests (existing) | 4-branch EvaluateFamilyParamSkipReason; 6-branch EvaluateStandardItemSkipReason; FormatSafeRenameLog 4-branch; GroupCheckedFamilyParameterItems; LegacyProgressReporter null-callback; ctor null-guards; + 3 polish helpers | ❌ -> Wave 2 (05-02) + Wave 3 (05-03) |

Acceptance: every row ✅ AND full suite 100% GREEN.

## Manual Revit Verification Checklist (Wave 4 — 05-04)

Status legend: ⬜ pending · ✅ pass · ❌ fail · 🚫 N/A

### Polish #1 — count-on-rollback (`BatchRenameExecutionService.cs:222`)
- [ ] Force a family-edit rollback path (e.g., trigger concurrent edit / refuse-commit scenario)
- [ ] Confirm reported success count = 0 in completion message and LogView

### Polish #2 — Dimension.FamilyLabel null-clear
- [ ] Choose a family with a dimension whose `FamilyLabel.Definition.Name` already binds to a parameter
- [ ] Rename that parameter via Batch Rename -> Family Parameters
- [ ] Confirm the dimension reassignment commits cleanly (no "cannot overwrite" warning from Revit)

### Polish #3 — Standard-item progress capping
- [ ] Run a mixed batch: 5+ Sheets or Materials + 3+ family parameters across >= 2 families
- [ ] Watch the progress bar — confirm it reaches 100% (monotonic 0->100, no mid-flight cap below 100)

## Sign-off (Wave 4)

- [ ] Full xUnit suite GREEN (target >= ~165 tests)
- [ ] Matrix above: all rows ✅
- [ ] Manual checklist: all 3 polish fixes PASS or trust-based sign-off recorded
- [ ] REQ-05 flipped Pending -> Complete in `.planning/REQUIREMENTS.md`
