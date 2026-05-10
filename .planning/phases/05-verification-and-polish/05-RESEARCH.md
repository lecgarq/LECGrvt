# Phase 5: Verification & Polish — Research

**Researched:** 2026-05-10
**Domain:** xUnit unit-test coverage for `src/Services/Renaming/` (6 services) + 3 polish bug fixes (RED-then-GREEN)
**Confidence:** HIGH

## Summary

Phase 5 is an internally-bounded testing & polish phase — no external library research is required. Every relevant pattern (pure-data helper extraction, skip-gated RED, `InternalsVisibleTo`, reflection-over-NSubstitute for Revit-API-bound types, `IProgressReporter` mocking, `List<Action>` reassignment seam) is already established in Phases 3 and 4 and lives in this codebase. Research therefore focuses on (a) mapping each of the six target services' current testability surface, (b) designing the new/deepened fixtures around concrete file:line anchors, and (c) locating the three polish bugs precisely and designing minimal RED tests that exercise the seams already in place.

Critical finding while reading the source: **the `count`-on-rollback bug is NOT at `BatchRenameExecutionService.cs:185` literally** — line 185 is a `continue` in the "not a Family" branch. The actual bug is at **line 222** (`count += renamedInFamily;` lives INSIDE the `_transactionService.RunConditional` lambda). If the outer family-edit transaction rolls back (predicate returns false, OR Revit refuses commit), the `count` increment has already been observed by the outer scope. The fix is to move `count += renamedInFamily;` to a post-`RunConditional` `if (committed)` branch.

**Primary recommendation:** Five-wave plan — Wave 0 matrix + RED scaffolds → Wave 1 deepen `BaseElementCollectionServiceTests` + add `RenameRulePipelineServiceTests` → Wave 2 add `BatchRenameExecutionServiceTests` direct + `SearchReplaceServiceTests` → Wave 3 polish RED→GREEN cycle for the 3 bugs → Wave 4 phase-end manual Revit verify + closure.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Service Scope (REQ-05 boundary):**
- ONLY `src/Services/Renaming/` counts for REQ-05 in this phase. Six services:
  `BaseElementCollectionService`, `BatchRenameExecutionService`, `FormulaUpdateService`,
  `RenameRulePipelineService`, `SearchReplaceService`, `SearchReplacePreviewService`.
- Existing naming-policy fixtures and `LECG.Core/Rename/` fixtures are NOT re-opened.

**Fixtures to Add or Deepen:**
- `RenameRulePipelineServiceTests` — NEW fixture (pipeline composition / rule ordering / pass-through).
- `BatchRenameExecutionServiceTests` — NEW direct fixture (skip detection, transaction loop ordering, progress accounting, composite log formatting).
- `SearchReplaceServiceTests` — NEW fixture, **full coverage** despite v1.2 deletion candidacy.
- `BaseElementCollectionServiceTests` — deepen from current 4-test surface; cover collector branches per scope at `ElementLabelService.GetLabelsFromRaw` boundary + null-fallback paths; stay pure-data.
- `FormulaUpdateServiceTests` (3 tests), `SearchReplacePreviewServiceTests` (8 tests), `BatchRenameSafeRenameTests` (9 tests) STAY AS-IS — adequate.

**Coverage Depth:**
- Pure-data helpers + key branches (Phase 04 convention: `Collect*` / `Evaluate*` / `Build*` / `Format*`).
- Extraction policy: extract where new helper is ≤ ~30 LOC and decoupling is clean. Otherwise skip-gated RED naming the unreachable Revit dependency.
- **NO coverlet / numeric coverage gate.** Acceptance = the matrix in 05-VERIFICATION.md, not a percentage.

**Polish Bug Fixes (RED-test gated):**
1. `count`-on-rollback (CONTEXT says `BatchRenameExecutionService.cs:185`; ACTUAL location is `:222`, see Code Context below).
2. `Dimension.FamilyLabel` null-clear (Phase 4 C1 follow-up) — null-clear before re-assign, reuses `ExecuteDimensionReassignments(List<Action>)` seam.
3. Standard-item progress capping below 100%.

**Acceptance:**
- Service→fixture coverage matrix in `05-VERIFICATION.md` (one row per service, ✅/⚠/❌ per scenario).
- 100% GREEN suite.
- Short manual Revit checklist (3 polish fixes), trust-based sign-off acceptable.
- Flip REQ-05 Pending → Complete in REQUIREMENTS.md only after matrix + manual checklist pass.

### Claude's Discretion
- Exact scenario list per matrix row (highest-value behaviors per service).
- Wave breakdown.
- Whether helper extraction lands in the same plan as the tests, or splits into a small "extract" plan + a separate "test the extract" plan.
- Where skip-gated RED is used: pick the reason string naming the unreachable Revit dependency.
- Test fixture file naming: follow Phase-04 convention (`{ServiceName}Tests.cs` under `LECG.Tests/Services/`).

