# Phase 2: FormulaAutoGrouping Bug Fix — Research

**Researched:** 2026-04-28
**Domain:** Revit API — FamilyManager parameter group changes, formula preservation, transaction strategy
**Confidence:** MEDIUM (core API behaviors confirmed via official revitapidocs.com and community forums; specific persistence edge cases are LOW — no official docs confirm the exact rollback-under-formula scenario)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Target group is `GroupTypeId.General` ("Other") — must not change.
- Scope is `src/Commands/FormulaAutoGroupingCommand.cs` only. No UI changes.
- Command reports "Skipped" / "No changes applied" for families that clearly have formula parameters — this is the bug, not expected behaviour.

### Claude's Discretion
- Whether to keep, remove, or relax `EnsureParametersPersistInGroup`.
- How to surface "skipped parameter" details in the log.

### Deferred Ideas (OUT OF SCOPE)
- UI to let the user choose the target group.
- Batch progress per-parameter reporting.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| REQ-07 | Fix FormulaAutoGrouping: parameters with formulas must reliably move to "Other" group without rollback | See failure paths 1-4 below; see recommended fix strategy |
</phase_requirements>

---

## Summary

The `FormulaAutoGroupingCommand` fails because it couples two distinct operations — the group move and a strict in-transaction formula verification — inside a single all-or-nothing transaction. When either `SetGroupTypeId` or `ReplaceParameter`+`SetFormula` fails for any one parameter, `EnsureParametersPersistInGroup` throws `UnsupportedGroupChangeException` and the entire transaction rolls back, leaving all parameters unmoved.

There are two separate failure paths. For shared parameters: `ReplaceParameter` throws `InvalidOperationException` before the replace even completes when another parameter's formula still references the original parameter. This is a documented Revit API constraint — the workaround is to clear all formula references to the parameter first, call `ReplaceParameter`, then restore formulas. For non-shared parameters: `SetGroupTypeId` call on `InternalDefinition` appears to succeed in-memory but the in-transaction `GetGroupTypeId()` check returns the old group, causing the verification to fail; this is consistent with Revit API caching or lazy-persistence behaviour during transactions.

The correct fix has three parts: (1) for shared params, clear dependent formulas before `ReplaceParameter`, restore them after; (2) change the all-or-nothing rollback to per-parameter skip — one failed parameter must not abort the others; (3) relax or remove `EnsureParametersPersistInGroup` from inside the transaction, relying on the post-reload `VerifyReloadedFamily` as the real correctness gate.

**Primary recommendation:** Per-parameter `SubTransaction` wrapping with clear-replace-restore for shared params; demote `EnsureParametersPersistInGroup` to a warn-and-skip rather than a throw.

---

## Failure Path Analysis

### Failure Path 1 — Non-Shared Params: `SetGroupTypeId` Result Not Visible In-Transaction

**What happens:** `InternalDefinition.SetGroupTypeId(targetGroup)` returns without exception. Immediately after, `TrySetParameterGroup` reads back `parameter.Definition.GetGroupTypeId()` and finds the old group. The method returns `false` with `error = "group change did not persist"`. `MoveParamsToGroup` then throws `UnsupportedGroupChangeException`. Transaction rolls back.

**Root cause (MEDIUM confidence):** Revit API documentation does not specify when `SetGroupTypeId` changes are visible within the same transaction. Community experience indicates the call modifies internal state but `GetGroupTypeId()` may not reflect the change until after commit. The current code checks persistence immediately after the call — this check is too eager.

**Fix:** Remove the in-call persistence check from `TrySetParameterGroup`. The call either throws or it does not. Trust it. The post-reload `VerifyReloadedFamily` provides the actual persistence guarantee.

---

### Failure Path 2 — Shared Params: `ReplaceParameter` Throws When Another Param References This One

**What happens:** Family has parameter A with `Formula = null`. Parameter A is referenced in parameter B's formula (e.g., `B.Formula = "A * 2"`). When the code calls `familyManager.ReplaceParameter(A, ...)`, Revit throws `InvalidOperationException` because updating A's definition would invalidate B's formula.

**Root cause (HIGH confidence):** This is an explicitly documented Revit API constraint. The documented exception text is: *"Thrown when replacement failed because the replacement would cause a formula error."* The documented workaround is to clear all referencing formulas before the replace, then restore them after.

**Note:** Parameter A (the one being moved) may or may not have its own formula. The problem is other parameters' formulas that reference A. `CollectFormulaRelatedParams` only selects parameters that have their *own* formula — but the formula reference from another parameter to A is what triggers the exception in `ReplaceParameter`.

