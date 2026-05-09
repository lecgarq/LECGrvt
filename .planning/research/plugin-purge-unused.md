# Plugin Audit: Purge Unused

## What it does (one paragraph)

Purge Unused is a dual-mode cleanup command. In **Safe** mode (`PassCount == 1`) it scans the project once and removes user-selected unused styles/patterns/materials/levels and a grab-bag of extras (groups, grid/level types, constraints, unplaced rooms, view templates/filters, family parameters). In **Deep** mode (`PassCount == 3`) it ignores every category toggle, runs Revit's native `Document.GetAllUnusedElements` 3 times against every editable loaded family (round-tripping through `EditFamily`/`LoadFamily`) and 3 times against the project, then optionally also runs the family-parameter purge. Both modes auto-dismiss Revit `TaskDialog`s during execution to suppress LoadFamily geometry-validation popups.

## Architecture map

- `src/Commands/PurgeCommand.cs:44` — `Execute()` opens dialog → validates → subscribes `OnDialogShowing` → branches on `PassCount`
- `src/Views/PurgeView.xaml` + `src/Views/PurgeView.xaml.cs` — WPF dialog, 13 checkboxes + Deep toggle
- `src/ViewModels/PurgeViewModel.cs` — observable bools + Check/UncheckAll commands (no `IsDeepPurge` in CheckAll!)
- `src/Models/PurgeDialogSettings.cs:19` — `PassCount => IsDeepPurge ? 3 : 1` (the only place pass count comes from)
- **Safe path:** `IPurgeService` (`PurgeService.cs:47`) → `IPurgeExecutionCoordinatorService` (`PurgeExecutionCoordinatorService.cs:49`) → loop `IPurgePassExecutionService.ExecutePass` (`PurgePassExecutionService.cs:56`) → 5 category services + `IPurgeExtendedElementService`
- **Deep path:** `IDeepPurgeService` (`DeepPurgeService.cs:30`) → `INativePurgeDocumentService.PurgeUnused` (`NativePurgeDocumentService.cs:10`)
- **Shared:** `PurgeContext.Create` (`PurgeContext.cs:37`) builds reference indexes (parameter refs, materials, line styles, fill patterns, levels) used by safe-path category services
- **Family params:** `PurgeParameterService.cs:37` — runs in both modes when toggled, opens each editable family with `EditFamily`

## Findings by axis

### Functionality

- **Deep purge silently ignores every category checkbox.** `PurgeCommand.cs:65-86` — if `PassCount > 1`, the user's selection of "Purge Levels", "Purge View Filters", etc. is thrown away and only Revit's native purge runs. The view does not communicate this; the only hint is `Log("Category-specific purge toggles are ignored during deep purge.")` after the fact (line 68). UX implication is large because `IsDeepPurge` defaults to `true` (`PurgeViewModel.cs:60`).
- **`CheckAll`/`UncheckAll` don't toggle `IsDeepPurge`** (`PurgeViewModel.cs:9-42`). Probably intentional, but combined with the above means clicking "Check All" with the default Deep state checked makes 12 of the 13 checkboxes inert.
- **`PurgeContext.Create` is rebuilt every pass.** `PurgePassExecutionService.cs:91-92` instantiates a new `PurgeContext` per pass even though deletions only invalidate a small subset. For 200K-500K element models the comment in `CompactingStylesContext.cs:80` warns about exactly this (it skips instances entirely there) — but `PurgeContext.cs:108` *does* iterate `WhereElementIsNotElementType()` over all instances (line 108) and again across `CollectInstanceMaterialParameterReferences` (line 292) to scan parameters. This is the single most expensive operation and runs 3× in Safe mode (Deep mode skips it).
- **`BuildFormulaReferencedSet` is O(N×M) substring search** (`PurgeParameterService.cs:347-378`). For each formula it scans every parameter name with `IndexOf`. A formula `"Width * 2"` will match a parameter literally named `"i"` because of substring containment — false positives **preserve garbage**, which is the safer error direction, but means the count of "deletable" params is biased low. Also tokenless: a parameter named `Length` triggers on a formula `if(LengthOverride > 0, …)`.
- **`BuildElementAssociationSet` enumerates `el.Parameters` on every non-type element in the family doc** (`PurgeParameterService.cs:421-446`). That's a documented hazard the project itself flags in `PurgeContext.cs:51` ("native access violations"). Slightly safer because it only runs inside an opened family doc (smaller scope), but it's the same anti-pattern.
- **Constraint purge deletes whatever it can without distinguishing user constraints from sketch/system constraints** (`PurgeExtendedElementService.cs:80-121`). Catches `ArgumentException`/`InvalidOperationException` to skip un-deletable ones, but *does* delete user-authored EQ/dimension lock constraints. Destructive and not really "unused" — it's "all".
- **Auto-dialog dismissal cancels EVERY TaskDialog raised during purge** (`PurgeCommand.cs:36-42`). Comment claims this is for "Extrusion is too thin", but the indiscriminate `e.OverrideResult(2)` will also suppress legitimate dialogs the user might need to see (e.g. CAD link warnings). The mitigation that "result 2 = cancel = preserves geometry" is true for many but not all dialogs.
- **`NativePurgeDocumentService.PurgeUnused` ignores results from `doc.Delete`'s collateral deletions.** `NativePurgeDocumentService.cs:27-31` increments `deletedCount` once per top-level `Delete` call regardless of how many elements were actually removed. Reported counts are misleading low.
- **Level purge silently no-ops for projects with ≤1 level** (`PurgeLevelService.cs:36-40`). Fine. But it also calls `ElementLevelFilter` per candidate level inside a loop (`PurgeLevelService.cs:52-55`) — N collector calls. Acceptable given level count is small.
- **Half-finished `_progressCallback` interface coexists with `IProgressReporter`.** Both `IPurgeService` (`IPurgeService.cs:8-9`) and `IPurgeExecutionCoordinatorService` (`PurgeExecutionCoordinatorService.cs:42`) and `IPurgePassExecutionService` (`PurgePassExecutionService.cs:50`) and `IPurgeSummaryService` (`PurgeSummaryService.cs:47`) and `IPurgePassMessagingService` (`PurgePassMessagingService.cs:23-34`) expose **two** signatures (callback + reporter) with the legacy callback wrapped via `LegacyProgressReporter`. Migration was started and abandoned.

