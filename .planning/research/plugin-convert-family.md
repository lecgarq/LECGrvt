# Plugin Audit: Convert Family

## What it does (one paragraph)

The Convert Family plugin takes user-selected hosted family instances (doors, windows, furniture, etc.) and rebuilds each unique source family on top of a "neutral" template (`LECG_070_GENERIC-MODELS.rft` by default, falling back to the category-specific LECG template, then to a stock Revit Generic Model template). For each source family it: opens the family document, creates a new family document from a template, copies parameters (including formulas and additional types) and geometry across, saves the new family to a temp `.rfa`, loads it back into the project under the *same family name* (overwriting the original), then deletes the original instances and re-places replacements at the captured locations with their captured rotation/flips/parameter values. There is no dedicated WPF view — the command picks instances directly via `PickObjects`, then opens the shared `LogView` for streaming progress.

## Architecture map

### Command -> Service -> View flow

- Entry: `src/Commands/ConvertFamilyCommand.cs:69` `Execute`
  - Hooks `uiApp.DialogBoxShowing += OnDialogShowing` (`ConvertFamilyCommand.cs:79`) — global dialog auto-handler with allow/deny/unknown lists at lines 33-66.
  - Resolves selection: pre-selection at `ConvertFamilyCommand.cs:85-90`, otherwise `uiDoc.Selection.PickObjects` with `LECG.Utilities.FamilyInstanceFilter` at `ConvertFamilyCommand.cs:97-99`.
  - Temporarily unsubscribes the dialog handler around `PickObjects` (`ConvertFamilyCommand.cs:94, 108`) — necessary because the pick prompt is itself a TaskDialog-ish thing.
  - `ShowLogWindow("Converting Families...")` — opens `LogView` via `RevitCommand.ShowLogWindow` (`Core/RevitCommand.cs:70-90`).
  - Calls `service.ConvertFamilyBatch(doc, instances, "", "", false, replaceInPlace: true, reporter)` at `ConvertFamilyCommand.cs:132`.
  - `finally` always unsubscribes (`ConvertFamilyCommand.cs:140`). Good.

- Orchestrator: `src/Services/FamilyConversion/FamilyConversionService.cs:48` `ConvertFamilyBatch`
  - Groups instances by `Symbol.Family.Id` (`FamilyConversionService.cs:58`).
  - For each group → `ProcessFamilyGroup` (line 81): captures sibling instances, opens source doc, calls `ExecuteFamilyGroupConversion` (line 125).
  - `ExecuteFamilyGroupConversion`:
    - If `replaceInPlace`: deletes captured instances *before* execution (lines 144-147) — this is intentional because Revit refuses to overwrite a family while instances reference it.
    - Calls `IFamilyConversionExecutionService.Execute` (line 149).
    - Then `FindReplacementSymbol` (line 158) → `ReplaceCapturedInstances` (line 161).
    - `finally` calls `IFamilyConversionFinalizeService.Finalize` (line 183) which closes both docs and cleans up temp.

- Execution: `FamilyConversionExecutionService.Execute` (`FamilyConversionExecutionService.cs:26`)
  - `IFamilyTargetDocumentService.Create` → `IFamilyGeometryCopyService.CopyGeometry` → `IFamilySaveLoadService.SaveAndLoad`. That's it. 31 lines, three line-level pass-throughs.

- Geometry copy: `FamilyGeometryCopyService.CopyGeometry` (line 25)
  - Collects via `IFamilyGeometryCollectionService.CollectGeometryElementIds` (`FamilyGeometryCollectionService.cs:9`).
  - Inside one transaction (`RunWithWarningHandler` w/ `WarningSwallower`), calls `IFamilyParameterSetupService.ConfigureTargetFamilyParameters` then `ElementTransformUtils.CopyElements` batch with per-id fallback (`FamilyGeometryCopyService.cs:39-62`).

- Save/load: `FamilySaveLoadService.SaveAndLoad` → `FamilySaveService.SaveTemp` (writes to `Path.GetTempPath()/<name>.rfa`) then `FamilyProjectLoadService.Load` (calls `doc.LoadFamily` with `WarningSwallower`).

