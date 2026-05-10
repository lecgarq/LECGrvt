# Phase 4: Advanced Renaming Logic — Research

**Researched:** 2026-05-10
**Domain:** Revit FamilyParameter renaming, formula reference rewriting, dimension-label reassignment, WPF row state (skip/mute), pre-flight validation pattern
**Confidence:** HIGH (based on direct source-code inspection; all claims derived from actual files in this repo)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **Always-on Safe Rename.** No toggle. Formula-referenced and dimension-label parameters are renamed + references updated by default. The current skip path for these categories is removed.
- **Safe Rename scope:** Formula-referenced (REQ-02) via `FormulaNameUpdater.UpdateFormula`; Dimension-label (REQ-03) via `Dimension.FamilyLabel` reassignment; Element-associated flipped from skip to allowed (Revit re-resolves by ParameterId — no explicit update needed).
- **Categories that REMAIN skipped (with reason surfaced):** Built-in (`fp.Id.Value < 0`), Reporting (`fp.IsReporting`), Name conflict (new name collides with existing param in same family). No auto-suffixing.
- **Standard-item pre-flight skip detection (REQ-04):** Four conditions — read-only/built-in types, name conflict, sheet-number locked/restricted format, system families (`Family.IsSystemFamily == true`). Detection during the dry-run pass.
- **Two-pass execution:** Pass 1 (dry-run validate) flips failures to skip-with-reason before commit; Pass 2 (commit) executes survivors only. Per-param SubTransaction inside EditFamily/LoadFamily as runtime safety net.
- **Non-sticky skip.** A skipped row re-evaluates fresh on next Apply.
- **Per-family transaction grain unchanged.** All params in one family still go through one EditFamily/LoadFamily cycle.
- **Status column reuse.** Skip reason in Status for skipped rows; side-effect count (e.g. "+2 formulas, +1 dimension") for safe-rename rows. No new columns.
- **Tooltip on skipped rows.** Hovering Status shows full reason text.
- **Disabled checkbox + muted row.** Skipped rows: `IsEnabled = false`, reduced opacity. User cannot tick a skipped row.
- **LogView mirror.** One log line per skipped row: `Skipped '{originalName}' ({type}/{family}): {reason}`. Severity = LogWarning. Applies to both FamilyParameter and standard items.
- **Side-effect preview inline in Status.** Computed during dry-run. No confirmation modal. Recomputed on every preview (existing 150ms debounce).
- **Logging conventions:** Skip = LogWarning `Skipped '{name}' ({context}): {reason}`; Safe Rename success extends existing log line: `Renamed '{old}' to '{new}' (updated N formulas, N dimension labels)`.
- **LogView-only logging surface** (Phase 02.5 decision held).
- **Locale-safe Revit API access** (Phase 02.5 decision held).

### Claude's Discretion
- Exact name of the new dimension-label update service (e.g. `IDimensionLabelUpdateService` vs. inline helper inside `BatchRenameExecutionService`).
- Whether to extract a `RenameSkipDetector` strategy or keep detection inline alongside `GetRenameSkipReason`.
- Wave breakdown of plans (suggested: Wave 0 test scaffolds → Wave 1 REQ-04 UI/log surfacing + standard-item skip detection → Wave 2 REQ-02 formula safe rename → Wave 3 REQ-03 dimension safe rename → Wave 4 phase-end manual Revit verify).
- How the dry-run pass surfaces "this row will fail at commit" without mutating Revit state — likely a probe SubTransaction inside the family edit.
- Whether the dry-run pass for standard items happens INSIDE the main rename transaction or in a pre-flight read-only walk (recommendation: pre-flight read-only).

