---
status: complete
phase: 02-formula-auto-grouping-bug-fix
source:
  - 02-01-SUMMARY.md
  - 02-02-SUMMARY.md
started: 2026-05-09T04:35:00Z
updated: 2026-05-09T04:38:00Z
outcome: 1-passed-4-skipped
---

## Current Test

[testing complete]

## Tests

### 1. xUnit test suite green (FormulaGrouping + full suite)
expected: dotnet test runs FormulaAutoGroupingCommandTests + entire LECG.Tests suite without failures.
result: pass
evidence: |
  Category=FormulaGrouping filter: 4 passed, 0 failed (Duration 2ms)
  Full suite: 79 passed, 0 failed (Duration 107ms)
  Run timestamp: 2026-05-09T04:38:00Z

### 2. FormulaAutoGrouping moves formula-bearing parameters to "Other" group
expected: Run FormulaAutoGrouping command in Revit on a family with formula-bearing parameters; parameters move to "Other" group; LogView shows no [SKIP] messages where the move should have succeeded.
result: skipped
reason: User opted to trust theory + code review + green xUnit suite

### 3. Cross-referenced formulas restored after ReplaceParameter
expected: Family with parameter A whose formula references parameter B. Run FormulaAutoGrouping to move B to "Other". After completion, A's formula still references B and evaluates correctly (clear-replace-restore sequence in TryReplaceSharedParameterGroup).
result: skipped
reason: User opted to trust theory + code review

### 4. SubTransaction loop continues past unsupported params
expected: Batch including a parameter that cannot be moved (e.g., type-driving) — that single param logs a [SKIP] reason and the rest of the batch still moves successfully. No all-or-nothing rollback.
result: skipped
reason: User opted to trust theory + code review

### 5. EnsureCurrentType guard prevents SetFormula crash on typeless family
expected: Run FormulaAutoGrouping on a family with no current type set. SetFormula no longer throws; EnsureCurrentType activates a type before the call.
result: skipped
reason: User opted to trust theory + code review

## Summary

total: 5
passed: 1
issues: 0
pending: 0
skipped: 4

## Gaps

None recorded. Trust basis:
- Test #1 verified by direct dotnet test execution (4/4 FormulaGrouping + 79/79 full suite green)
- Tests #2-5: implementation reviewed in 02-01-SUMMARY / 02-02-SUMMARY; all four structural fixes (SubTransaction loop, EnsureCurrentType, removal of in-transaction group check, EnsureParametersPersistInGroup post-reload only) plus clear-replace-restore sequence are present in src/Commands/FormulaAutoGroupingCommand.cs per SUMMARYs

Risk: Real Revit behavior for formula round-trip and SubTransaction rollback semantics on FamilyManager.SetFormula was not exercised in a live document. First production use is the real test.