### Error handling

- **WPF dialog catch in `PurgeCommand.cs:149-153` falls through to `LecgDialog.ShowOptions` fallback**, then continues running with a possibly-null settings — actually `null` is checked (`line 52`), so OK, but the fallback offers only 2 options vs 13 checkboxes, dramatically worse UX after a transient WPF failure.
- **`SafeFailureHandler` rolls back transactions on errors** (`PurgeExecutionCoordinatorService.cs:88`) — good. But the deep-purge family path uses a separate handler (`DeepPurgeService.cs:131`) so each family is isolated, and individual family failures only log a warning (`DeepPurgeService.cs:159`). Acceptable.
- **`NativePurgeDocumentService.cs:33-37` swallows `catch { }` — bare catch.** No log, no count, no telemetry. Combined with the deletedCount undercount above, deep purge errors are invisible.
- **`PurgeExtendedElementService.PurgeConstraints` swallows ArgumentException/InvalidOperationException silently** (`PurgeExtendedElementService.cs:105-116`) — comment says "to avoid log noise" but in a forensic situation you can't tell which constraint refused to die.
- **Reload failure handlers (`DeepPurgeService.cs:216-234` and `PurgeParameterService.cs:309-324`) are duplicated** and both delete all warnings then roll back if errors remain. This rollback throws away the parameter deletions just made — silently.
- **`OnDialogShowing` is unsubscribed in `finally`** (`PurgeCommand.cs:90-92`). Good, no leak.
- **`PurgeParameterService.ProcessFamily` calls `TryCloseFamilyDocument` on the success path AND in the catch** but not via `try/finally` — if `DeleteParameters` throws an exception **not** in `IsExpectedFamilyPurgeException` (e.g. `OutOfMemoryException`, `AccessViolationException` wrapped as managed), the family doc leaks. Same shape in `DeepPurgeService.TryPurgeFamily` which **does** use `finally` (`DeepPurgeService.cs:162-165`) — inconsistent.

### Performance