### Deferred Ideas (OUT OF SCOPE)
- Coverage of `LECG.Core/Rename/` (FormulaNameUpdater, RenameRuleEngine) and naming policies — already covered.
- Coverlet / numeric coverage gating.
- Collapse `SearchReplaceService` pass-through facade.
- Async `CollectBaseElements`.
- Scope UX (single-select pills).
- Dedup `ParamGroup` swallowed try/catch at `BaseElementCollectionService.cs:213`+`:275`.
- Per-column header funnel chrome.
- `SwapStyle` orphaning non-`CurveElement` consumers.
- New Batch Rename scopes (Schedules, Tags, Title Blocks).
- UI overhaul.
- FsCheck / snapshot testing / CI matrix enforcement.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| REQ-05 | Unit tests for all renaming services | This research maps each of the 6 services in `src/Services/Renaming/` to a dedicated unit-test fixture with concrete scenarios; defines the service→fixture matrix structure for 05-VERIFICATION.md; locates the 3 polish bugs at file:line and proposes RED-test designs against existing pure-data seams. |
</phase_requirements>

## Service Inventory & Testability Surface

The 6 services in `src/Services/Renaming/`, ordered by complexity / testability:

| # | Service | LOC | Revit-API surface | Existing test fixture | Status per CONTEXT |
|---|---------|-----|-------------------|----------------------|--------------------|
| 1 | `FormulaUpdateService.cs` | 14 | none (delegates to `FormulaNameUpdater`) | `FormulaUpdateServiceTests` (3 tests) | **stays as-is** |
| 2 | `RenameRulePipelineService.cs` | 22 | none (composes 5 `IRenameRule` instances) | (none — only `RenameRuleEngineTests` engine-level) | **NEW fixture** |
| 3 | `SearchReplaceService.cs` | 78 | none (facade — delegates to 3 injected services) | (none) | **NEW fixture, full coverage** |
| 4 | `SearchReplacePreviewService.cs` | 235 | none (pure pipeline over `List<ElementData>`) | `SearchReplacePreviewServiceTests` (8 tests) | **stays as-is** |
| 5 | `BaseElementCollectionService.cs` | 335 | **heavy** (`FilteredElementCollector`, `Document.ParameterBindings`, `Definition.GetGroupTypeId`) | `BaseElementCollectionServiceTests` (4 tests via `ElementLabelService.GetLabelsFromRaw` SSoT) | **deepen** |
| 6 | `BatchRenameExecutionService.cs` | 944 | **heavy** (`ITransactionService.Run/RunConditional`, `Document.EditFamily/LoadFamily`, `FamilyManager`, `SubTransaction`, `Dimension.FamilyLabel`) | partial (`BatchRenameSafeRenameTests` 9 tests + `FormulaUpdateServiceTests` 1 wiring test) | **NEW direct fixture** |

### Per-service surface analysis

**`FormulaUpdateService`** — pure pass-through to `LECG.Core.Rename.FormulaNameUpdater.UpdateFormula`. Already covered by 3 tests; matrix row is ✅, no work.

**`RenameRulePipelineService`** — single public method `ApplyRules(text, context, index)` composes 5 rules in a fixed order: Remove → Replace → Case → Add → Numbering. All 5 rules are `IRenameRule` and can be substituted via NSubstitute against `RenameRuleContext` (a `record`). No Revit API touch — 100% pure-data testable. Estimated 5–8 scenarios.

**`SearchReplaceService`** — facade delegating to `ISearchReplacePreviewService`, `IBatchRenameExecutionService`, `IBaseElementCollectionService`. The two `ExecuteBatchRename` overloads accept a Revit `Document` — but the facade does NOT touch it (just passes through). NSubstitute can mock all three dependencies. Public methods: `CollectBaseElements`, `GetUniqueCategories`, `ProcessPreview`, `ExecuteBatchRename(...IProgressReporter)`, `ExecuteBatchRename(...Action<double,string>)`. Each delegation can be verified by `Received(1)` against the substitute. Estimated 5–7 scenarios. Note: `Document doc` will be passed as `null` to substitutes — substitutes don't dereference it. If null-throw guards exist on facade methods, those must be tested too.

**`SearchReplacePreviewService`** — pure-data pipeline; 8 tests already cover scope filtering, name filtering, FamilyParameter status, cross-batch collision. Matrix row is ✅, no work.

**`BaseElementCollectionService`** — every branch goes through `FilteredElementCollector` which requires a Revit Document. Current strategy (Plan 03-03 decision): test the *SSoT helper* `ElementLabelService.GetLabelsFromRaw` rather than the collector loop. The 4 existing tests verify the no-blanks invariant via that seam. To **deepen** without a Revit harness, the high-value pure-data targets are:
- Scope dispatch branches (types / families / views vs sheets / materials / fillPatterns / object-vs-line-styles / familyParameters) — currently a 9-branch if/else cascade. Cannot test directly without a Document. **Best option:** extract a pure-data helper `DispatchScopeFlags(bool... ) → ScopeMask` (≤ 15 LOC) and unit-test that flag→mask mapping. Skip-gate the Revit-bound iteration per branch with reason "requires FilteredElementCollector — covered by manual Revit verification".
- ParamGroup label resolution: `LabelUtils.GetLabelForGroup(groupTypeId)` is wrapped in try/catch that returns `""` on failure (lines 234–243 + 304–313). This try/catch is `BaseElementCollectionService.cs:213` + `:275` per the STATE.md note — **but the CONTEXT explicitly defers dedup to v1.2**. Phase 5 only needs to *cover* its observable contract: when `GetGroupTypeId` throws, label = "". This can be tested via a tiny extracted helper `TryGetGroupLabel(Func<ForgeTypeId> getId, Func<ForgeTypeId,string> getLabel) → string` (~10 LOC), pure.
- Phase A / Phase B param scan (FamilySymbol-then-FamilyInstance) — the dedup logic via `seenParamNames` + `seenParamsByFamily` Dictionary is testable as a pure helper if extracted: `MergeParamScanResults(List<scanA>, List<scanB>, Dictionary<long,HashSet<string>>) → List<ElementData>` (~25 LOC).