- View: `src/Views/LogView.xaml(.cs)` (shared, no command-specific UI). Progress reaches it via `RevitCommandProgressReporter` → `_logViewModel.UpdateProgress` (rate-limited at `RevitCommand.cs:127-146`, ~250 ms throttle).

- DI: `src/Core/Bootstrapper.cs:133-148` registers all 14 family-conversion services as singletons (singleton lifetime is fine because none hold mutable state, but see "Architecture concerns").

### On the heavy decomposition

The folder has **17 implementations + 17 interfaces = 34 files, ~1666 LOC** for a flow that is essentially: open source, create target, copy geometry, save, load, replace. About a third of these classes have **fewer than 30 LOC** and contain a single one-line method:

- `FamilySaveLoadService` (24 LOC): two-line method calling two other services.
- `FamilySourceDocumentService` (24 LOC): one call to `doc.EditFamily`.
- `FamilyTargetDocumentService` (31 LOC): one call to `app.NewFamilyDocument` + file-exists guard.
- `FamilyConversionLoggingService` (26 LOC): three `Logger.Instance.Log` wrappers.
- `FamilyConversionFinalizeService` (29 LOC): `Close(false)` x2 + delegate to cleanup.
- `FamilyTempFileCleanupService` (37 LOC): `File.Delete` with stale comments arguing with itself (lines 26-34).
- `FamilyLoadOptionsFactory` (29 LOC): wraps a 4-line nested class.
- `FamilyConversionNamingService` (21 LOC): used in zero call sites — see Functionality.

This decomposition does **not** help. Each new service adds: an interface file, a DI registration, a constructor parameter on its consumer, indirection when reading, and a place where logging conventions diverge. It does not help testability either, because most of these services wrap raw Revit API calls (`doc.EditFamily`, `app.NewFamilyDocument`, `File.Delete`) that cannot be exercised without a running Revit. There's no unit test directory for `FamilyConversion/` anywhere in `LECG.Tests/`.

The `FamilyConversionService` itself is then *seven* injected services + a transaction service (`FamilyConversionService.cs:24-40`), and still does plenty of "real" work (instance capture, deletion, replacement, level resolution, placement) inline. The decomposition pushed trivia out and left non-trivia behind.

## Findings by axis

### Functionality

**Bugs / dead paths**

- **`FamilyConversionNamingService` is dead code.** Injected into `FamilyConversionService` (`FamilyConversionService.cs:18, 28, 37`) but **never called**. The orchestrator hardcodes `targetFamilyName = sourceFamilyName` at `FamilyConversionService.cs:93`, ignoring both the `customName` argument *and* the naming service. The `customName` parameter on `IFamilyConversionService.ConvertFamilyBatch` is therefore also dead — the command always passes `""` (`ConvertFamilyCommand.cs:132`).

- **`isTemporary` is hardcoded false** at `FamilyConversionService.cs:99` and `:183`, so the parameter on the public API is purely decorative. The `FamilyTempFileCleanupService` then hits a long, stale debate-with-itself comment (`FamilyTempFileCleanupService.cs:26-34`) about what to do in the `!isTemporary` branch — the dead branch leaves the `.rfa` in `%TEMP%` forever.

- **Hosted instances are silently dropped during replacement.** `ReplaceCapturedInstances` only handles `LocationPoint`-based instances (`FamilyConversionService.cs:284-288`). Anything `LocationCurve`-based (lines, beams) is captured into `FamilyInstanceData.LocationCurve` (`FamilyInstanceData.cs:39-42`) and *never used* — `Apply` only ever applies parameters/rotation/flips. Original instance is already deleted by this point → silent data loss. The log says "Skipping non-point-based instance" with no instance ID and no count summary.

- **Host relationship is captured but never restored.** `FamilyInstanceData.HostId` (`FamilyInstanceData.cs:18, 28`) is captured but never used in `Apply` (`FamilyInstanceData.cs:69-104`). For "hosted family instances" — the very thing the command's prompt asks for — the new instance is placed free-standing on a level. Wall-hosted doors lose their host.

