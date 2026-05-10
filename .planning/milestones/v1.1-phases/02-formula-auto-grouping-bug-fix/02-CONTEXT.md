# Phase 2: FormulaAutoGrouping Bug Fix — Context

**Gathered:** 2026-04-28
**Status:** Ready for planning
**Source:** User description + code review

<domain>
## Phase Boundary

Fix `FormulaAutoGroupingCommand` so that family parameters that have a formula are reliably moved to the "Other" parameter group (`GroupTypeId.General`). Currently the command constantly rolls back or silently marks families as unsupported without actually moving the parameters.

Scope: `src/Commands/FormulaAutoGroupingCommand.cs` only. No UI changes.

</domain>

<decisions>
## Implementation Decisions

### Target Group
- Target is `GroupTypeId.General` = "Other" — this is correct and must not change.

### What "failing" means
- The command logs "Skipped" or "No changes applied" for families where parameters clearly have formulas.
- Internally, `EnsureParametersPersistInGroup` throws `UnsupportedGroupChangeException`, causing rollback.
- The user sees 0 parameters moved even though the family has formula parameters.

### Known failure paths to investigate
1. Non-shared parameters: `SetGroupTypeId()` appears to work in-transaction but `EnsureParametersPersistInGroup` finds the group reverted — possible Revit API caching issue.
2. Shared parameters: `ReplaceParameter()` drops formula; `TrySetFormula()` may fail for formulas that reference other parameters in circular or ordered-dependency scenarios, causing `MoveParamsToGroup` to throw before any commit.
3. `EnsureParametersPersistInGroup` formula check: throwing if formula is empty/null after move even if the move itself succeeded — too strict.
4. All-or-nothing behaviour: one bad parameter rolls back ALL parameters in the family.

### Fix strategy (Claude's direction, user wants it fixed for good)
- Diagnose the exact exception/log message from the failure path.
- For shared params: implement formula restoration robustly, or skip formula verification inside the transaction and rely only on the post-reload `VerifyReloadedFamily` check.
- For non-shared params: if `SetGroupTypeId()` doesn't persist, evaluate alternative approaches (add/remove trick, or skip those params with a clear log message instead of rolling back all).
- Replace all-or-nothing rollback with per-parameter skip: a parameter that can't be moved is logged and skipped; other parameters still commit.
- The post-reload `VerifyReloadedFamily` already covers the final correctness guarantee — the in-transaction double-check can be relaxed.

### Claude's Discretion
- Whether to keep or remove `EnsureParametersPersistInGroup` entirely vs. relax it.
- How to surface "skipped parameter" details in the log.

</decisions>

<specifics>
## Specific References

- Command file: `src/Commands/FormulaAutoGroupingCommand.cs`
- Key method: `MoveParamsToGroup` (line ~485), `EnsureParametersPersistInGroup` (line ~521), `TryReplaceSharedParameterGroup` (line ~663), `TrySetParameterGroup` (line ~605)
- Current failure path: `UnsupportedGroupChangeException` from `EnsureParametersPersistInGroup` → rollback of entire transaction
- `GroupTypeId.General` is correct for "Other"

</specifics>

<deferred>
## Deferred Ideas

- Adding UI to let the user choose the target group (hardcoded to "Other" for now — keep it that way)
- Batch progress per-parameter reporting

</deferred>

---

*Phase: 02-formula-auto-grouping-bug-fix*
*Context gathered: 2026-04-28 from user description*