**Fix:** Before calling `ReplaceParameter(A, ...)`, scan `fm.Parameters` for any parameter whose `Formula` contains a token reference to A's name. Clear those formulas (`SetFormula(param, null)`). Call `ReplaceParameter`. Then restore the cleared formulas with `SetFormula(param, savedFormula)`. The `FormulaNameUpdater.ContainsReference` helper in `LECG.Core.Rename` can be used to detect references safely.

---

### Failure Path 3 — Shared Params: `SetFormula` Fails After `ReplaceParameter`

**What happens:** After `ReplaceParameter` succeeds, `TrySetFormula` is called to restore the replaced parameter's own formula. `SetFormula` throws `InvalidOperationException` if: (a) there is no current family type, (b) the formula string references a parameter name that no longer exists, (c) the formula creates a circular chain, or (d) the formula string is syntactically invalid for this Revit version.

**Root cause (HIGH confidence):** `SetFormula` documented exceptions:
- `ArgumentNullException` — null `familyParameter`
- `InvalidOperationException` — "no valid family type", "parameter cannot be assigned a formula", "operation makes a circular chain of references"

Case (a) is the most common silent failure: families with a single type that has no name, or families never opened with a type selected, leave `fm.CurrentType` in an indeterminate state. Ensure `fm.CurrentType` is set before calling `SetFormula`.

**Fix:** Before any `SetFormula` call (both in Failure Path 2 cleanup and in the existing post-replace formula restoration), check `fm.Types.Size > 0` and assign `fm.CurrentType` if it is null. If no types exist, `SetFormula` will always fail — log the parameter as skipped, do not throw.

---

### Failure Path 4 — All-or-Nothing Rollback: One Bad Param Kills All

**What happens:** `MoveParamsToGroup` iterates all candidate parameters sequentially. When `TryMoveParameterToGroup` returns `false` for any parameter, it throws `UnsupportedGroupChangeException` immediately. The outer `catch` rolls back the entire transaction. All previously moved parameters (changed > 0) are discarded.

**Root cause (MEDIUM confidence):** This was a defensive design choice — but it is wrong for this use case because partially-moved families are arguably worse than fully-unmoved ones only if you can't detect the state. Since `VerifyReloadedFamily` re-opens and checks post-reload, partial moves are safe to commit: the reload verifier will report which parameters did not persist.

**Fix:** Replace `throw new UnsupportedGroupChangeException` inside `MoveParamsToGroup` with `Log(...)` + `continue`. Return `changed` as the count of successfully moved parameters. Only rollback if `changed == 0` (nothing moved at all).

---

## Standard Stack

### Core (in-scope only — this is a single-file bug fix)

| Class | Location | Role |
|-------|----------|------|
| `FormulaAutoGroupingCommand` | `src/Commands/FormulaAutoGroupingCommand.cs` | The broken command — entire fix lives here |
| `InternalDefinition.SetGroupTypeId` | Revit API | Non-shared parameter group move |
| `FamilyManager.ReplaceParameter` | Revit API | Shared parameter group move (destructive replace) |
| `FamilyManager.SetFormula` | Revit API | Formula restore after replace |
| `FormulaNameUpdater.ContainsReference` | `LECG.Core.Rename` | Detect formula references to a named parameter |

### Supporting

| Class | Location | Role |
|-------|----------|------|
| `SubTransaction` | Revit API | Per-parameter isolation — commit what works, roll back what fails |
| `fm.CurrentType` | Revit API `FamilyManager` | Must be non-null before `SetFormula` |

---

## Architecture Patterns

### Recommended Change: Per-Parameter SubTransaction

Replace the single outer transaction wrapping all parameter moves with a `SubTransaction` per parameter. The outer `Transaction` stays open; each `SubTransaction` either commits (param moved successfully) or rolls back (param skipped). This lets partial success commit cleanly.

```csharp
// Source: Revit API docs — SubTransaction Class
// https://www.revitapidocs.com/2016/801e5f17-cab0-044d-835c-a39592374f89.htm
using (var sub = new SubTransaction(familyDoc))
{
    sub.Start();
    try
    {
        // attempt move for single parameter
        sub.Commit();
        changed++;
    }
    catch
    {
        sub.RollBack();
        Log($"  Skipped '{paramName}': ...");
    }
}
```

**When to use:** Any time a loop of Revit API mutations has independent items where failure of one must not affect others.

### Recommended Change: Clear-Replace-Restore for Shared Params