- **Filter contradicts intent.** The selection filter (`FamilyInstanceFilter.cs:13-16`) admits a family **only if** `FAMILY_WORK_PLANE_BASED == 1` (delegated to `FamilySelectionPolicy.IsSafeToConvert`). But the prompt the user sees is `"Select hosted family instances to convert."` (`ConvertFamilyCommand.cs:98`). Standard Revit hosted families (door/window in walls) are usually *not* work-plane-based. The user is told to pick hosted, but the filter rejects them. After the work-plane parameter is set on the *target* (`FamilyParameterSetupService.cs:30`), the intent is clearer — the tool converts work-plane families to work-plane families — but the prompt text is wrong.

- **`FamilyEditorService.RecreateAs` and `BatchProcess` belong to the wrong feature.** Used only by `CategoryChangerCommand` (`CategoryChangerCommand.cs:115`), but live in `FamilyConversion/`. Mixing leaks unrelated logic into the conversion folder.

- **`ResolvePlacementLevel` fallback is unsafe.** When `LevelId` is invalid, falls back to `ActiveView.GenLevel` then "first level in document" (`FamilyConversionService.cs:335-340`). Silently planting elements on an arbitrary level can corrupt model data with no log indication of the substitution.

- **`FindReplacementSymbol` always picks the first symbol** (`FamilyConversionService.cs:259`). Original family had multiple types? All instances regardless of original type get the first symbol of the new family. Original `Symbol` was captured nowhere on `FamilyInstanceData`.

- **Per-type values are written via `targetFm.CurrentType =` in a loop** (`FamilyParameterSetupService.cs:193`). This works but it's fragile inside a single transaction; if `NewType` succeeds and `CurrentType` switch fails mid-loop, you get inconsistent type state. No assertion of which type ends up active at the end.

- **`CapturedFamilyInstances.InstanceIds` may differ from the user's selection.** The capture re-collects ALL instances of the source family in the project (`FamilyConversionService.cs:202-206`), then deletes all of them (`FamilyConversionService.cs:145`). User picked one door of family X → all 200 doors of family X across the project get replaced. This may be intentional ("you can't update a family with live instances anyway"), but it's not documented and not warned about in the UI. Combined with silent host loss, this is high-risk.

**Half-finished**

- `FamilyTempFileCleanupService` literally contains a TODO-disguised-as-comment debating what `!isTemporary` should mean.
- `BatchProcess` (`FamilyEditorService.cs:244-255`) "Future performance enhancement: consider batching transactions if Revit allows" — currently just a `foreach` calling `ProcessFamily`.

### Error handling

- **Top-level `Execute` has no try/catch around the service call** (`ConvertFamilyCommand.cs:76-141`). It relies on `RevitCommand.Execute(commandData,...)` (`RevitCommand.cs:46-55`) to catch + log. That's OK, but the `finally` at line 137 only unsubscribes; if the service throws after `ShowLogWindow`, the log window stays open with no failure summary banner.

- **`FamilyConversionExecutionService.Execute` swallows expected exceptions** and returns `(null, "")` (`FamilyConversionExecutionService.cs:39-43`). The caller in `ExecuteFamilyGroupConversion` (line 170-174) sees `targetFamilyDoc == null` and logs "ERROR: targetFamilyDoc was null. Conversion failed internally." — but the **original instances have already been deleted** (line 145). Failure here = silent data loss.

- **`IsExpected*Exception` patterns are scattered everywhere** (`FamilyConversionService.cs:327`, `FamilyConversionExecutionService.cs:49`, `FamilyEditorService.cs:257`, `FamilyParameterSetupService.cs:205`, `FamilyTemplatePathService.cs:79`). All slightly different sets of exception types. No central convention. Several `catch` blocks downstream catch `Exception` bare (e.g. `FamilyGeometryCopyService.cs:44, 54`, `FamilyParameterSetupService.cs:163`).