Realistic deepening: 4 existing tests → **8–12 tests** total. Time-box per CONTEXT pitfall: prioritize Types / Families / Materials / Parameters paths.

**`BatchRenameExecutionService`** — already heavily refactored in Phase 4 with seven `internal static` pure-data helpers (visible via `InternalsVisibleTo("LECG.Tests")` on `src/InternalsVisibleTo.Tests.cs`):

| Helper | LOC | Tested by | Coverage |
|--------|-----|-----------|----------|
| `CollectFormulaUpdates` | 18 | `BatchRenameSafeRenameTests` | ✅ |
| `LogRenameSuccess` | 3 | `BatchRenameSafeRenameTests` | ✅ |
| `FormatSafeRenameLog` | 10 | indirectly via `LogRenameSuccess` | ✅ |
| `ExecuteDimensionReassignments` | 13 | `BatchRenameSafeRenameTests` | ✅ |
| `GroupCheckedFamilyParameterItemsForTest` | 3 | `BatchRenameSafeRenameTests` | ✅ |
| `ApplyPreFlightSkipReasons` | 19 | `BatchRenameSafeRenameTests` | ✅ |
| `EvaluateFamilyParamSkipReason` | 35 | `BatchRenameSafeRenameTests` (indirectly) | ⚠ direct |
| `EvaluateStandardItemSkipReason` | 30 | `BatchRenameSafeRenameTests` (indirectly) | ⚠ direct |