### Deferred Ideas (OUT OF SCOPE)
- Shift-click multi-select on preview rows
- Excel-style per-column filter visual chrome (popup with searchable checklist) — functional `SetColumnFilter` API already shipped in Plan 03-05; visual chrome is what's missing
- Modernize preview UI look
- Add more scopes (Schedules, Tags, Title Blocks, etc.)
- Async `CollectBaseElements`
- Single-select pill UX for exclusive scope toggle
- Collapsing `SearchReplaceService` pass-through facade
- Fixing `count`-on-rollback bug at `BatchRenameExecutionService.cs:185`
- All other v1.2 Phase 6 backlog items
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| REQ-02 | Safe Rename for formula-referenced parameters | Wire existing `IFormulaUpdateService`/`FormulaUpdateService` into `BatchRenameExecutionService`; `FormulaNameUpdater.UpdateFormula` is the tested engine. Per-param SubTransaction pattern proven in Phase 02. |
| REQ-03 | Safe Rename for dimension-label parameters | New dimension-label reassignment path needed; `BuildDimensionLabelNames` already collects dimensions; reassignment is `dim.FamilyLabel = newParamRef` inside the same SubTransaction that renames the param. |
| REQ-04 | Reason for Skip in Batch Rename UI/Logs | `ElementRowViewModel.Status` is the carrier; `SearchReplacePreviewService.ProcessPreview` populates it; `SearchReplaceView.xaml` needs checkbox `IsEnabled` binding and muted-row Style trigger; standard-item pre-flight detection is net-new. |
</phase_requirements>

---

## Summary

Phase 4 is purely a service-layer and ViewModel wiring phase — no new infrastructure, no new data model. The three deliverables (REQ-02, REQ-03, REQ-04) each have a distinct insertion point in the already-working rename pipeline.

**REQ-02 (formula safe rename)** requires wiring `IFormulaUpdateService` (already registered, zero consumers) into `RenameFamilyParameters`. After `manager.RenameParameter(param, newName)` succeeds, the caller iterates over every other param whose formula `ContainsReference(oldName)`, rewrites the formula via `UpdateFormula`, and calls `manager.SetFormula(thatParam, updatedFormula)` — all inside the per-param SubTransaction. `FormulaNameUpdater` is a proven pure-string engine with 6 passing tests.

**REQ-03 (dimension safe rename)** requires a companion loop after the rename: collect every `Dimension` in the family document whose `FamilyLabel.Definition.Name == oldName`, then set `dim.FamilyLabel = newParam` (the freshly renamed param reference). `BuildDimensionLabelNames` already does the collection; the Phase 4 addition is using those dimension objects — not just their names — to do the reassignment. This can live in a small extracted helper or inline.

**REQ-04 (skip-reason surfacing)** is the widest surface change: it touches `SearchReplacePreviewService.ProcessPreview` (populate `Status` and set `IsRenameable` / `IsChecked = false` for skipped rows), `SearchReplaceView.xaml` (bind checkbox `IsEnabled`, add muted Style trigger), and adds a pre-flight standard-item skip detector (four conditions) that runs before the main `_transactionService.Run(...)` block.

**Primary recommendation:** Implement in the suggested wave order — Wave 0 test scaffolds first so every subsequent plan has RED tests to flip GREEN. The existing `GetRenameSkipReason` narrowing (remove three old skip reasons, keep three) and the `GetStandardItemSkipReason` addition are the highest-risk changes; write tests before touching the service.

---

## Standard Stack

### Core (no new libraries — all already in project)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xunit | 2.6.5 | Unit test framework | Already in `LECG.Tests.csproj` |
| FluentAssertions | 6.12.0 | Test assertions | Already in project |
| NSubstitute | 5.1.0 | Mock/stub interfaces | Already in project |
| CommunityToolkit.Mvvm | (project version) | `ObservableObject`/`ObservableProperty` | Used by `ElementRowViewModel` |
| Autodesk.Revit.DB | Revit 2024+ | `FamilyManager`, `Dimension`, `SubTransaction` | Required Revit API |

### No New Dependencies
Phase 4 adds zero NuGet packages. All Revit API types needed (`FamilyManager.SetFormula`, `Dimension.FamilyLabel`, `SubTransaction`) are already used elsewhere in the codebase.

**Build command while Revit is open:**
```bash
dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true
```

---

## Architecture Patterns