- **`FamilyParameterSetupService` swallows ALL exceptions** in `CopyTypeValues` (line 163-166: `catch { /* may fail */ }`). A formula error on one parameter silently drops it; user has no idea why their door no longer has a hinge offset.

- **`FamilyInstanceData.Apply` swallows all exceptions in the parameter loop** (`FamilyInstanceData.cs:97-101: catch { /* Best effort */ }`). Same problem — a "successful" replacement may have lost half its parameter values with no log.

- **Dialog handler is dangerously aggressive.** `OnDialogShowing` (`ConvertFamilyCommand.cs:25-67`) cancels *unknown* dialogs (line 58) and any non-TaskDialog (line 64). If Revit ever shows a recoverable dialog the author didn't anticipate (e.g. "license expired", "unsaved changes"), it gets clicked Cancel without the user ever seeing it. The substring matching is also brittle: "delete" matches `"Edit type properties without delete"` etc.

- **`FamilySourceDocumentService.Open`** lets `doc.EditFamily` throw upward (line 14, no try). Most callers in `FamilyConversionService.ProcessFamilyGroup` are *not* inside a try, so this exception propagates to `ConvertFamilyBatch` and aborts the whole batch on one bad family. (Counter-intuitive given how much `try/catch` there is elsewhere.)

- **No retry / continue-on-error policy at the batch level.** One failing family may halt the rest depending on where the exception originates.

### Performance

- **5 redundant `FilteredElementCollector` calls** in `FamilyGeometryCollectionService.CollectGeometryElementIds` (`FamilyGeometryCollectionService.cs:14-21`) — one per type. The first uses a captured `collector` variable (line 11), the rest re-instantiate. This isn't huge for a family doc but pattern is sloppy. Also note: `OfClass(FamilyInstance)` will pick up nested family instances, then `OfClass(GenericForm)` and `OfClass(FreeFormElement)` may overlap with what `GeomCombination` exposes — possible double-handling in the copy.

- **O(n) `FindParameterByName` inside an O(n) loop in CopyFamilyParameters** = O(n²) per family parameter set (`FamilyParameterSetupService.cs:213-221` called from line 64). Most families have <30 params, but for parametric families this matters.

- **Type loop calls `CopyTypeValues` per type** (`FamilyParameterSetupService.cs:196`), which itself iterates `sourceFm.Parameters` and rebuilds a `HashSet<string>` of formula names *every iteration* (`FamilyParameterSetupService.cs:122-124`). Should be hoisted out — it's invariant across types.

- **`CaptureFamilyInstances` runs a full `FilteredElementCollector` per family group** (`FamilyConversionService.cs:202-206`). For users selecting many families at once, this is one full-doc scan per group. Could be a single project-wide pre-pass into a `Dictionary<ElementId, List<FamilyInstance>>`.

- **`ReplaceCapturedInstances` calls `doc.Create.NewFamilyInstance` one at a time inside one transaction** (`FamilyConversionService.cs:279-300`). Acceptable but each call also triggers `ApplyCapturedInstanceData` which calls `RotateElement` separately — two regen-points per instance. For 200+ instances this is the slow path.

- **Save → Load → Close → Cleanup is sequential file I/O per family group**, no parallelization possible (Revit single-threaded).

- **Three exception type checks per warning log** (`FamilyConversionService.cs:329-333` etc.) — micro, but each conversion logs dozens of these, and `Logger.Instance.Log` writes to a UI dispatcher.

- **`ShouldPublishProgress` throttles UI updates well** (`RevitCommand.cs:127-146`) — this is good, not a problem.

### UX / UI

- **No dedicated dialog, no preview, no settings.** Compared to `CategoryChangerView`, `ConvertCadView`, etc., Convert Family has zero pre-flight UI. The user gets only:
  1. A pick prompt with the *wrong* description ("hosted family instances" but filter rejects them).
  2. The shared `LogView` with streaming logs and a progress bar.

- **No confirmation step.** A user picking one door of family X has no warning that *all* instances of family X across the project will be deleted-and-recreated, parameters partially preserved, hosts lost, types collapsed to one. This is the single biggest UX risk.