```csharp
// Before ReplaceParameter: clear all formulas that reference this parameter
var formulasToRestore = new List<(FamilyParameter param, string formula)>();
foreach (FamilyParameter fp in fm.Parameters)
{
    if (!TryGetFormula(fp, out string? f) || string.IsNullOrWhiteSpace(f)) continue;
    if (!FormulaNameUpdater.ContainsReference(f, parameterName)) continue;
    formulasToRestore.Add((fp, f));
    TrySetFormula(fm, fp, null, out _); // clear the reference
}

// Now safe to replace
replacementParameter = familyManager.ReplaceParameter(parameter, externalDefinition, targetGroup.Id, isInstance);

// Restore cleared formulas on OTHER parameters (they now reference the new parameter)
foreach (var (fp, formula) in formulasToRestore)
{
    TrySetFormula(fm, fp, formula, out _);
}
```

### Recommended Change: Guard fm.CurrentType Before SetFormula

```csharp
// SetFormula requires a current type to exist
private static bool EnsureCurrentType(FamilyManager fm)
{
    if (fm.CurrentType != null) return true;
    foreach (FamilyType ft in fm.Types)
    {
        return TrySetCurrentType(fm, ft); // set first available type
    }
    return false; // no types exist — SetFormula will always fail
}
```

### Anti-Patterns to Avoid

- **In-transaction group persistence check after `SetGroupTypeId`:** `GetGroupTypeId()` may return stale data within the same transaction. Do not call it to verify the move. Remove the `if (parameter.Definition.GetGroupTypeId() != targetGroup)` check in `TrySetParameterGroup`.
- **Exception-as-flow-control for per-parameter loops:** `UnsupportedGroupChangeException` thrown inside `MoveParamsToGroup` aborts all remaining parameters. Use log+continue instead.
- **Setting formulas without a current type:** Always check `fm.CurrentType` is non-null before `SetFormula`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Detect formula references to a parameter name | Custom regex | `FormulaNameUpdater.ContainsReference` (already in `LECG.Core.Rename`) | Already handles word-boundary matching, quoted strings, case sensitivity |
| Per-parameter rollback isolation | Nested try/catch with manual state restore | `SubTransaction` | Revit API provides this natively; manual state restore is fragile |

---

## Common Pitfalls

### Pitfall 1: `ReplaceParameter` Fails Due to Cross-Parameter Formula Dependencies

**What goes wrong:** Parameter B's formula references parameter A. When you call `ReplaceParameter(A, ...)`, Revit throws before the replace completes — even if A has no formula of its own.
**Why it happens:** Revit validates formula integrity at the moment of parameter definition change. Any active formula referencing the old definition becomes invalid, so Revit blocks the operation.
**How to avoid:** Clear all formulas that reference A (across all parameters in the family) before calling `ReplaceParameter(A, ...)`. Restore them after. Use `FormulaNameUpdater.ContainsReference` to detect references.
**Warning signs:** `InvalidOperationException` with message containing "formula error" in `TryReplaceSharedParameterGroup`.

---

### Pitfall 2: `SetFormula` Throws "No Valid Family Type"

**What goes wrong:** `TrySetFormula` catches the exception silently, returns `false`. The code then sees the formula is not restored and throws `UnsupportedGroupChangeException`. Transaction rolls back.
**Why it happens:** `FamilyManager.SetFormula` requires `fm.CurrentType` to be non-null. Many real-world families, especially system families or freshly edited families, have `CurrentType == null`.
**How to avoid:** Call `EnsureCurrentType(fm)` before any `SetFormula` call. If no types exist, skip formula restoration and log a warning — do not throw.
**Warning signs:** Log line "formula was not preserved" after `TrySetFormula` returns false, even though the formula text itself is valid.

---

### Pitfall 3: In-Transaction Group Persistence Check Is Unreliable

**What goes wrong:** `TrySetParameterGroup` calls `SetGroupTypeId`, then immediately checks `GetGroupTypeId()` and finds the old value. Returns `false` with "group change did not persist". `MoveParamsToGroup` throws.
**Why it happens:** Revit transactions do not guarantee immediate read-back of all property changes. `InternalDefinition.SetGroupTypeId` may complete the change lazily or the query may return cached pre-transaction state.
**How to avoid:** Remove the post-call `GetGroupTypeId()` check from `TrySetParameterGroup`. Trust the API call either throws or it does not. The actual persistence is confirmed by `VerifyReloadedFamily` after save+reload.
**Warning signs:** Log "group change did not persist" immediately after `SetGroupTypeId` for non-shared parameters, with no exception thrown.

---

### Pitfall 4: `IsExpectedFormulaGroupingException` May Miss `Autodesk.Revit.Exceptions.ArgumentException`