### Recommended Project Structure (additions only)

```
src/Services/Renaming/
├── BatchRenameExecutionService.cs      # MODIFIED: wire IFormulaUpdateService, add SubTransaction per-param, narrow GetRenameSkipReason, add GetStandardItemSkipReason, add two-pass dry-run
├── SearchReplacePreviewService.cs      # MODIFIED: populate Status + IsRenameable during ProcessPreview
└── (optional) IDimensionLabelUpdateService.cs + DimensionLabelUpdateService.cs

src/Views/
└── SearchReplaceView.xaml              # MODIFIED: IsEnabled binding on Sel checkbox, muted-row Style trigger, Status tooltip

src/ViewModels/Components/
└── ElementRowViewModel.cs              # MODIFIED: add IsRenameable bool property (drives IsEnabled + muted style)

LECG.Tests/Services/
├── FormulaUpdateServiceTests.cs        # NEW: unit tests for IFormulaUpdateService wiring
├── BatchRenameSafeRenameTests.cs       # NEW: safe-rename integration tests (formula + dim-label paths)
└── RenameSkipDetectorTests.cs          # NEW: tests for both GetRenameSkipReason (narrowed) and GetStandardItemSkipReason
```

### Pattern 1: Per-Parameter SubTransaction Inside EditFamily
**What:** Each "rename param + update its formulas + reassign its dimension labels" runs in a `SubTransaction` scoped to that one parameter. If any step throws, only that param's SubTransaction rolls back; other params in the same family still commit.
**When to use:** Any mutation of FamilyManager state where partial success is acceptable.

```csharp
// Source: existing FormulaAutoGroupingCommand.MoveParamsToGroup (Phase 02 pattern)
// Adapted for Phase 4 BatchRenameExecutionService.RenameFamilyParameters

using var subTx = new SubTransaction(famDoc);
subTx.Start();
try
{
    manager.RenameParameter(paramToRename, item.NewValue);

    // REQ-02: update formulas that reference the old name
    foreach (FamilyParameter other in manager.Parameters)
    {
        if (string.IsNullOrEmpty(other.Formula)) continue;
        if (!FormulaNameUpdater.ContainsReference(other.Formula, item.OriginalValue)) continue;
        string updated = _formulaUpdateService.UpdateFormula(other.Formula, item.OriginalValue, item.NewValue);
        manager.SetFormula(other, updated);
        formulaCount++;
    }

    // REQ-03: reassign dimension labels that pointed at the old param
    foreach (Dimension dim in dimensionObjects)
    {
        if (dim.FamilyLabel?.Definition.Name != item.OriginalValue) continue;
        FamilyParameter? renamedRef = FindFamilyParameterByName(manager, item.NewValue);
        if (renamedRef != null) dim.FamilyLabel = renamedRef;
        dimCount++;
    }

    subTx.Commit();
    logger.LogSuccess($"Renamed '{item.OriginalValue}' to '{item.NewValue}' (updated {formulaCount} formulas, {dimCount} dimension labels)");
    renamedCount++;
}
catch
{
    subTx.RollbackIfNotClosed();
    logger.LogWarning($"Skipped '{item.OriginalValue}' in '{familyName}': reference update failed");
}
```

### Pattern 2: Dry-Run Pass (Two-Pass Execution for Standard Items)
**What:** Pre-flight read-only walk before the main `_transactionService.Run(...)` — avoids committing partial renames and catches skip conditions early.
**When to use:** Standard-item renames (non-FamilyParameter rows); keep pre-flight OUTSIDE the main transaction so the transaction stays small.

```csharp
// Source: CONTEXT.md §Failure Policy — Two-Pass Execution
// Pre-flight read-only pass — runs before _transactionService.Run(...)
foreach (var item in standardItems)
{
    if (!item.IsChecked) continue;
    ElementId id = new ElementId(item.Id);
    Element el = doc.GetElement(id);
    string? skipReason = GetStandardItemSkipReason(el, item.NewValue, doc);
    if (skipReason != null)
    {
        item.Status = skipReason;
        item.IsChecked = false;   // prevent execution in commit pass
        logger.LogWarning($"Skipped '{item.OriginalValue}' ({item.Type}): {skipReason}");
    }
}
// Commit pass then processes only items where IsChecked is still true
```