- **Template path is hardcoded** to `English\LECG\-\LECG_070_GENERIC-MODELS.rft` (`FamilyConversionService.cs:196`) with no UI to choose otherwise. A non-LECG installer or non-English Revit silently falls through to `_templatePathService.GetTargetTemplatePath` (`FamilyTemplatePathService`), which tries the standard Revit Generic Model. Output quality degrades silently.

- **Operation is fully blocking** on the Revit UI thread. Long batches freeze Revit; user has no Cancel button (the `LogView` has no cancel hook tied to this command). `OperationCanceledException` is only handled around `PickObjects` (`ConvertFamilyCommand.cs:101`).

- **Final state surprises.** After completion the user is left in the project with new instances — but if any conversion failed, the original instances are gone and there's no recovery hint. No "X/Y converted, Z failed" summary; no link to undo (Revit's undo stack will be a long compound entry).

- **`Topmost = true`** on the log window (`RevitCommand.cs:80`) is a small annoyance but probably correct here.

### Log experience

**What's logged**

- Start/end banners (`Log("--- 1-Click Seamless Conversion Started ---")` etc.).
- Per-family conversion start with template name (`FamilyConversionLoggingService.LogStart`).
- Geometry copy fallback notes (`FamilyGeometryCopyService.cs:46, 58, 61`).
- Per-instance placement OK with coords formatted to 2 decimals (`FamilyConversionService.cs:311`).
- Per-instance warnings on placement / parameter apply failure (lines 286, 315, 352, 370).
- Auto-accepted / blocked dialog messages (`ConvertFamilyCommand.cs:41, 53, 59, 65`).

**What's missing or broken**

- **No batch summary.** No "X families converted, Y instances replaced, Z skipped, time = …". The `ExecutionTimer` block exists (`FamilyConversionService.cs:56`) but writes only its own log line on dispose — no totals.

- **No per-family success/failure marker.** When a group fails inside `ExecuteFamilyGroupConversion`'s catch (line 178), the message comes from `LogCriticalError` which prepends "Critical Error:" but doesn't say which family it was — `LogStart` happens earlier but isn't paired with a `LogEnd`.

- **Inconsistent prefixes.** `[ConvertFamily]`, `[FamilyConversionService]`, `[FamilyEditorService]`, `[FamilyTemplatePathService]`, plus bare lines, plus `"--- ... ---"` banners, plus `"Critical Error:"`, `"Warning:"`, `"  Skipped: …"` (two-space indent), `"  [OK] …"`, `"  [ERROR] …"`. No convention, no easy way to filter for errors.

- **Stack traces dumped raw** into the user-visible log (`FamilyConversionLoggingService.cs:23`). Useful for debugging, terrible for end-user UX. No "show details" toggle.

- **`Logger.Instance` is a global singleton** — accessed directly in 9+ places inside the `FamilyConversion` folder. The injected `IFamilyConversionLoggingService` is used in only 2 places (`FamilyConversionService.cs:99, 178`); everywhere else bypasses it. The abstraction exists but isn't enforced.

- **Spam risk**: per-instance `[OK]` log lines (`FamilyConversionService.cs:311`) for batches of hundreds. No log level filtering.

- **`LogWarning` in `IFamilyConversionLoggingService`** (line 15) just calls `Logger.Instance.Log("Warning: ...")` — doesn't use `Logger.Instance.LogWarning`. Inconsistent severity reporting.

- **No correlation ID per family group.** When 30 families are converted, identifying which messages belong to which family requires reading sequentially.

### Technical debt / bloat