- **Triple-pass `PurgeContext.Create` over instance elements** is the biggest hit. `PurgeContext.cs:55, 61, 66, 108, 292, 297` — at least three full `WhereElementIsNotElementType()` collector materializations per pass. With Safe mode = 1 pass that's 3 sweeps. The bypass for instance scan via `ElementParameterFilter` in `CollectBuiltInMaterialParameterReferences` (`PurgeContext.cs:331`) is the right pattern; it's not applied to `CollectInstanceReferences` (`PurgeContext.cs:108`).
- **`CollectFillPatternReferences` re-runs the Material collector** (`PurgeContext.cs:118`) even though `CollectTypeReferences` already iterated all types (Materials are types). Two passes over the same set.
- **Family-parameter purge opens every family even with no matches.** `PurgeParameterService.cs:104` calls `EditFamily` per family before checking whether anything is deletable. EditFamily is expensive (parses BCF, instantiates a new doc). Could pre-screen by `family.GetFamilyTypeIds()` and skip families with zero non-built-in params (cheap to inspect at the project level via `FamilyType.LookupParameter` proxies… not perfect but a screen).
- **`BuildFormulaReferencedSet` O(P²) substring scan** noted above. Cap is small per-family but cumulative across hundreds of families it adds up.
- **`BuildElementAssociationSet` enumerates `el.Parameters` for every element in the family doc.** A heavy family (e.g. parametric curtain panel) can have thousands of nested elements.
- **Deep purge always runs `passCount` times even when a pass returns 0.** `DeepPurgeService.cs:179-188` and `PurgeExecutionCoordinatorService.cs:89-123` — should short-circuit when a pass deletes nothing (Revit's native purge is idempotent after the first stable state).
- **No batching of `doc.Delete`.** Individual `Delete` calls per material/style; `doc.Delete(ICollection<ElementId>)` overload would let Revit batch the regen.
- **`doc.Regenerate()` after each native pass** (`NativePurgeDocumentService.cs:40`) — N regens for N passes. Forced regens are expensive; once at the end of the transaction sequence is usually enough.

### UX / UI

- **No progress bar UI in the dialog itself**, just the log window after it closes. Fine, that's project convention.
- **The Deep Purge checkbox visually looks like just another option** (`PurgeView.xaml:56`) but actually disables 12 of the 13 checkboxes above it. There's a separator but no warning, no disabled state, no tooltip explaining the mode swap.
- **Dialog default state is dangerous.** `PurgeViewModel.cs:60` defaults `IsDeepPurge = true` and `PurgeLineStyles/Patterns/FillPatterns/Materials = true`. A new user clicking "Purge" runs deep purge (3-pass, opens every editable family, modifies project) without realizing the checkboxes were ignored.
- **"Unused Family Parameters (Warning)"** has a tooltip (`PurgeView.xaml:54`) but no visual warning treatment (no red text, no icon). The word "(Warning)" in the label is the only signal.
- **No estimate of cost.** A 500MB project with 800 families will sit on Deep+Parameters for 30+ minutes with no preflight estimate.
- **Cancel button only cancels the dialog, not in-flight purge.** No mid-run cancel mechanism (would require Revit external event plumbing — large effort).
- **Window title is just "PURGE"** (`PurgeView.xaml:5`) which is less descriptive than the body header.
- **"All Constraints" label** is honest but the tooltip would help users understand it's not "unused constraints" but "delete every constraint".

### Log experience

- **Generally good:** pass headers (`PurgePassMessagingService.cs:11`), per-category section markers, `=== SUMMARY ===` block (`PurgeSummaryService.cs:27`), per-deletion lines.
- **`LogIfActive` skips zero-count categories** (`PurgeSummaryService.cs:67-70`) — clean summary, but if a user toggled "Purge View Templates" expecting deletions and got 0, the summary doesn't acknowledge they asked for it. Hard to tell "didn't run" from "ran and found nothing".
- **Auto-dismissed dialogs are logged** (`PurgeCommand.cs:41`) but with no count — could be hundreds of "Auto-dismissed dialog during purge: extrusion too thin" lines. Log spam for large families.
- **Per-deleted-element lines** (`PurgeDeleteElementService.cs:33`) — `Deleted: <name>`. Fine for Safe mode (small N) but Deep+native can produce thousands of these from family docs. No dedup, no rollup.
- **`PurgeContext` warnings** all use `Logging.Logger.Instance.LogWarning` directly (`PurgeContext.cs:176, 204, …`) bypassing the reporter. They show up in the global log but not the purge log window — split-brain logging.
- **No timing.** Doesn't log "Pass 1 took 47s". Hard to diagnose slow models.
- **`Log("")` for spacing** (`PurgeCommand.cs:75`, `PurgeSummaryService.cs:26`) instead of a structured separator. Cosmetic.
- **`Log("Purge Complete.")` runs even if the inner branch threw** — wait, no, `try/finally` only covers the dialog handler unsubscribe; an exception bubbles up. Actually re-reading `PurgeCommand.cs:58-96`: the `try { … } finally { unsubscribe }` *will* let exceptions propagate, so "Purge Complete" *won't* log on failure. But there's also no `catch` — so the user sees the Revit exception dialog with no friendly summary.

### Technical debt / bloat

- **18 service files for Purge** (`Glob` count above). Compare functionality: 6 main categories + 7 extended categories + 1 deep + 1 parameters. The split is far finer than the behavioral diversity:
  - `PurgeService` (line 11): pure delegation shell. 124 lines that just forwards to coordinator + summary. Could be inlined.
  - `PurgeExecutionCoordinatorService` (138 lines): also mostly delegation. Holds a 13-tuple return that's destructured in `PurgeService.PurgeAll` (`PurgeService.cs:49`) into 13 named local vars then passed to `PurgeSummaryService.Report` as 13 positional args. **Add a 14th category and you touch ~10 files.**
  - `PurgePassMessagingService` (37 lines): two methods, both have callback + reporter overloads. Could be 5 lines of inline code.
  - `PurgePassSequenceService` (14 lines): a 1-line wrapper around `LECG.Core.Purge.PurgeSequence.GetPasses`.
- **Dual API surface (callback vs reporter) doubles the surface of every service** with no consumer of the legacy callback shape that I can see in the command path. `LegacyProgressReporter` is the bridge but it's used to convert *callbacks back into reporters* — purely defensive.
- **13-positional-arg methods** (`IPurgeService.cs:8-9`, `PurgeSummaryService.cs:8-22`, etc.) are a maintenance nightmare. Should be a `PurgeOptions` record + `PurgeResult` record.
- **`PurgeReferenceScannerService.cs` (55 lines) is essentially dead code.** `CollectUsedIds` and `AddIfValid` exist as a service but the actual context construction (`PurgeContext.cs`) reimplements both inline (`AddIfValid` private at line 82, parameter scan inline at line 146).
- **`PurgeMaterialUsageCollectorService` (45 lines)** is a thin wrapper over `PurgeContext` projections. Filter-and-copy of two hashsets — 8 useful lines.
- **`CompactingStylesContext.InstanceElements` is always `Array.Empty<Element>()`** (`CompactingStylesContext.cs:82`) and the parameter index iterates over an empty list (`CompactingStylesContext.cs:179`). The whole `ParameterIndex` field for Compacting context is essentially unused output. Stale field carrying dead intent.
- **Repeated exception-filter helper** `IsExpected*Exception` defined separately in `PurgeCommand` (line 179), `DeepPurgeService` (line 236), `PurgeParameterService` (line 520), `PurgeExtendedElementService` (inline catches). Same three exception types each time.
- **Repeated reload-with-failure-handler pattern** in `DeepPurgeService.ReloadPurgedFamily` (line 193) and `PurgeParameterService.ReloadFamily` (line 304). Identical structure, both swallow rollback silently.
- **Both code paths build `SafeFailureHandler` per pass** (`PurgeExecutionCoordinatorService.cs:88`, `DeepPurgeService.cs:92, 131`). One reusable instance would be fine.
- **Stale comment** in `PurgeService.cs:9` — "Refactored to use RevitConstants and cleaner logic" — refactor history doesn't belong in a docstring.

### Architecture concerns

- **`PurgeCommand.Execute` does its own service location** (`ServiceLocator.GetRequiredService<IPurgeService>()` on line 84, `IDeepPurgeService` on line 70, `IPurgeParameterService` on line 77, `PurgeView` on line 103). Mixed mode: ctor-injected services everywhere else, but the command resolves at runtime. If the locator is mid-init (cold open), the command crashes inside `Execute`.
- **`PurgeView` is registered in DI and `view.ShowDialog()` is called from background-flow code** (`PurgeCommand.cs:103, 120`). Resolving a WPF view from DI at command time means it gets a fresh `PurgeViewModel` each invocation; the explicit `vm.PurgeLineStyles = loaded.PurgeLineStyles; …` block (lines 105-118) reimplements binding because the loaded settings can't be passed to the VM constructor through DI.
- **`PurgeContext` knows about Revit `Logger.Instance`** (`PurgeContext.cs:176` etc.), bypassing the reporter passed by the caller. Two log destinations.
- **`PurgeContext.Create` mixes type, instance, category, and binding scans in one method** with no per-section progress reporting. A user watching the log sees nothing for the 30-60s this takes on a big model.
- **Deep purge orchestrator and Safe purge orchestrator share zero code** even though both:
  - iterate passes via `IPurgePassSequenceService`
  - run inside `_transactionService.RunWithWarningHandler` with `SafeFailureHandler`
  - report progress per pass
  The pass-sequence-with-handler pattern (`DeepPurgeService.RunPurgePassSequence` at line 168) could be the only orchestrator with the per-pass body as a delegate.
- **`PurgeParameterService` decides safety via 10 heuristics in `GetSkipReason`** (`PurgeParameterService.cs:174-232`). Hard-coded `"Enscape"` substring (line 203) for third-party filter — won't catch Twinmotion, Lumion, Unifi, etc. List should be config-driven.
- **`SafeFailureHandler` is referenced (`PurgeExecutionCoordinatorService.cs:88`) but defined outside the purge folder** — coupling to a global handler that other commands also use. Behavioural drift across commands could break purge.

## Recommended upgrade themes (top 5, ranked)

### 1. Make Deep mode honor (or visibly disable) the category checkboxes — S
**Why:** Today Deep + checkboxes is a UX trap. Either (a) gate the checkboxes' `IsEnabled` to `!IsDeepPurge` with explanatory text, or (b) actually compose Deep + custom: run native purge first, then run the safe-path categories the user picked. Option (a) is one XAML binding + a converter. Option (b) is a few hours of orchestration.

### 2. Replace the 13-positional-arg API with `PurgeOptions`/`PurgeResult` records — M
**Why:** `IPurgeService.PurgeAll(...)`, `PurgeExecutionCoordinatorService.Execute(...)`, `PurgeSummaryService.Report(...)` and `PurgePassExecutionService.ExecutePass(...)` all take 13-15 positional bools/ints. Adding a category now requires touching 6 signatures, 6 implementations, 1 interface, the command, and the summary. A `PurgeOptions` record + `PurgeResult` record + `IReadOnlyDictionary<PurgeCategory, int>` for counts compresses this dramatically and makes it possible to log "ran but found nothing" per category.

### 3. Performance: deduplicate `PurgeContext` work and drop full instance scans — M
**Why:** `PurgeContext.Create` runs once per pass and sweeps every non-type element 2-3 times. For a 500K-element model that's ~30-60s of pure overhead per pass. Apply the same `ElementParameterFilter` pattern already used in `CollectBuiltInMaterialParameterReferences` to the broad scans in `CollectInstanceReferences`. Cache `PurgeContext` across passes and only rebuild the deltas (or stop rebuilding entirely — Safe mode could be 1-pass-only since 2/3 of the work is reference indexing, not deletion).

### 4. Collapse the service zoo and kill the dual callback/reporter API — M
**Why:** ~7 of the 18 service files are pass-through shells (`PurgeService`, `PurgeExecutionCoordinatorService`, `PurgePassMessagingService`, `PurgePassSequenceService`, `PurgeReferenceScannerService`, `PurgeMaterialUsageCollectorService`, `PurgeSummaryService`). Eliminate `Action<string> log + Action<double,string> progress` overloads everywhere, keep only `IProgressReporter`, delete `LegacyProgressReporter` and the duplicate methods. Probable 30-40% LoC reduction with no behavioral change.

### 5. Surface real status: timing, cancellation, real counts, and a "what would change" preflight — L
**Why:** Today the user sees a checkbox dialog → log spam → hopefully a summary. Add (a) per-pass timing, (b) accurate `deletedCount` from `doc.Delete`'s ICollection return (`NativePurgeDocumentService.cs:27-31` undercounts), (c) a dry-run mode that builds `PurgeContext` and reports "would delete N materials" before committing, and (d) cancellation via Revit's `IExternalEventHandler` (only relevant for Deep+Parameters which can run minutes). Largest effort but turns the tool from "fingers crossed" into "informed cleanup".

## Out-of-scope observations

- **`SafeFailureHandler`** is consumed by purge but lives in `LECG.Core` (presumably). Behavioral changes by other commands could regress purge silently. Worth a sanity test in the purge test suite.
- **`SettingsManager.Load<PurgeDialogSettings>`** throws on schema drift; the catch in `PurgeCommand.cs:149-153` only filters specific exception types — a corrupted JSON could throw `JsonException` and crash the command.
- **`LecgDialog.ShowOptions` fallback** offers a fundamentally different feature set (2 modes vs 13 toggles) — falling back from a WPF parse error to a 2-button dialog silently degrades the user's options without explanation.
- **`Logger.Instance` global** vs the reporter pattern is an addin-wide split; purge inherits the inconsistency rather than causing it.
- **`CompactingStylesContext`** lives next to `PurgeContext` but is used by the *Compaction* services (separate feature). The two contexts have overlapping intent (index references for cleanup) but no shared abstraction. If a future "compact + purge" combined command emerges, this is a refactor target.
- **`IFamilyLoadOptionsFactory`** is shared with other commands; its `Create()` controls overwrite-vs-keep semantics for family parameters. Behavior surprises here would be attributed to purge but originate elsewhere.