New direct fixture `BatchRenameExecutionServiceTests` adds:
- Direct branch coverage for `EvaluateFamilyParamSkipReason` (built-in / reporting / name-conflict / safe — 4 paths).
- Direct branch coverage for `EvaluateStandardItemSkipReason` (cross-batch / system-family / name-in-scope / sheet-locked / read-only / freely-renameable — 6 paths).
- `FormatSafeRenameLog` 4-branch matrix (both>0 / formulas-only / dims-only / neither).
- `GroupCheckedFamilyParameterItems` ordering preservation (multiple items per family Id, unchecked excluded, empty case).
- `LegacyProgressReporter` callback wiring — both ctor variants, null-callback no-throw paths (5 methods × null-callback = 5 cases).
- Constructor null-guard tests (`ArgumentNullException` for `formulaUpdateService`; the other two ctor params currently aren't null-checked — verify or add guards).

Plus the **3 polish-fix RED tests** documented below in §Polish Fixes.

## Test Patterns Already Established

### Pattern 1: Pure-data helper extraction (Phase 04 convention)
**What:** Extract Revit-API-bound logic into a pure-data static helper with naming prefix `Collect*` / `Evaluate*` / `Build*` / `Format*`. Mark `internal static` so `InternalsVisibleTo("LECG.Tests")` exposes it to xUnit.
**When to use:** When the new helper is ≤ ~30 LOC and decoupling is clean. Otherwise skip-gate.
**Example:** `BatchRenameExecutionService.CollectFormulaUpdates` at `:407` — 18 LOC; takes `IEnumerable<(string,string)>` instead of `FamilyManager`; tested directly in `BatchRenameSafeRenameTests.RenameFamilyParameters_FormulaReferenced_UpdatesAllReferencingFormulas`.

### Pattern 2: Skip-gated RED test
**What:** `[Fact(Skip = "Implemented by plan 05-XX")]` with a reason string naming the implementing plan ID; ensures the suite stays GREEN while preserving the test as a TDD anchor.
**When to use:** Behavior is locked but implementation lands in a later wave; or behavior is Revit-API-bound and cannot be exercised in xUnit (reason string then names the unreachable Revit dependency, e.g. `"requires FilteredElementCollector — covered by manual Revit verification"`).
**Example:** `BatchRenameSafeRenameTests.Fixture_Anchor_Exists` at `:25` — single non-skipped anchor; rest of the fixture was skip-gated before plans 04-01/04-03/04-04 flipped them GREEN.

### Pattern 3: Anchor test per fixture
**What:** Exactly one non-skipped `[Fact]` per fixture file so `dotnet test --filter` discovers the fixture even when all behavioral tests are skip-gated.
**Why:** Without an anchor, xUnit may not enumerate a fully-skipped fixture in some runners.
**Example:** `BatchRenameSafeRenameTests.Fixture_Anchor_Exists`, `FormulaUpdateServiceTests.Fixture_Anchor_Exists`.

### Pattern 4: Reflection over NSubstitute for Revit-bound interfaces
**What:** When NSubstitute / Castle DynamicProxy cannot proxy an interface because it depends on RevitAPI.dll types not loadable in the test runner (e.g., `ITransactionService` referencing `Document`), use reflection to assert constructor / signature contracts instead of behavior.
**Example:** `FormulaUpdateServiceTests.IFormulaUpdateService_IsInjectableIntoBatchRenameExecutionService_ViaConstructor` at `:63` — walks `typeof(BatchRenameExecutionService).GetConstructors()` to verify the parameter type without instantiating.
**Decision recorded:** Phase 04-03 decision in STATE.md.

### Pattern 5: `List<Action>` reassignment seam
**What:** Decouple Revit-bound side-effects from the helper that orchestrates them by passing a pre-built `IReadOnlyList<Action>`. Production caller builds closures that touch the API; unit tests inject `() => counter++` mock actions.
**Example:** `BatchRenameExecutionService.ExecuteDimensionReassignments` at `:463` accepts `IReadOnlyList<Action>` rather than `List<Dimension>`. This is the exact seam reused for the dimension null-clear polish (see §Polish Fix #2).

### Pattern 6: Build command while Revit is open
```bash
dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true
```
Bypasses MSB3027 file-lock on `ProgramData` addins folder. Phase 03-00 decision.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Mock `ITransactionService` for `BatchRenameExecutionService` tests | Custom test double | Reflection on constructor + test pure-data helpers in isolation | Castle DynamicProxy cannot proxy without RevitAPI.dll loaded — Phase 04-03 precedent |
| Mock `Document` / `FilteredElementCollector` for `BaseElementCollectionService` | Revit harness | Test `ElementLabelService.GetLabelsFromRaw` SSoT + extracted helpers | Plan 03-03 decision; Revit harness adds enormous complexity for tiny gain |
| Mock `Logger.Instance` static for LogView assertions | Custom static-singleton interceptor | Pass `ILogger` through helper signatures + use NSubstitute | All Phase-04 helpers take `ILogger` explicitly; static singleton is only used in `SwapStyle` (out of scope per CONTEXT) |
| Cross-formula reference enumeration | Custom regex | `FormulaNameUpdater.ContainsReference` | Already in `LECG.Core/Rename/`; covered |
| ParamGroup label fallback handling | Custom try/catch wrapper inline | Extract `TryGetGroupLabel` helper | Phase 04 pattern; dedup deferred to v1.2 but observable contract testable in isolation |
| Coverlet numeric gate enforcement | Add coverage threshold CI step | The matrix in 05-VERIFICATION.md | CONTEXT explicitly excludes coverlet/numeric gating |

## Common Pitfalls

### Pitfall 1: Misreading the `count`-on-rollback location
**What goes wrong:** CONTEXT names `BatchRenameExecutionService.cs:185` but that line is the `continue` in the "not a Family" branch (`if (el is not Family family) { ... continue; }`). Easy to author a RED test against the wrong location and have the test pass trivially.
**Why:** Line numbers shift across Phase 4 plans; CONTEXT was authored against a pre-Phase-4 snapshot.
**How to avoid:** Plan author MUST verify against current file: `count += renamedInFamily;` lives at `BatchRenameExecutionService.cs:222` INSIDE `_transactionService.RunConditional(famDoc, "Rename Parameters", _ => { ... })` lambda. The lambda's return value `renamedInFamily > 0` decides whether the outer transaction commits. If the predicate returns true but Revit refuses commit (or any rollback path triggers), `count` has already been incremented in the outer scope.
**Warning signs:** Test passes against `:185` without exercising the rollback path — it isn't testing the actual bug.

### Pitfall 2: Stale FamilyParameter reference after RenameParameter
**What goes wrong:** Already documented in Phase 04 (`Pitfall 2` in code comments at `:344`). Dimension null-clear polish must NOT reintroduce stale-ref bug.
**How to avoid:** The null-clear action must capture the renamed `FamilyParameter? renamedRef` AFTER `FindFamilyParameterByName(manager, item.NewValue)` at `:350`, never the pre-rename `paramToRename`. RED test should verify the action sequence: null-clear FIRST, then re-assign — captured via two `() => callCount++` actions in correct order.

### Pitfall 3: Progress-loop counter increments before IsChecked guard
**What goes wrong:** Lines 85–88 increment `current++` BEFORE `if (!item.IsChecked) continue;`. So unchecked rows DO advance the progress fraction. For a standard-only batch with no skips this is fine — percent reaches 100. For mixed standard+family batches the **standard-loop percent caps at `standardItems.Count / total * 100`** (which is < 100 by construction). The family loop then uses a different scale `familyIndex / byFamily.Count * 100` which jumps back to a fresh 0..100 — not continuous from where standard left off. The final `reporter.Report("Done", 100)` at `:244` is the only call that guarantees 100%.
**How to avoid:** RED test should capture a sequence of `(percent, message)` tuples from a custom `IProgressReporter` test double and assert the **max observed percentage before "Done"** equals 100 for a mixed batch. The fix likely unifies both loops to use `(current / total) * 100` continuously rather than two scales.
**Warning signs:** Manual Revit users see a progress bar that "stutters" or sits at e.g. 60% during family processing then jumps to 100% at the end.

### Pitfall 4: Cross-batch collision set timing
**What goes wrong:** In `ApplyPreFlightSkipReasons` the `claimedNewNames` HashSet at `:65` is populated by the caller's `getSkipReason` closure but the FIRST row's `newValue` is never added to it (only the closure's `GetStandardItemSkipReason` checks membership; it never inserts). The cross-batch collision check is therefore done in the preview pass (`SearchReplacePreviewService.ApplyCrossBatchCollisionCheck` at `:203`), not in the execution pre-flight.
**How to avoid:** When deepening `BatchRenameExecutionServiceTests`, do NOT write a test that expects `ApplyPreFlightSkipReasons` to detect collisions across rows — collisions are pre-detected in the preview layer. Test the execution pre-flight only for single-row read-only/system-family/sheet-locked detection.

### Pitfall 5: Facade test brittleness
**What goes wrong:** `SearchReplaceService` is a v1.2 deletion candidate. Tests that assert *internal call structure* (e.g., "delegates to method X then method Y") couple to dispatch internals that may collapse.
**How to avoid:** Test the **observable behavior** — what the facade returns, that the right substitute method was called with the same args. Don't assert call order between methods. CONTEXT explicitly raises this: "Mitigation: test the observable behavior … not the call structure."

### Pitfall 6: Skip-gated test discovery vs anchor convention
**What goes wrong:** A fixture file with ALL tests `[Fact(Skip = ...)]` may not appear in `dotnet test --filter` enumeration on some runners — the planner sees green and the RED tests stay invisible.
**How to avoid:** Every new fixture (`RenameRulePipelineServiceTests`, `BatchRenameExecutionServiceTests`, `SearchReplaceServiceTests`) gets exactly ONE non-skipped anchor `[Fact(DisplayName = "anchor — ...")]` that simply does `Assert.True(true)` or references `typeof(...)`. Phase 03-00 decision.

## Polish Fixes — File:Line Anchors + RED Test Designs

### Polish Fix #1: `count`-on-rollback

**Code location (verified by reading source):**
- `BatchRenameExecutionService.cs:208` — `bool committed = _transactionService.RunConditional(famDoc, "Rename Parameters", _ => { ... })`
- `BatchRenameExecutionService.cs:222` — `count += renamedInFamily;` **INSIDE the lambda, before the predicate returns**
- `BatchRenameExecutionService.cs:223` — `return renamedInFamily > 0;` (commit predicate)
- `BatchRenameExecutionService.cs:227` — `if (committed) famDoc.LoadFamily(...);`

**The bug:** `count` is captured by the lambda and mutated before the outer transaction is finalized. If the outer transaction's commit is refused (regardless of the predicate returning true), or if `committed == false` AND `renamedInFamily > 0` (rare but possible if inner SubTransactions succeeded but the outer wrap rolls back), the user-reported count overstates actual renames.

**Minimal fix:**
```csharp
// Inside the lambda — return the count, don't mutate outer scope:
int renamedInFamily = 0;
bool committed = _transactionService.RunConditional(famDoc, "Rename Parameters", _ =>
{
    FamilyManager mgr = famDoc.FamilyManager;
    renamedInFamily = RenameFamilyParameters(mgr, ...);
    return renamedInFamily > 0;
});
// Mutate `count` ONLY after committed observation:
if (committed)
{
    count += renamedInFamily;
    famDoc.LoadFamily(doc, _loadOptionsFactory.Create());
}
```

**RED test design:**
- Fixture: `BatchRenameExecutionServiceTests`
- Name: `ExecuteBatchRename_FamilyTransactionRollsBack_CountStaysAtZero`
- Strategy: this test path cannot be exercised without `ITransactionService` proxy. Two options:
  - **(A) Skip-gated RED** with reason `"requires ITransactionService proxy — fix verified by manual Revit verification per 05-VERIFICATION.md"` plus one **pure-data assertion** that the new helper (if extracted) honors the committed=false branch. Most likely choice given Phase 04-03 precedent.
  - **(B) Extract a pure helper** `AccumulateCommittedFamilyCount(bool committed, int renamedInFamily, int currentCount) → int` (3 LOC) and unit-test it directly. Returns `currentCount + renamedInFamily` when committed, else `currentCount`. RED test asserts `committed=false` returns `currentCount` unchanged.
- **Recommendation:** option (B). The helper is 3 LOC, perfectly within the ≤30 LOC extraction policy, and gives a direct pure-data RED→GREEN cycle.

### Polish Fix #2: `Dimension.FamilyLabel` null-clear

**Code location:**
- `BatchRenameExecutionService.cs:347–363` — dimension reassignment loop inside per-param SubTransaction.
- `BatchRenameExecutionService.cs:353–358` — `reassignActions` list of `() => { capturedDim.FamilyLabel = capturedRef; }`.
- `BatchRenameExecutionService.cs:361` — `ExecuteDimensionReassignments(reassignActions, item.OriginalValue, item.NewValue, out dimCount);`
- The pure-data helper at `:463` already accepts `IReadOnlyList<Action>` — Phase 4 designed exactly this seam.

**The Phase 4 C1 observation:** In some Revit versions, `dim.FamilyLabel = newParam;` is refused when the dimension already has a different `FamilyLabel` set. Workaround: null-clear first.

**Minimal fix:** At `:353–358`, prepend a null-clear action before each assignment action:
```csharp
foreach (Dimension dim in dims)
{
    Dimension capturedDim = dim;
    FamilyParameter capturedRef = renamedRef;
    reassignActions.Add(() => { capturedDim.FamilyLabel = null; });    // NEW: null-clear first
    reassignActions.Add(() => { capturedDim.FamilyLabel = capturedRef; });
}
```

This doubles the action list. `ExecuteDimensionReassignments` increments `dimCount` per action — so semantics shift unless we adjust. Two options:
- **(A)** Keep `dimCount` semantics at "successful reassignments" by pairing two actions per dimension and dividing `dimCount` by 2 at the end. Ugly.
- **(B)** Change `ExecuteDimensionReassignments` signature: accept `IReadOnlyList<(Action clear, Action assign)>` and increment `dimCount` once per pair. Cleaner. ≤ 5 LOC change.

**Recommendation:** option (B).

**RED test design:**
- Fixture: `BatchRenameExecutionServiceTests` (or add to `BatchRenameSafeRenameTests` since it's REQ-03 follow-up)
- Name: `ExecuteDimensionReassignments_PreClearsLabel_BeforeReassigning`
- Test:
```csharp
var log = new List<string>();
var pairs = new[]
{
    ((Action)(() => log.Add("clear-A")), (Action)(() => log.Add("assign-A"))),
    ((Action)(() => log.Add("clear-B")), (Action)(() => log.Add("assign-B"))),
};
BatchRenameExecutionService.ExecuteDimensionReassignments(pairs, "old", "new", out int dimCount);
dimCount.Should().Be(2);
log.Should().Equal("clear-A", "assign-A", "clear-B", "assign-B");
```
- Manual Revit verification (small checklist row): rename a dimension-driving parameter on a family where the dimension already had a different label assigned; verify reassignment succeeds (no "cannot overwrite" warning).

### Polish Fix #3: Standard-item progress capping below 100%

**Code location:**
- `BatchRenameExecutionService.cs:43–44` — `int total = items.Count;` and `int current = 0;` captured before pre-flight.
- `BatchRenameExecutionService.cs:85` — `current++;` runs every iteration including unchecked.
- `BatchRenameExecutionService.cs:86` — `double percent = (double)current / total * 100;`
- `BatchRenameExecutionService.cs:175` — family loop uses `(double)familyIndex / byFamily.Count * 100` — DIFFERENT denominator.
- `BatchRenameExecutionService.cs:244` — `reporter.Report("Done", 100);` final.

**The bug:** Standard loop caps at `standardItems.Count / total * 100` which is < 100 for mixed batches. Family loop resets to 1/N..100 scale. Discontinuous progress.

**Minimal fix:** Unify both loops to share `current` and `total`:
```csharp
// Family loop (rewrite ~:171–178):
foreach (var kvp in byFamily)
{
    current += kvp.Value.Count;   // advance by row-count contributed by this family
    double percent = (double)current / total * 100;
    ...
}
```
After both loops, `current == total` exactly (assuming no items lost to grouping), so final report hits 100% naturally.

**RED test design:**
- Fixture: `BatchRenameExecutionServiceTests`
- Cannot exercise full method without `ITransactionService` proxy. Best path is to **extract a progress helper** `ComputeProgressPercent(int current, int total) → double` (1 LOC, trivial) AND a **sequence helper** that simulates the loops — but the loops are too entangled with Revit to extract cleanly.
- **Recommended:** extract pure-data helper `BuildProgressSequence(int standardCount, int familyGroupRowCounts[]) → IReadOnlyList<double>` (~15 LOC) that returns the percent sequence the loops *would* emit. RED test asserts max value in the sequence == 100.0 (currently it would cap below 100 mid-flight). Then refactor the actual loops to call the helper or share its arithmetic.
- Alternative: **Skip-gated RED** with reason `"requires IProgressReporter call-sequence capture via ITransactionService proxy — covered by manual Revit verification"`.
- Manual Revit verification: run a mixed standard+family batch (e.g., 5 Sheets + 3 family parameters across 2 families). Watch the progress bar — RED behavior: caps below 100 between standard-finish and family-start. GREEN: monotonic 0→100.

## Validation Architecture

> CONTEXT does not mention `nyquist_validation`. Project config (`.planning/config.json`) does not set `workflow.nyquist_validation`. Treating as **enabled** per phase-researcher default.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.6.5 + FluentAssertions 6.12.0 + NSubstitute 5.1.0 |
| Config file | `LECG.Tests/LECG.Tests.csproj` (TargetFramework: `net8.0-windows`, x64) |
| Quick run command | `dotnet test LECG.Tests/LECG.Tests.csproj --filter "FullyQualifiedName~<FixtureName>"` |
| Full suite command | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` |
| Internals access | `src/InternalsVisibleTo.Tests.cs` exposes `internal` members to `LECG.Tests` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-05 | `FormulaUpdateService` delegates to `FormulaNameUpdater` | unit | `dotnet test --filter "FormulaUpdateServiceTests"` | ✅ |
| REQ-05 | `RenameRulePipelineService.ApplyRules` composes rules in Remove→Replace→Case→Add→Numbering order | unit | `dotnet test --filter "RenameRulePipelineServiceTests"` | ❌ Wave 0 |
| REQ-05 | `RenameRulePipelineService` passes correct `index` arg to every rule | unit | (same fixture) | ❌ Wave 0 |
| REQ-05 | `SearchReplaceService` delegates each public method to the correct dependency | unit | `dotnet test --filter "SearchReplaceServiceTests"` | ❌ Wave 0 |
| REQ-05 | `SearchReplaceService` passes through return values unchanged | unit | (same fixture) | ❌ Wave 0 |
| REQ-05 | `SearchReplacePreviewService` scope filter / name filter / FamilyParameter status / collision | unit | `dotnet test --filter "SearchReplacePreviewServiceTests"` | ✅ |
| REQ-05 | `BaseElementCollectionService` no-blanks invariant via SSoT helper | unit | `dotnet test --filter "BaseElementCollectionServiceTests"` | ✅ (4 tests; deepen to 8–12) |
| REQ-05 | `BaseElementCollectionService` ParamGroup try/catch returns "" on failure | unit | (deepened fixture) | ❌ Wave 1 |
| REQ-05 | `BatchRenameExecutionService.EvaluateFamilyParamSkipReason` 4 branches | unit | `dotnet test --filter "BatchRenameExecutionServiceTests"` | ❌ Wave 0 |
| REQ-05 | `BatchRenameExecutionService.EvaluateStandardItemSkipReason` 6 branches | unit | (same fixture) | ❌ Wave 0 |
| REQ-05 | `BatchRenameExecutionService.FormatSafeRenameLog` 4-branch matrix | unit | (same fixture) | ❌ Wave 0 |
| REQ-05 | `BatchRenameExecutionService.GroupCheckedFamilyParameterItems` filters unchecked | unit | (same fixture) | ❌ Wave 0 |
| REQ-05 | `LegacyProgressReporter` null-callback no-throw on all 5 methods | unit | (same fixture) | ❌ Wave 0 |
| REQ-05 | `BatchRenameExecutionService` ctor null-guards (`formulaUpdateService`) | unit | (same fixture) | ❌ Wave 0 |
| Polish #1 | `count`-on-rollback — extracted `AccumulateCommittedFamilyCount` helper RED→GREEN | unit | `dotnet test --filter "ExecuteBatchRename_FamilyTransactionRollsBack"` | ❌ Wave 3 |
| Polish #2 | Dimension null-clear sequencing — `ExecuteDimensionReassignments` pair-action variant | unit | `dotnet test --filter "ExecuteDimensionReassignments_PreClearsLabel"` | ❌ Wave 3 |
| Polish #3 | Standard-item progress hits 100% — `BuildProgressSequence` helper | unit | `dotnet test --filter "BatchRenameProgress"` | ❌ Wave 3 |
| Polish #1 | `count`-on-rollback live behavior | manual | n/a — 05-VERIFICATION.md checklist | manual |
| Polish #2 | Dimension reassignment with pre-existing label live behavior | manual | n/a — 05-VERIFICATION.md checklist | manual |
| Polish #3 | Progress bar reaches 100% on mixed/family batch live behavior | manual | n/a — 05-VERIFICATION.md checklist | manual |

### Sampling Rate
- **Per task commit:** `dotnet test LECG.Tests/LECG.Tests.csproj --filter "FullyQualifiedName~<FixtureName>" -p:SkipRevitDeploy=true` (< 5 sec per fixture)
- **Per wave merge:** `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` (full suite, < 30 sec)
- **Phase gate:** Full suite green + matrix in 05-VERIFICATION.md all ✅ before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `LECG.Tests/Services/RenameRulePipelineServiceTests.cs` — new fixture, anchor + skip-gated RED tests naming Wave 1 plan
- [ ] `LECG.Tests/Services/SearchReplaceServiceTests.cs` — new fixture, anchor + skip-gated RED tests naming Wave 2 plan
- [ ] `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs` — new fixture, anchor + skip-gated RED tests naming Wave 2 plan (skip-gated) and Wave 3 polish plan (skip-gated RED for the 3 bugs)
- [ ] Deepening rows in `LECG.Tests/Services/BaseElementCollectionServiceTests.cs` — anchored existing fixture; skip-gated RED rows naming Wave 1 plan
- [ ] `05-VERIFICATION.md` — service→fixture coverage matrix scaffold (rows present, ✅/⚠/❌ as appropriate)
- No framework install needed — xUnit + FluentAssertions + NSubstitute already in `LECG.Tests.csproj`.

### Service → Fixture Coverage Matrix (target structure for 05-VERIFICATION.md)

| Service | Fixture | Scenarios | Status |
|---------|---------|-----------|--------|
| FormulaUpdateService | FormulaUpdateServiceTests | delegation × 3 inputs; empty input no-throw; constructor injection contract | ✅ |
| RenameRulePipelineService | RenameRulePipelineServiceTests | rule order (Remove→Replace→Case→Add→Numbering); index propagation; null-throw guards; inactive-rules pass-through; null-argument guards | ❌ → ✅ Wave 1 |
| SearchReplaceService | SearchReplaceServiceTests | CollectBaseElements delegation; GetUniqueCategories delegation; ProcessPreview delegation + CancellationToken passthrough; ExecuteBatchRename two-overload delegation; ctor null-guards | ❌ → ✅ Wave 2 |
| SearchReplacePreviewService | SearchReplacePreviewServiceTests | Category propagation; ParamGroup/IsInstance/IsReadOnly; skip-reason population; side-effect count; cross-batch collision (8 tests) | ✅ |
| BaseElementCollectionService | BaseElementCollectionServiceTests | non-blank invariant via GetLabelsFromRaw (4 existing); TryGetGroupLabel pure helper; DispatchScopeFlags pure helper; skip-gated RED for Revit-iteration branches | ⚠ → ✅ Wave 1 (deepen) |
| BatchRenameExecutionService | BatchRenameExecutionServiceTests (NEW) + BatchRenameSafeRenameTests (existing) | New: 4-branch EvaluateFamilyParamSkipReason; 6-branch EvaluateStandardItemSkipReason; FormatSafeRenameLog 4-branch; GroupCheckedFamilyParameterItems; LegacyProgressReporter null-callback; ctor null-guards. Existing (9): formula collection / dim reassignment / pre-flight / collision. Polish: count-on-rollback (extracted helper); dim null-clear (pair-action helper); progress-cap (sequence helper). | ❌ → ✅ Wave 2 + Wave 3 |

## Open Questions

1. **Should the dim null-clear ship as a signature change to `ExecuteDimensionReassignments`?**
   - What we know: Phase 4 designed the `List<Action>` seam exactly for testability; current signature is `IReadOnlyList<Action>`.
   - What's unclear: Whether to keep the existing 9 REQ-03 tests passing unchanged (yes — they use the single-action overload) or break them (no — keep both overloads).
   - Recommendation: Add an overload `ExecuteDimensionReassignments(IReadOnlyList<(Action clear, Action assign)>, string, string, out int)` and have the production caller use the pair overload. Existing single-action overload stays for backward compat (no test churn).

2. **Should the `count`-on-rollback fix extract a helper, or refactor `ExecuteBatchRename` directly?**
   - Recommendation: extract `AccumulateCommittedFamilyCount(bool committed, int renamedInFamily, int currentCount) → int` per §Polish Fix #1 option (B). 3 LOC. Pure RED→GREEN cycle, no live-Revit dependency for the test.

3. **`BatchRenameExecutionService` 944 LOC is too large — refactor in scope?**
   - CONTEXT defers facade collapse / async / scope-UX to v1.2 Phase 6.
   - Recommendation: NO further refactor in Phase 5 beyond the 3 polish fixes + their minimal extraction. Don't drift.

4. **`SearchReplaceService` facade tests — assert via `Substitute.Received(1)` or via return-value equality?**
   - CONTEXT pitfall warns against call-structure coupling. Recommendation: prefer return-value equality (e.g., construct a list, have the substitute return it, assert facade returned the same list reference). Use `Received(1)` only for void methods or `Document doc` arg passthrough verification.

## Sources

### Primary (HIGH confidence) — codebase reads
- `src/Services/Renaming/BatchRenameExecutionService.cs` (944 LOC, full read) — confirmed `:222` is the real `count`-on-rollback site; `:347–363` is the dimension reassignment loop; `:43–88, :171–178, :244` are the progress-loop sites.
- `src/Services/Renaming/BaseElementCollectionService.cs` (335 LOC, full read) — confirmed 9-branch scope dispatch, ParamGroup try/catch at `:234` + `:304`, Phase A/B param scan.
- `src/Services/Renaming/SearchReplaceService.cs` — confirmed 4-method facade with 3 injected dependencies.
- `src/Services/Renaming/SearchReplacePreviewService.cs` (235 LOC) — confirmed pure-data pipeline; 8 tests adequate.
- `src/Services/Renaming/RenameRulePipelineService.cs` (22 LOC) — confirmed 5-rule composition order.
- `src/Services/Renaming/FormulaUpdateService.cs` (14 LOC) — confirmed pure pass-through.
- `src/Services/Renaming/RenameRules.cs` — confirmed `IRenameRule` interface; 5 concrete rules.
- `src/Services/Infrastructure/LegacyProgressReporter.cs` — confirmed null-callback no-throw paths on all 5 methods.
- `src/InternalsVisibleTo.Tests.cs` — `[assembly: InternalsVisibleTo("LECG.Tests")]`.
- `LECG.Tests/LECG.Tests.csproj` — xUnit 2.6.5 + FluentAssertions 6.12.0 + NSubstitute 5.1.0; `-p:SkipRevitDeploy=true` build switch.
- `LECG.Tests/Services/BatchRenameSafeRenameTests.cs` (272 LOC) — Phase 04 RED→GREEN patterns.
- `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` (306 LOC) — 8 tests, RenameRuleContext record construction pattern.
- `LECG.Tests/Services/FormulaUpdateServiceTests.cs` (76 LOC) — anchor pattern + reflection-over-NSubstitute pattern.
- `LECG.Tests/Services/BaseElementCollectionServiceTests.cs` (79 LOC) — SSoT-helper test pattern.
- `.planning/STATE.md` — Phase 03/04 decisions (12 decisions referenced).
- `.planning/phases/05-verification-and-polish/05-CONTEXT.md` — locked decisions.
- `.planning/REQUIREMENTS.md` — REQ-05 Pending; REQ-02/03/04 Complete.
- `.planning/ROADMAP.md` — Phase 5 boundary; v1.2 deferrals.
- `.planning/v1.1-MILESTONE-AUDIT.md` — gap that drives Phase 5.

### Secondary (MEDIUM)
- None — all findings from direct codebase read.

### Tertiary (LOW)
- None.

## Metadata

**Confidence breakdown:**
- Service inventory & testability: HIGH — all 6 service files read in full.
- Polish-fix locations: HIGH — `count`-on-rollback verified at `:222` (CONTEXT's `:185` is stale; planner must use `:222`); dimension null-clear seam verified at `:347–363`; progress loops verified at `:43–88` + `:171–178` + `:244`.
- Test patterns: HIGH — three reference fixtures read in full, all six Phase-04 patterns documented from code comments.
- Validation Architecture: HIGH — xUnit/FluentAssertions/NSubstitute versions verified from csproj; build command verified from Phase 03-00 STATE.md decision.

**Research date:** 2026-05-10
**Valid until:** 2026-06-09 (30 days — internal codebase, stable; will need refresh if Phase 4 plans are reworked or if v1.2 begins refactoring `BatchRenameExecutionService`)