- **34 files / 1666 LOC for ~600 LOC of real logic.** Removing the trivial wrappers (Source/Target/Save/Load/Cleanup/LoadOptionsFactory/Logging/Naming) and merging `SaveLoadService` (it's a 2-line indirection) would drop ~10 service classes and ~10 interfaces with no loss of capability.

- **`FamilyConversionNamingService` is dead** (see Functionality). Whole file is unused by Convert Family; only place it could be wired up is the `customName` parameter, which the command always passes empty.

- **`FamilyEditorService` is misplaced** — used only by `CategoryChangerCommand`. Also has its own `IsModelCategory`, `FindTemplate` helpers duplicating concepts in `FamilyTemplatePathService`.

- **Two parallel template-resolution stacks**:
  - `FamilyTemplatePathService.GetTargetTemplatePath` (LECG door/window/furniture-aware).
  - `FamilyEditorService.FindTemplate` (model vs detail, English/Spanish/Imperial-aware).
  - `Configuration.RevitConstants.FindTemplate` (used by the orchestrator at line 196).
  Three different resolvers, three different fallback orders. Pick one.

- **Stale comments**:
  - `FamilyTempFileCleanupService.cs:26-34` (~9 lines arguing with itself).
  - `FamilyEditorService.cs:249-250` ("Future performance enhancement…").
  - `FamilyParameterSetupService.cs:165` ("Some values may fail to set…").

- **`Configuration.RevitConstants.FindTemplate` literal** (`FamilyConversionService.cs:196`) embeds an LECG-internal path inside business logic. Belongs in config, not code.

- **No tests for any FamilyConversion service.** `LECG.Tests/` has tests for `FamilyNamePolicy`, `FamilySelectionPolicy`, `DetailFamilyNamePolicy` — pure-logic policies — but the service mass is untested. The decomposition didn't even buy testability.

- **`FamilyInstanceData` is 105 LOC with non-trivial logic** (parameter capture/apply, host loss, location loss) and is also untested.

- **Per-class `IsExpected*Exception` static methods** duplicate the same set of 4-5 exception types. Should be one helper.

### Architecture concerns

- **DI singletons holding nothing.** All 14 family-conversion services are registered as `Singleton` (`Bootstrapper.cs:133-148`). None hold per-conversion state, so this is correct, but the volume of registrations is itself a smell. Also: ServiceLocator pattern (`ConvertFamilyCommand.cs:81`) is used for the orchestrator, while the orchestrator itself uses constructor injection — inconsistent.

- **Coupling: `FamilyConversionService` knows everything.** It owns capture, deletion, placement, level resolution, naming resolution (kind of), template resolution, *and* coordinates 6 services. It's the god class the decomposition was supposed to prevent.

- **Leaky abstraction: `IFamilyConversionExecutionService` returns `(Document, string)` tuple.** Callers must remember to `Close` the doc and clean up the temp path. The Finalize service exists but has to be invoked manually in the orchestrator's `finally`. Lifetime ownership is unclear.

- **`WarningSwallower` auto-resolves DeleteElements failures** (`Core/WarningSwallower.cs:26-30`). Combined with the `OnDialogShowing` handler, the conversion path runs with two layers of "make warnings go away." This is necessary for unattended batch conversion but means the user has *zero* visibility into what got deleted under them. There's no log of which elements were resolved-by-deletion.

- **`uiApp.DialogBoxShowing` is process-global** — if anything else in Revit shows a dialog during the conversion (e.g. linked file reload), this command's handler will silently click Cancel. Particularly risky given the `OnDialogShowing` "cancel anything I don't recognize" default.

- **Side effects across documents in one transaction.** The flow opens the source family doc, mutates the target family doc, saves to disk, loads into project doc, and starts a transaction in the project doc — across 3 documents and 1 file. Failure recovery semantics are fuzzy; partial state (deleted instances + failed load) is reachable.

- **No undo coordination.** Each step uses its own transaction. The user sees multiple undo entries, none of which fully reverse the operation (the file write to `%TEMP%` and any LoadFamily aren't undoable as a unit).

## Recommended upgrade themes (top 5, ranked)

1. **Add a confirmation/preview UI and fix the "all instances of the family get touched" surprise. (M)**
   The single biggest user-facing risk is that picking one instance silently rebuilds every instance of that family. A dedicated `ConvertFamilyView.xaml` (matching the pattern of `CategoryChangerView`) showing: families about to be converted, instance count per family, host status (will be lost!), template selection, "do not save" toggle, and a summary table on completion. *Why*: prevents data loss, exposes the existing `customName`/`templatePath`/`isTemporary` knobs that are already in the API but unreachable.

2. **Restore host + location-curve fidelity, or refuse to convert what we can't preserve. (M-L)**
   Either implement curve-based and host-based replacement in `FamilyInstanceData.Apply`, or detect those cases in the filter and exclude them with a clear log line ("3 instances skipped: hosted to wall, conversion does not preserve hosts"). Today both data points are captured and silently discarded. *Why*: this is the most likely source of "the tool broke my model" bug reports.

3. **Collapse the over-decomposition; move FamilyEditor out. (M)**
   Inline `FamilySourceDocumentService`, `FamilyTargetDocumentService`, `FamilySaveLoadService`, `FamilyConversionLoggingService`, `FamilyConversionFinalizeService`, `FamilyTempFileCleanupService`, `FamilyConversionNamingService` (delete — dead) into the orchestrator or into one `FamilyDocumentLifetimeService`. Move `FamilyEditorService` to `Services/FamilyCategory/` since `CategoryChangerCommand` is its only client. Centralize `IsExpected*Exception` and the three template resolvers. *Why*: 34→~10 files, the same behavior, the surface area you actually need to test becomes tractable.

4. **Build a real error/summary report instead of streaming text. (S-M)**
   Replace the stack-trace dumps + per-instance `[OK]` lines with a structured per-family record (family name, instance count, copied geometry count, parameter copy errors, replacement count, skipped-host count, elapsed). Surface in the LogView footer ("12/13 families succeeded, 47 instances replaced, 3 skipped — see details"). Add a single consistent log-prefix convention. *Why*: today the log is unreadable for batches and useless as audit. Low effort relative to value.

5. **Tighten the dialog handler and add a unit-testable conversion plan. (M)**
   The `OnDialogShowing` substring matching is brittle and dangerously broad. Replace with an explicit `DialogId` allow/deny list plus a "cancel and log" default that surfaces blocked dialogs to the user. Separately, extract the "what would this conversion do" logic into a pure `ConversionPlan` object (families to convert, instances per family, hosts to drop, types to collapse) — testable without Revit, and the foundation for the preview UI in #1. *Why*: today the only tests are on naming policies; no service code is exercised. A `ConversionPlan` is the natural seam.

## Out-of-scope observations

- `RevitCommand.ShowLogWindow` (`Core/RevitCommand.cs:70-90`) is shared by 20+ commands but uses `Application.Current?.Dispatcher` which can be null in some Revit hosting scenarios (the fallback `else action()` at line 101 just runs synchronously on the calling thread — risky if it's not the UI thread). Consider centralizing UI-thread access.

- `Logger.Instance` global singleton is bypassed by all the logging-service abstractions — consistent with much of the codebase. The `IFamilyConversionLoggingService` injection adds a constructor parameter and zero benefit.

- `Configuration.RevitConstants.FindTemplate` is referenced from inside the orchestrator (`FamilyConversionService.cs:196`) — coupling business logic to a constants helper. Belongs in `IFamilyTemplatePathService`.

- The `LogView` has no cancel button wired to in-flight commands, so any long-running command (not just Convert Family) blocks the user. Generic problem worth fixing once.

- `WarningSwallower.PreprocessFailures` auto-accepts `DeleteElements` resolutions for *any* failure that offers it (`Core/WarningSwallower.cs:26-30`). This is shared infrastructure used by `DeepPurgeService`, `PurgeExecutionCoordinatorService`, `CompactingStylesCommand`, `FamilyGeometryCopyService`, `FamilyProjectLoadService`. A single audit-log of what got deleted across the whole add-in would help diagnose phantom data loss reports.

- Tests folder pattern: pure logic (`Naming/*Policy.cs` in `LECG.Core/`) is tested; everything Revit-bound is not. Consider a thin `IRevitFacade` to make at least the orchestration logic testable.