### Pattern 3: Status + IsRenameable Surfacing in ProcessPreview
**What:** `SearchReplacePreviewService.ProcessPreview` populates `Status` (skip reason or side-effect count) and `IsRenameable` flag. Downstream XAML binds `IsEnabled` to `IsRenameable`.
**When to use:** Any row with a deterministic skip condition known at preview time.

```csharp
// Source: SearchReplacePreviewService.ProcessPreview (src/Services/Renaming/SearchReplacePreviewService.cs)
// Extended for Phase 4 — FamilyParameter skip reason population
// (standard-item skip reasons come from the dry-run pass at execute time)
var row = new ElementRowViewModel
{
    ...
    Status = skipReason ?? sideEffectSummary,  // e.g. "built-in parameter" or "+2 formulas"
    IsRenameable = skipReason == null,
    IsChecked = skipReason == null,            // auto-uncheck skipped rows
};
```

### Pattern 4: Muted Row + Disabled Checkbox in XAML
**What:** A `DataGrid` row `Style` with a `DataTrigger` on `IsRenameable == false` reduces opacity to ~0.45 and sets the row foreground to a muted color. The Sel-column checkbox binds `IsEnabled="{Binding IsRenameable}"`.
**When to use:** Visual indication that the user cannot select the row.

```xml
<!-- Source: CONTEXT.md §Skip-Reason Surfacing in UI -->
<!-- SearchReplaceView.xaml — inside the LecgDataGrid's RowStyle -->
<Style.Triggers>
    <DataTrigger Binding="{Binding IsRenameable}" Value="False">
        <Setter Property="Opacity" Value="0.45"/>
        <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
    </DataTrigger>
</Style.Triggers>

<!-- Sel column CheckBox -->
<CheckBox IsChecked="{Binding IsChecked}"
          IsEnabled="{Binding IsRenameable}"/>

<!-- Status cell tooltip -->
<DataGridTextColumn.ElementStyle>
    <Style TargetType="TextBlock">
        <Setter Property="ToolTip" Value="{Binding Status}"/>
    </Style>
</DataGridTextColumn.ElementStyle>
```

### Anti-Patterns to Avoid
- **Mutating FamilyManager state outside a SubTransaction:** `manager.SetFormula` inside a top-level transaction is not per-param rollback safe. Always wrap per-parameter in a SubTransaction.
- **Reading `Dimension.FamilyLabel` after rename without refreshing the param reference:** After `manager.RenameParameter`, re-query the parameter by new name via `FindFamilyParameterByName` before assigning `dim.FamilyLabel`.
- **Running standard-item pre-flight INSIDE `_transactionService.Run`:** Pre-flight must be a read-only pass outside the transaction; see Pattern 2.
- **Caching dimension objects across the EditFamily cycle:** Collect dimensions fresh after opening the family document; the collector returns live references valid for that `famDoc` session.
- **Using English-string comparisons for FamilyParameter names:** Parameter names are user-defined strings, not Revit-localized API names. Ordinal comparison is correct here (as in existing `FindFamilyParameterByName`).
- **Setting `count++` before `subTx.Commit()`:** The existing Phase 03 bug at line 185 increments count inside a conditional transaction lambda even on rollback. Phase 4 must NOT repeat this pattern — increment only after a confirmed commit or successful SubTransaction.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Formula token substitution | Custom regex/string-replace | `FormulaNameUpdater.UpdateFormula` | Already handles word-boundary matching, quoted-text exclusion, tested with 6 unit tests |
| Checking if a formula references a param | Manual string search | `FormulaNameUpdater.ContainsReference` | Handles partial-token false-positives, quoted-text exclusion |
| Iterating FamilyManager parameters | LINQ on `fm.Parameters` raw | Existing `FindFamilyParameterByName` loop pattern | FamilyManager.Parameters is `IEnumerable<FamilyParameter>` — the iteration pattern is already in place |
| Per-param rollback in EditFamily | Custom try/catch with transaction nesting | `SubTransaction` | Only SubTransactions can be rolled back inside an active Transaction; raw try/catch does not roll back Revit state |
| Skip-reason detection | Ad-hoc inline conditions scattered across methods | Extract `GetRenameSkipReason` (existing) + new `GetStandardItemSkipReason` | Centralizes conditions, testable without Revit |