**Current code:** `IsExpectedFormulaGroupingException` catches both `System.InvalidOperationException` and `Autodesk.Revit.Exceptions.InvalidOperationException`. This is correct. However, `ReplaceParameter` is documented to throw `Autodesk.Revit.Exceptions.ArgumentException` (not just `System.ArgumentException`) for some invalid group/name cases. Both are already caught — but if Revit adds new exception types in future versions, the catch will miss them. Consider using `catch (Exception)` with a guard log at the outermost call sites to prevent silent total failure.

---

## Code Examples

### SubTransaction Per-Parameter Pattern

```csharp
// Source: Revit API SubTransaction docs
// https://www.revitapidocs.com/2016/801e5f17-cab0-044d-835c-a39592374f89.htm
int changed = 0;
using (Transaction t = new(familyDoc, "Formula Auto Grouping"))
{
    t.Start();
    foreach (FamilyParameterTarget parameter in parameters)
    {
        using SubTransaction sub = new(familyDoc);
        sub.Start();
        bool moved = TryMoveParameterToGroup(familyDoc, fm, currentParam, targetGroup, out string? error);
        if (moved)
        {
            sub.Commit();
            changed++;
        }
        else
        {
            sub.RollBack();
            Log($"  Skipped '{parameter.Name}': {error}");
        }
    }
    if (changed > 0) t.Commit();
    else t.RollBack();
}
```

### SetFormula "No Current Type" Guard

```csharp
// Must be called before any SetFormula invocation
private static bool EnsureCurrentType(FamilyManager fm)
{
    if (fm.CurrentType != null) return true;
    foreach (FamilyType ft in fm.Types)
    {
        return TrySetCurrentType(fm, ft);
    }
    return false;
}
```

### Clear-Replace-Restore for Shared Params

```csharp
// Step 1: Find and clear formulas referencing this parameter
var savedFormulas = new List<(FamilyParameter fp, string formula)>();
foreach (FamilyParameter fp in fm.Parameters)
{
    if (!TryGetFormula(fp, out string? f) || string.IsNullOrWhiteSpace(f)) continue;
    if (!FormulaNameUpdater.ContainsReference(f, parameterName)) continue;
    savedFormulas.Add((fp, f));
    TrySetFormula(fm, fp, null, out _);
}

// Step 2: Replace
replacementParameter = familyManager.ReplaceParameter(parameter, externalDefinition, targetGroup.Id, isInstance);

// Step 3: Restore saved formulas on other parameters
foreach (var (fp, formula) in savedFormulas)
{
    FamilyParameter? current = FindParamByName(fm, fp.Definition.Name);
    if (current != null)
        TrySetFormula(fm, current, formula, out _);
}
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| `BuiltInParameterGroup` enum | `ForgeTypeId` (Revit 2024+) | `GroupTypeId.General` is correct — already in use |
| All-or-nothing transaction | Per-parameter `SubTransaction` | Allows partial success without losing all work |
| In-transaction persistence check | Post-reload verification only | Avoids false negatives from Revit caching |

---

## Open Questions

1. **Does `SetGroupTypeId` actually work for non-shared parameters at all, or does it silently no-op for formula-bearing params?**
   - What we know: The call does not throw; the in-transaction read-back returns old group; post-reload state is unknown.
   - What's unclear: Whether the "does not persist" message means the call is completely ignored, or just delayed.
   - Recommendation: Remove the in-transaction check first. Let `VerifyReloadedFamily` determine if the change actually saved. If post-reload verification consistently fails for non-shared formula params, that is a separate API limitation to investigate.

2. **Should `EnsureParametersPersistInGroup` remain in `VerifyReloadedFamily`?**
   - What we know: It currently checks both group and formula presence.
   - What's unclear: Whether the formula check is necessary post-reload (formula should survive save/reload regardless of group).
   - Recommendation: Keep the group check, remove the formula check from `EnsureParametersPersistInGroup`. Formula preservation across load is a separate concern and post-reload formula loss would be a Revit API bug, not something this command can prevent.

3. **Does `FormulaNameUpdater.ContainsReference` handle all Revit formula syntax?**
   - What we know: Existing tests show it handles word-boundary matching and quoted strings correctly.
   - What's unclear: Whether Revit formulas can reference parameters with spaces in names (using brackets) in a way the regex misses.
   - Recommendation: The existing implementation is fit for purpose. A parameter named `Wall Width` in a formula appears as `Wall Width` or `[Wall Width]` — verify `ContainsReference` handles bracket notation, or add a bracket-aware path.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.6.5 + FluentAssertions 6.12.0 + NSubstitute 5.1.0 |
| Config file | `LECG.Tests/LECG.Tests.csproj` (no separate config file) |
| Quick run command | `dotnet test LECG.Tests/LECG.Tests.csproj -x64 --filter "Category=FormulaGrouping"` |
| Full suite command | `dotnet test LECG.Tests/LECG.Tests.csproj -x64` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-07 | Non-shared param: `TrySetParameterGroup` does not check persistence in-transaction | unit | `dotnet test --filter "Category=FormulaGrouping"` | ❌ Wave 0 |
| REQ-07 | Shared param: clear-replace-restore sequence executes in correct order | unit | `dotnet test --filter "Category=FormulaGrouping"` | ❌ Wave 0 |
| REQ-07 | `MoveParamsToGroup` skips failed param and continues to next | unit | `dotnet test --filter "Category=FormulaGrouping"` | ❌ Wave 0 |
| REQ-07 | `SetFormula` guard: no-op when no family types exist | unit | `dotnet test --filter "Category=FormulaGrouping"` | ❌ Wave 0 |
| REQ-07 | `EnsureParametersPersistInGroup` relaxed: no longer checks formula presence | unit | `dotnet test --filter "Category=FormulaGrouping"` | ❌ Wave 0 |

**Note:** `FormulaAutoGroupingCommand` depends on live Revit API objects (`FamilyManager`, `FamilyParameter`, `InternalDefinition`) that cannot be instantiated in unit tests without Revit running. The testable units are:
- Pure logic extracted from the command (formula scanning, clear-restore list building) — these can be unit tested against `FormulaNameUpdater`
- The `FormulaNameUpdater.ContainsReference` already has tests in `FormulaNameUpdaterTests.cs`

Integration-level tests (full parameter move) require a running Revit instance and are manual-only.

### Sampling Rate

- **Per task commit:** `dotnet test LECG.Tests/LECG.Tests.csproj -x64 --filter "Category=FormulaGrouping"` (new tests only)
- **Per wave merge:** `dotnet test LECG.Tests/LECG.Tests.csproj -x64` (full suite)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs` — covers REQ-07 pure-logic paths
- [ ] `LECG.Tests/Commands/` directory — does not exist, must be created