**Key insight:** The formula engine and the dimension-label collection infrastructure are fully built. Phase 4 is "wire the existing wires" — not "build new engine parts."

---

## Common Pitfalls

### Pitfall 1: SubTransaction Not Disposed on Exception Path
**What goes wrong:** If SubTransaction.Start() succeeds but an exception escapes before RollbackIfNotClosed/Commit, Revit leaves the family document in a corrupt mid-transaction state; subsequent LoadFamily fails.
**Why it happens:** Missing try/finally or catch-all before the SubTransaction using block.
**How to avoid:** Wrap SubTransaction in `using` **or** ensure `RollbackIfNotClosed()` is called in every catch/finally path.
**Warning signs:** `famDoc.LoadFamily` throws "cannot load while transaction open" after a rename failure.

### Pitfall 2: Stale FamilyParameter Reference After Rename
**What goes wrong:** `dim.FamilyLabel = paramToRename` sets the dimension label to the ORIGINAL parameter object reference. After `manager.RenameParameter(paramToRename, newName)`, the object reference is still valid but represents the RENAMED param — this is actually fine. However, `paramToRename.Definition.Name` will now return `newName`. The risk is passing the wrong reference when multiple params are being renamed in sequence.
**Why it happens:** Confusion between object identity (ParameterId stays stable) and name string.
**How to avoid:** After `RenameParameter`, re-fetch the param by new name via `FindFamilyParameterByName(manager, item.NewValue)` before using it for `dim.FamilyLabel` reassignment — eliminates ambiguity.
**Warning signs:** Dimension labels silently revert on LoadFamily.

### Pitfall 3: `count` Pre-increment Bug Pattern (Pre-existing)
**What goes wrong:** The existing `count += renamedInFamily` at `BatchRenameExecutionService.cs:198` runs AFTER `RunConditional` but the inner lambda increments `renamedInFamily` before knowing if Revit committed. If the transaction rolls back, the count is wrong.
**Why it happens:** Count increment inside transaction lambda, not after confirmed commit.
**How to avoid:** Phase 4 must NOT introduce the same pattern. Increment only in the SubTransaction commit branch, not speculatively.
**Warning signs:** "Batch rename complete. Modified X elements." where X > actual renames visible in Revit.