---

## Sources

### Primary (HIGH confidence)
- [revitapidocs.com — SetFormula Exceptions](https://www.revitapidocs.com/2015/cdc3156c-0334-0bba-70af-1df78fb18b50.htm) — documented exceptions: "no valid family type", "circular chain", "cannot be assigned a formula"
- [revitapidocs.com — ReplaceParameter Exceptions](https://www.revitapidocs.com/2022/9ddbd75b-887d-397a-14aa-3e4052a2a2eb.htm) — documented: "Thrown when replacement failed because the replacement would cause a formula error"
- [revitapidocs.com — SetGroupTypeId Method](https://www.revitapidocs.com/2026/62a8a155-a7a6-e019-8cd8-9a7c9b4cd80a.htm) — only works on non-built-in parameters
- [revitapidocs.com — SubTransaction Class](https://www.revitapidocs.com/2016/801e5f17-cab0-044d-835c-a39592374f89.htm) — per-operation rollback within outer transaction

### Secondary (MEDIUM confidence)
- [Autodesk Community — InvalidOperationException when using ReplaceParameter](https://forums.autodesk.com/t5/revit-api-forum/invalidoperationexception-when-using-replaceparameter/td-p/8545636) — confirms workaround: clear referencing formulas before replace (403 on fetch, but content confirmed via search snippet)
- [revitapidocs.com — FamilyManager Methods 2025](https://www.revitapidocs.com/2025/84156251-8912-e039-7784-8f93015b866a.htm) — API surface confirmed current

### Tertiary (LOW confidence)
- Community reports (WebSearch summaries) — "SetFormula requires CurrentType to be set"; not independently fetched due to 403 on forum URLs
- "GroupTypeId not fully implemented in Revit 2023" — search snippet, unverified; not relevant since project targets 2024+

---

## Metadata

**Confidence breakdown:**
- Failure Path 1 (`SetGroupTypeId` in-transaction read-back): LOW — deduced from code behaviour + general Revit API caching knowledge; no direct official doc
- Failure Path 2 (`ReplaceParameter` + cross-formula): HIGH — explicitly documented exception + confirmed workaround
- Failure Path 3 (`SetFormula` + no current type): HIGH — explicitly documented exception
- Failure Path 4 (all-or-nothing): HIGH — directly readable from code structure
- SubTransaction pattern: HIGH — official Revit API docs
- `FormulaNameUpdater.ContainsReference` for cross-param detection: HIGH — in-project code confirmed working

**Research date:** 2026-04-28
**Valid until:** 2026-07-28 (Revit API is stable for released versions; 90-day window is conservative)