### Pitfall 4: Dry-Run Probe SubTransaction Leaving Open State
**What goes wrong:** If a probe SubTransaction (used to test formula validity) is not properly rolled back, the outer transaction sees uncommitted changes that corrupt later operations.
**Why it happens:** Exception during probe — SubTransaction never rolled back.
**How to avoid:** Always roll back probe SubTransactions unconditionally (don't commit them). Wrap in try/finally.

### Pitfall 5: Name Conflict Detection Cross-Batch Race
**What goes wrong:** If two rows in the same batch rename A → B and C → B, the dry-run pass checking each row individually will not catch the second collision (B doesn't exist yet when checking C → B).
**Why it happens:** Dry-run checks against the current family state, not the projected post-rename state.
**How to avoid:** During the dry-run, build a `HashSet<string>` of `NewValue` strings already claimed by other checked rows in the same family batch. Check against both the existing params AND this claimed-names set.
**Warning signs:** The second rename in the collision silently fails at commit time with a Revit ArgumentException.

### Pitfall 6: `IsRenameable` Missing from `ElementRowViewModel`
**What goes wrong:** XAML tries to bind `IsEnabled="{Binding IsRenameable}"` but the property doesn't exist → silent binding failure, checkbox stays enabled for skipped rows.
**Why it happens:** Forgetting to add the property to the ViewModel before wiring XAML.
**How to avoid:** Add `IsRenameable` to `ElementRowViewModel` in the Wave 0 scaffold plan, before any XAML binding is written.

---

## Code Examples

Verified patterns from actual source files:

### GetRenameSkipReason — Current (to be narrowed in Phase 4)
```csharp
// Source: src/Services/Renaming/BatchRenameExecutionService.cs:334-376
// Phase 4 removes the dimension-label, formula-referenced, and element-associated branches.
// Remaining: built-in (fp.Id.Value < 0), reporting (fp.IsReporting), name-conflict.
private static string? GetRenameSkipReason(
    FamilyParameter fp,
    string newName,
    FamilyManager mgr,
    HashSet<string> dimensionLabels,
    HashSet<string> formulaReferenced,
    HashSet<string> elementAssociated)
```

### BuildDimensionLabelNames — Current (to be extended for reassignment)
```csharp
// Source: src/Services/Renaming/BatchRenameExecutionService.cs:381-404
// Phase 4 extends: instead of just building a HashSet<string> of names,
// also return (or separately collect) the Dimension objects themselves for reassignment.
private static HashSet<string> BuildDimensionLabelNames(Document famDoc)
{
    var dimensions = new FilteredElementCollector(famDoc)
        .OfClass(typeof(Dimension))
        .Cast<Dimension>();
    foreach (Dimension dim in dimensions)
    {
        FamilyParameter? label = dim.FamilyLabel;
        if (label != null) names.Add(label.Definition.Name);
    }
}
```

### FormulaUpdateService — Current (zero consumers, ready to wire)
```csharp
// Source: src/Services/Renaming/FormulaUpdateService.cs
public sealed class FormulaUpdateService : IFormulaUpdateService
{
    public string UpdateFormula(string formula, string oldName, string newName)
        => FormulaNameUpdater.UpdateFormula(formula, oldName, newName);
}
// Registered: Bootstrapper.cs:131. Inject into BatchRenameExecutionService constructor.
```

### ElementRowViewModel.Status — Current (plain settable string)
```csharp
// Source: src/ViewModels/Components/ElementRowViewModel.cs:25
public string Status { get; set; } = "";  // command-specific outcome / skip reason
// Phase 4 adds: public bool IsRenameable { get; set; } = true;
```

### Skip-Gated RED Test Pattern (Phase 03-00 convention)
```csharp
// Source: LECG.Tests/Services/ (established in Plan 03-00)
[Fact(Skip = "Implement in plan 04-XX")]
[Trait("Category", "Renaming")]
public void GetRenameSkipReason_FormulaReferenced_NoLongerSkips_WhenPhase4Active()
{
    // RED until Phase 4 plan wires IFormulaUpdateService
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Skip formula-referenced params (log only) | Rename + update all referencing formulas | Phase 4 (REQ-02) | Formula-referenced params become renameable |
| Skip dimension-label params (log only) | Rename + reassign all dimension FamilyLabel refs | Phase 4 (REQ-03) | Dimension-driving params become renameable |
| Skip element-associated params (log only) | Rename freely (Revit resolves by ParameterId) | Phase 4 | Element-associated params become renameable |
| Standard items throw at execution on conflict | Pre-flight skip-with-reason before execution | Phase 4 (REQ-04) | User sees reason BEFORE Apply completes |
| FamilyParameter skips log-only | Skip reason surfaces in Status column | Phase 4 (REQ-04) | User sees reason inline in grid row |
| IsChecked drives selection | IsChecked + IsRenameable: skipped rows auto-unchecked and disabled | Phase 4 (REQ-04) | Eliminates "Apply does nothing" confusion |

**Deprecated/outdated:**
- `GetRenameSkipReason` returning skip reason for formula-referenced/dimension-label/element-associated categories: those three branches will be REMOVED. The updated method handles only: built-in, reporting, name-conflict.

---

## Open Questions

1. **Dimension FamilyLabel reassignment API availability**
   - What we know: `Dimension.FamilyLabel` is readable and used in `BuildDimensionLabelNames` (no exception caught on read). The Revit API documents `FamilyLabel` as a settable property on `Dimension` in a family document context.
   - What's unclear: Whether `FamilyLabel = null` is required before setting a new value (some versions of the Revit API require clearing before reassigning). Cannot verify without a live Revit test.
   - Recommendation: The Wave 3 plan (REQ-03) should include a manual Revit checkpoint specifically verifying dimension label reassignment. Wrap the set in a try/catch with a logged warning if it throws.

2. **`manager.SetFormula` nullability on cleared formulas**
   - What we know: `FormulaNameUpdater.UpdateFormula` returns an empty string when the input formula is empty (it short-circuits). An empty string formula call to `manager.SetFormula(param, "")` may throw or clear the formula depending on Revit version.
   - What's unclear: Whether `SetFormula(param, "")` is a no-op or throws. The guard `if (string.IsNullOrEmpty(fp.Formula)) continue;` in `BuildFormulaReferencedNames` already skips empty formulas, so this path should not be reached.
   - Recommendation: Retain the `string.IsNullOrEmpty(other.Formula)` guard before any `SetFormula` call.

3. **Conflict detection order for batch cross-rename**
   - What we know: CONTEXT.md mentions no auto-suffixing; name conflicts skip with reason.
   - What's unclear: The planner needs to decide whether cross-batch conflict detection (Pitfall 5 above) is implemented in Phase 4 or deferred. CONTEXT.md is silent on this edge case.
   - Recommendation: Implement the claimed-names HashSet in the dry-run pass — it is cheap and prevents silent failures on batch renames where two rows collide.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.6.5 + FluentAssertions 6.12.0 + NSubstitute 5.1.0 |
| Config file | `LECG.Tests/LECG.Tests.csproj` |
| Quick run command | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true --filter "Category=Renaming" --no-build` |
| Full suite command | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-02 | `GetRenameSkipReason` no longer returns skip for formula-referenced params | unit | `dotnet test --filter "FullyQualifiedName~RenameSkipDetectorTests"` | Wave 0 |
| REQ-02 | After rename, all formulas referencing old name are updated | unit | `dotnet test --filter "FullyQualifiedName~BatchRenameSafeRenameTests"` | Wave 0 |
| REQ-02 | `IFormulaUpdateService` wiring: `UpdateFormula` called for each referencing formula | unit (NSubstitute mock) | `dotnet test --filter "FullyQualifiedName~FormulaUpdateServiceTests"` | Wave 0 |
| REQ-03 | `GetRenameSkipReason` no longer returns skip for dimension-label params | unit | `dotnet test --filter "FullyQualifiedName~RenameSkipDetectorTests"` | Wave 0 |
| REQ-03 | After rename, dimension FamilyLabel reassignment loop runs | unit (mock dimension collector) | `dotnet test --filter "FullyQualifiedName~BatchRenameSafeRenameTests"` | Wave 0 |
| REQ-04 | `GetRenameSkipReason` still skips built-in/reporting/name-conflict | unit | `dotnet test --filter "FullyQualifiedName~RenameSkipDetectorTests"` | Wave 0 |
| REQ-04 | `GetStandardItemSkipReason` returns reason for read-only/conflict/system-family | unit | `dotnet test --filter "FullyQualifiedName~RenameSkipDetectorTests"` | Wave 0 |
| REQ-04 | `ProcessPreview` sets `Status` = skip reason for skipped rows | unit | `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` | Exists (extend) |
| REQ-04 | `ProcessPreview` sets `IsRenameable = false` and `IsChecked = false` for skipped rows | unit | `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` | Exists (extend) |
| REQ-04 | `ProcessPreview` sets `Status` = side-effect count for safe-rename rows | unit | `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` | Exists (extend) |
| REQ-04 | Manual Revit: skipped rows appear muted + unchecked in grid; tooltip shows reason | manual | Phase-end Revit verify session | — |
| REQ-04 | Manual Revit: LogView shows one LogWarning per skipped row | manual | Phase-end Revit verify session | — |

### Sampling Rate
- **Per task commit:** `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true --filter "Category=Renaming" -p:SkipRevitDeploy=true`
- **Per wave merge:** `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` (full 100+ test suite)
- **Phase gate:** Full suite green + manual Revit verification session before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `LECG.Tests/Services/RenameSkipDetectorTests.cs` — covers narrowed `GetRenameSkipReason` (REQ-02/03/04) + new `GetStandardItemSkipReason` (REQ-04); skip-gated RED until implementing plans
- [ ] `LECG.Tests/Services/FormulaUpdateServiceTests.cs` — covers `IFormulaUpdateService` injection wiring (REQ-02); `FormulaNameUpdaterTests.cs` covers the engine already — this new file covers the service + call-site
- [ ] `LECG.Tests/Services/BatchRenameSafeRenameTests.cs` — covers end-to-end safe-rename logic (formula count + dimension count in Status, SubTransaction rollback path); skip-gated RED
- [ ] Extend `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — add `IsRenameable`/`Status` tests once `ElementRowViewModel.IsRenameable` property is added in Wave 0

*(Framework install not needed — xunit/FluentAssertions/NSubstitute already in LECG.Tests.csproj)*

---

## Sources

### Primary (HIGH confidence)
- `src/Services/Renaming/BatchRenameExecutionService.cs` — full source read; `GetRenameSkipReason` (lines 334-376), `BuildDimensionLabelNames` (lines 381-404), `BuildFormulaReferencedNames` (lines 406-434), `RenameFamilyParameters` (lines 247-286), EditFamily/LoadFamily cycle (lines 142-216)
- `src/Services/Renaming/FormulaUpdateService.cs` — full source read; confirmed zero consumers
- `src/Services/Renaming/SearchReplacePreviewService.cs` — full source read; ProcessPreview flow and row construction
- `LECG.Core/Rename/FormulaNameUpdater.cs` — full source read; `UpdateFormula`, `ContainsReference`, regex pattern
- `src/ViewModels/Components/ElementRowViewModel.cs` — full source read; `Status` field confirmed plain settable string
- `src/ViewModels/SearchReplaceViewModel.cs` — partial read; `PreviewItems`, `PreviewView`, `SetColumnFilter`, `MatchesAllFilters` confirmed
- `src/Views/SearchReplaceView.xaml` — partial read; grid structure, scope checkboxes, LecgDataGrid usage confirmed
- `LECG.Tests/Services/FormulaNameUpdaterTests.cs` — full source read; 6 tests covering word-boundary and quote exclusion
- `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — full source read; existing test shape for extension
- `.planning/phases/04-advanced-renaming-logic/04-CONTEXT.md` — full read; locked decisions and code context are primary authority for this phase

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` — project history; Phase 02 SubTransaction pattern for MoveParamsToGroup cited as the proven template for per-param SubTransaction usage
- `.planning/REQUIREMENTS.md` — requirement status table; confirms REQ-02/03/04 Pending

### Tertiary (LOW confidence)
- Revit API documentation on `Dimension.FamilyLabel` setter availability and `FamilyManager.SetFormula` behavior with empty strings — not directly verifiable without live Revit; flagged in Open Questions

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries confirmed in LECG.Tests.csproj; no new dependencies needed
- Architecture: HIGH — insertion points confirmed by direct source reading; SubTransaction pattern proven in Phase 02
- Pitfalls: HIGH for pitfalls 1-4 and 6 (derived from source code); MEDIUM for pitfall 5 (cross-batch conflict race — logic inference, not witnessed in code)
- Dimension FamilyLabel setter: MEDIUM — API is well-documented but setter behavior edge cases need live Revit validation

**Research date:** 2026-05-10
**Valid until:** 2026-06-10 (stable domain — Revit API Family editing APIs do not change between minor versions)
