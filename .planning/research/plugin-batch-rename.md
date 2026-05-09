# Plugin Audit: Batch Rename (Search & Replace)

## What it does (one paragraph)

The Batch Rename plugin opens a modal WPF dialog (`BATCH RENAME`, 680x600) that lets the user select one of nine mutually-exclusive scopes (Types, Families, Views, Sheets, Materials, Object Styles, Line Styles, Patterns, Parameters), filter by name/category and (for Parameters) by group/instance/read-only, then apply a stack of five rule operations (RegEx-or-literal Replace, Case, Remove first/last/range, Add prefix/insert/suffix, and Numbering) to produce a live-updated preview grid of Old Name -> New Name. Hitting "Apply Rename" runs a single transaction over standard elements and a per-family `EditFamily`/`LoadFamily` cycle for FamilyParameters, with a "skip-if-unsafe" pre-check guarding parameters that drive dimensions, formulas, or element associations.

## Architecture map

```
SearchReplaceCommand (src/Commands/SearchReplaceCommand.cs:24)
  -> ServiceLocator.GetRequiredService<ISearchReplaceService>()
  -> ServiceLocator.GetRequiredService<SearchReplaceView>()      [transient view + VM]
  -> view.ShowDialog()                                            [blocks Revit UI thread]
  -> ShowLogWindow("Batch Rename")
  -> service.ExecuteBatchRename(doc, vm.PreviewItems.Where(IsChecked), Logger.Instance, reporter)

SearchReplaceView (src/Views/SearchReplaceView.xaml + .xaml.cs:10-22)
  -> DataContext = SearchReplaceViewModel (constructor-injected)
  -> BindDialogClose closes when vm.ShouldRun toggles

SearchReplaceViewModel (src/ViewModels/SearchReplaceViewModel.cs)
  -> Initialize(service, doc) (line 134)
  -> RefreshScope() (line 161): calls service.CollectBaseElements + GetUniqueCategories
  -> Every rule/filter PropertyChanged -> UpdatePreviewAsync (line 186)
  -> UpdatePreviewAsync: 150ms debounce, Task.Run -> service.ProcessPreview, marshal back to UI
  -> Apply() (line 200): minimal validation, base.Apply() -> ShouldRun = true -> dialog closes

SearchReplaceService facade (src/Services/Renaming/SearchReplaceService.cs:27)
  -> CollectBaseElements -> IBaseElementCollectionService
  -> ProcessPreview / GetUniqueCategories -> ISearchReplacePreviewService
  -> ExecuteBatchRename -> IBatchRenameExecutionService

BaseElementCollectionService (src/Services/Renaming/BaseElementCollectionService.cs)
  - Single 300-line method with 7 if-blocks (Types/Families/Views+Sheets/Materials/Patterns/Styles/Parameters)
  - Parameters branch (line 154-298) does the documented two-phase symbol+instance scan

SearchReplacePreviewService (src/Services/Renaming/SearchReplacePreviewService.cs)
  - Pure in-memory filter+rule pipeline
  - Calls IRenameRulePipelineService.ApplyRules per element

RenameRulePipelineService (src/Services/Renaming/RenameRulePipelineService.cs)
  - Hard-coded order: Remove -> Replace -> Case -> Add -> Numbering
  - Each rule delegates to LECG.Core.Rename.RenameRuleEngine static methods

BatchRenameExecutionService (src/Services/Renaming/BatchRenameExecutionService.cs)
  - Splits items into standard vs FamilyParameter
  - Standard: one transaction, per-element rename with GraphicsStyle Swap fallback
  - FamilyParameter: groups by family, EditFamily -> RunConditional -> LoadFamily once per family
  - Pre-builds dimensionLabels / formulaReferenced / elementAssociated guard sets

FormulaUpdateService (src/Services/Renaming/FormulaUpdateService.cs)
  - Registered in DI but has ZERO consumers in src/. Dead service.
```

## Findings by axis

### Functionality

**REQ-01 root cause (the blank Name/Category bug).** The grid columns in `SearchReplaceView.xaml:428` bind to `Type` and `ElementId`, plus `OriginalValue` and `NewValue`. There is *no* "Name" or "Category" column in the actual grid — but `ReplaceItem` (defined inline in `SearchReplaceViewModel.cs:16-25`) has only `IsChecked`, `ElementName`, `OriginalValue`, `NewValue`, `ElementId`, `Type`. **The model is missing `Name` and `Category` entirely.** When `SearchReplacePreviewService.cs:110` constructs each `ReplaceItem`, it sets `ElementName = el.Name` is *not* even done — only `ElementId`, `ElementName`(missing), `OriginalValue`, `NewValue`, `IsChecked`, `Type` are set, and `ElementName` is never populated at all (line 110-118). So if any UI surface or downstream consumer reads `item.ElementName`, it gets `""`. This is also what the BatchRenameExecutionService logs when reporting progress: `reporter.Report($"Processing {item.ElementName}...", percent)` (line 75) — **the progress log will show "Processing ..." with an empty name**. That is the user-visible symptom of REQ-01 in the log window. If the bug report is about blank cells in the grid, it's almost certainly the same: someone added "Name"/"Category" columns at some point referencing properties that don't exist on `ReplaceItem`. Either way, the fix is structurally the same: populate `ElementName` (and add `Category`) in `SearchReplacePreviewService.cs` line 110-118 from `ElementData`.

**Dead/half-finished code.**
- `IFormulaUpdateService` / `FormulaUpdateService` (`src/Services/Renaming/FormulaUpdateService.cs:1-13`) is registered in `Bootstrapper.cs:131` but *no class in src/ injects it*. `BatchRenameExecutionService` calls `FormulaNameUpdater.ContainsReference` directly (line 425) and never updates formulas at all — the wrapper service is orphaned.
- `SearchReplaceCommand.cs:38-42`: after the dialog closes, the command calls `ShowLogWindow`, runs the rename, and... the log window stays open showing the result. But the command does not check `service` actually executed; it just trusts return value and writes "Rename complete." even if zero items were renamed.
- The "Extension" rule panel (`SearchReplaceView.xaml:378-383`) is a `(Coming Soon)` placeholder occupying a 1/6 of the operations grid.
- `ReplaceItem.ElementName` is declared but never assigned (see REQ-01 above) — it is a dead field.

**Missing features.**
- No undo across the family-parameter path. The standard transaction is undoable, but the per-family `EditFamily`/`LoadFamily` loop commits each family separately and there is no aggregate undo group. If family #5 fails halfway, families #1-#4 are already committed back into the project.
- No persistent skip reason on `ReplaceItem`. When a parameter is rejected by `GetRenameSkipReason` the reason is logged but the grid row still shows the rename as if it succeeded. Selecting it again will keep skipping.
- No regex validation feedback. If `ReplaceRule.UseRegex` is on and the user types an invalid pattern, the preview just throws and the catch in `UpdatePreviewAsync` (line 197) writes `"Error loading preview: ..."` — but no per-item indication, no syntax check.
- No "rename Family element itself" path (only Type, FamilySymbol, FamilyParameter). `Family` objects are listed in the grid (line 33-49 in `BaseElementCollectionService.cs` collects `OfClass(typeof(Family))`), and execution will just call `el.Name = item.NewValue` on them, which usually works but is silently uncategorized.
- The grid has no "show only changed" toggle. If you have 10,000 Types and your rule matches 3, you still see all 10,000 with NewValue equal to OriginalValue.

**Bugs.**
- `BatchRenameExecutionService.cs:64` computes `percent = (double)current / total * 100` against `total = items.Count`, but only standard items go through this loop. If you have 100 items where 60 are family parameters, the standard loop's progress maxes out at 40%.
- `BatchRenameExecutionService.cs:185-199`: `RunConditional` returns the result of `renamedInFamily > 0`, but `count += renamedInFamily` is incremented *inside* the lambda even if the transaction is rolled back. **Net effect: rolled-back parameters are still counted as "Modified" in the final log line** (line 218: `Modified {count} elements`).
- `BaseElementCollectionService.cs:213-222` and `:275-283`: classic "swallow exception, return empty string" anti-pattern for `ParamGroup`. If `GetGroupTypeId` throws on any param, the row's ParamGroup is `""` and the user can't tell whether the param has no group or whether the lookup failed. This is the "ParamGroup swallowed try/catch -> empty string" pattern called out in the prompt — confirmed.
- `SwapStyle` (`BatchRenameExecutionService.cs:469-547`) only re-points `CurveElement.LineStyle` references. Any other element using the old GraphicsStyle (annotation, model line subcategory, geometry style) is silently dropped. The "Try Delete Old" can succeed leaving orphan references, or fail and leave both old+new categories — neither is logged at warning severity.
- `BaseElementCollectionService.cs:111`: `bool isBuiltIn = cat.Id.Value < 0;` — the giant comment block above admits the author isn't sure this is right ("Imports... usually appear as subcategories of 'Imports in Families'"). The current heuristic likely under-collects user-renamable items and over-collects built-ins for the Imports tree.
- `SearchReplacePreviewService.cs:108`: `_renameRulePipelineService.ApplyRules(el.Name, context, results.Count)` uses `results.Count` as the numbering index. Since filtered-out items don't increment, the index will appear to "skip" numbers when filters are active — fine if the user expects that, but undocumented and inconsistent with most batch-rename tools (which typically number by selected-item rank).
- `BatchRenameExecutionService.cs:77`: `if (string.Equals(el.Name, item.NewValue, StringComparison.Ordinal)) continue;` — early-exits without incrementing `count`, but also without logging "no change". Silent.

### Error handling

- **Swallowed exceptions:**
  - `BaseElementCollectionService.cs:219-222` and `:280-283`: `try { GetGroupTypeId } catch { = "" }` — no log, no telemetry, no way to know it ever failed.
  - `BatchRenameExecutionService.cs:396-399` (`BuildDimensionLabelNames`): `try { dim.FamilyLabel } catch { /* Some dimensions may not support FamilyLabel */ }` — fine in principle but eats every other exception too (TypeLoadException, AccessViolation in native interop, etc.).
  - `BatchRenameExecutionService.cs:459-463` (`BuildElementAssociationNames`): same pattern — bare `catch {}`.
  - `BaseElementCollectionService.cs:177` and `:251`: `if (fs.Family == null)` and `if (fi.Symbol?.Family == null)` silently skip. A corrupt family is invisible to the user.
- **Exceptions that bubble:**
  - `BatchRenameExecutionService.cs:131`: `catch (Exception ex) when (IsExpectedRenameException(ex))` — anything not in {ArgumentException, InvalidOperationException, Revit equivalents} bubbles out of the transaction lambda, which means `_transactionService.Run` will likely abort the whole standard-item pass. Mid-batch crash = nothing logged for items 50-N.
  - `UpdatePreviewAsync` (`SearchReplaceViewModel.cs:197`): catches all, sets `ValidationMessage` — at least the user sees something, but no stack and no log entry.
- **User sees nothing on failure:**
  - GraphicsStyle swap "could not delete original" (`BatchRenameExecutionService.cs:529, 533, 537`) logs to the log window via `logger.Log(...)` — that's `LogLevel.Info`, no warning color, easy to miss.
  - When `_service.CollectBaseElements` throws inside `RefreshScope` (`SearchReplaceViewModel.cs:164`), there is **no try/catch**. The exception propagates up the property setter that fired it, and WPF's data-binding eats it silently. The user would see an empty grid and have no idea why.
  - `SearchReplaceCommand.cs:40` does not wrap `service.ExecuteBatchRename` in any try/catch. If anything escapes, the user sees the standard Revit "an error has occurred" dialog with no log context.

### Performance

- **Two-phase FamilyParameter scan (`BaseElementCollectionService.cs:154-298`).** The intent is correct: type-params live on `FamilySymbol`, instance-only params only show up on placed `FamilyInstance`s, so both must be walked. But the actual cost: `FilteredElementCollector(WhereElementIsElementType + OfClass FamilySymbol)` is cheap; the `FamilyInstance` collector then walks **every placed instance in the project**, doing a `processedInstanceFamilies.Add` HashSet check to bail after the first hit per family. In a model with 50,000 instances and 2,000 families, that's 50,000 enumerations to discover ~2,000 unique families, and we touch every `Parameter` on the first instance we see. This is acceptable but not optimal — a smarter approach would be `collector.WherePasses(new ElementClassFilter(typeof(FamilyInstance)))` followed by `.GroupBy(fi => fi.Symbol.Family.Id).Select(g => g.First())`, or even better, only collect instances for families *not yet seen* via a category-keyed query. As written, it's correct but does redundant `Parameters` reflection on the early instance for every family.
- **Per-iteration enum evaluation:** `foreach (Parameter p in fs.Parameters)` instantiates the Revit ParameterSet enumerator, which internally allocates. Doing this on every FamilySymbol (potentially thousands) creates GC pressure. Not catastrophic but noticeable on cold preview.
- **`BuildFormulaReferencedNames` is O(P²)** (`BatchRenameExecutionService.cs:408-433`). For every parameter with a formula, it loops over all parameter names and calls `FormulaNameUpdater.ContainsReference` (regex-based). A family with 200 parameters and 100 formulas does 20,000 regex matches per `EditFamily`. Fine for typical families, painful for parametric content libraries.
- **`BuildElementAssociationNames`** (`BatchRenameExecutionService.cs:439-467`) does `WhereElementIsNotElementType().ToList()` then iterates every `Parameter` of every element and calls `GetAssociatedFamilyParameter`. For a complex parametric family with hundreds of nested elements this is slow and is run *unconditionally* per family even if no items in this family need the safety check.
- **Preview rebuild on every keystroke:** `FilterName` setter calls `_ = UpdatePreviewAsync()` (line 69). The 150ms debounce in `UpdatePreviewAsync` (line 192) helps, but the *upstream* `_cachedElements` only refreshes when scope changes. Good — no Revit re-collection on every keystroke. Verified.
- **`SetExclusiveScope` re-collects everything on every scope toggle** (`SearchReplaceViewModel.cs:155 -> RefreshScope`). Toggling Types -> Views -> Types refetches Types. There is no per-scope cache. For Parameters (the slow scope) this is most painful.
- **`PreviewItems.Clear()` + `Add()` in a loop** (line 194). `ObservableCollection` raises one notification per Add. With 5,000+ rows and `EnableRowVirtualization=true` it's tolerable, but a `BulkObservableCollection` (suppress + reset) would be measurably faster.
- **Reflection in `LecgDataGrid.SetAllBooleanProperty`** (lines 67-75): reflects on the first item's type and `propInfo.SetValue` per item. Trivial for a few thousand rows but it's worth noting that **the View doesn't actually use this method** — `SearchReplaceView.xaml:417-419` binds Select All / Select None to ViewModel commands (`SelectAll`/`SelectNone`) which iterate `PreviewItems` directly. So `LecgDataGrid.CheckAll/UncheckAll` is dead code in this view (might be used by other views).

### UX / UI

- **Scope is exclusive (radio-like) but rendered as checkboxes.** `SearchReplaceViewModel.cs:42-66` toggles all other scopes off when one is set true (`SetExclusiveScope`). The XAML uses `<CheckBox Style="PillCheckboxStyle">` which look like multi-select pills. Users will assume they can multi-select scopes and be surprised.
- **No confirmation step.** `Apply Rename` immediately runs against the model. For 5,000 type renames there's no "Are you sure?" — and no dry-run / export-list option.
- **No conflict preview.** If two source names collapse to the same NewValue (e.g. "A 1" and "A_1" both become "A_1"), the preview shows two rows with the same NewValue and the second rename will fail at execution. No visual highlight.
- **Validation UI is single-line bottom strip** (`SearchReplaceView.xaml:453`). Errors from `UpdatePreviewAsync` get truncated; stack traces never shown.
- **Filter UX:** the "Filter by Name" textbox doesn't show match count ("3 of 1,200 match"). The PREVIEW header just says `({0} items)` (line 412) which is the post-rule count, not the pre-filter total.
- **`Replace Spaces with Underscore` button** (`SearchReplaceView.xaml:102`) is a single character `_` with a tooltip — not discoverable. Same button repeated next to the Replace text.
- **The "(Coming Soon)" Extension panel** (line 378) takes 1/6 of the operations grid — wastes prime real estate.
- **The dialog blocks the Revit UI thread** (`SearchReplaceCommand.cs:34: view.ShowDialog()`). User cannot pan/select in Revit while the dialog is open. For a tool whose preview depends on what's in the model, modeless would be a UX win — but introduces re-entrancy/transaction problems.
- **No keyboard shortcut hints** anywhere; no Enter-to-Apply.
- **DataGrid columns not resizable persistently** — `LecgDataGrid` doesn't save column widths between sessions.
- **No row-level "skip with reason" affordance.** The user cannot uncheck a row and add a note for themselves.

### Log experience

- **What gets logged:**
  - Start of batch with counts (line 54): `Starting batch rename for {total} items ({stdCount} standard, {famCount} family parameters)...` — good.
  - Per-element success: `Renamed '{old}' to '{new}'` (line 129) — good but repetitive for thousands of items.
  - Skip reasons for FamilyParameters (line 274): `Skipped '{old}' in '{family}': {reason}` — good and aligns with REQ-04 partially.
  - Final summary (line 218): `Batch rename complete. Modified {count} elements.` — count is wrong (see bug above).
- **What doesn't:**
  - Standard-item "no change" early-exit (line 77) is silent.
  - GraphicsStyle Swap "could not delete original" is `Log` (Info), not `LogWarning` (lines 529-537).
  - The non-FamilyParameter path has **no skip-reason concept at all**. Standard items either succeed or hit an exception. Read-only types, locked names, etc. just throw and get logged as `ERROR renaming {name}: {message}` — no structured reason.
  - When `ProcessPreview` runs, nothing is logged. The user has no visibility into how many items were filtered out and why.
  - The log window is shown *only after* the user clicks Apply (`SearchReplaceCommand.cs:38`). If the preview crashes (which is logged only as `ValidationMessage`), the log window is never opened.
- **Log spam:** "Processing {ElementName}..." per item (line 75) — and `ElementName` is `""` because of the REQ-01 bug, so the log fills with `Processing ...`.
- **Inconsistent severity:** GraphicsStyle Swap warnings use `Log` (Info), formula errors use `LogWarning` in `SwapStyle` line catches (line 494-495 — also note that line is a 200-char one-liner, see Tech debt), per-item rename errors use `LogError`. No clear convention.
- **Logger.Instance is a singleton** (`Logger.cs:27`) and the command passes `Logger.Instance` directly into the service (`SearchReplaceCommand.cs:40`). The DI registration of `ISearchReplaceService` doesn't get an `ILogger` injected; the service receives it per-call. That's fine, but it means tests can't easily verify log output — they'd need to mock `Logger.Instance`, which is hard against a static.

### Technical debt / bloat

- **`BaseElementCollectionService.CollectBaseElements` is a 290-line method** with 9 branches and two separate sub-loops (the FamilyParameter path is essentially its own ~150-line method). Should be split into one collector method per element kind, with shared `IElementCollector` strategy interface. Pure SRP violation.
- **`BatchRenameExecutionService.cs:494-495`** are two ~600-character one-liners stuffing three nested try/catch blocks into a single line — totally unreadable, looks like a bad auto-format pass:
  ```
  try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Projection); ... } catch (ArgumentException ex) { ... } catch (InvalidOperationException ex) { ... } catch (RevitExceptions.InvalidOperationException ex) { ... }
  ```
- **The "ParamGroup swallowed try/catch -> empty string" anti-pattern** (called out in prompt): confirmed in `BaseElementCollectionService.cs:213-222` and duplicated at `:275-283`. Identical 10-line block in two places — should be a static helper `TryGetParamGroupLabel`.
- **Duplicate `paramGroupLabel` extraction blocks** (lines 213-222 vs 275-283) — copy-paste.
- **Duplicate `ReplaceItem`-construction logic between symbol and instance phases** (`BaseElementCollectionService.cs:224-235` and `285-296`). Same 10 properties, copy-paste.
- **`SearchReplaceService` is a pure pass-through facade** (`SearchReplaceService.cs:27-71`). Every method just delegates to one of three services. The facade adds zero value and forces every method change to be made in 4 places (interface, facade, sub-interface, sub-impl). Could be eliminated; the command could depend on the three services directly, or the facade could become an aggregate composition root.
- **`SearchReplaceViewModel.cs:141-142`**: `ToContext()` and `ToCriteria()` are 20-parameter constructor calls on one line each. Need a builder or split objects.
- **`SearchReplaceViewModel.cs:153`**: a single line raises 9 PropertyChanged events for the 9 scope properties — easy to forget one when adding a new scope.
- **`ReplaceItem` is defined inside `SearchReplaceViewModel.cs` (line 16-25)** but `ElementData` is defined inside `SearchReplaceService.cs` (line 13-25). Two model types, two odd locations, neither in `Models/`.
- **`IFormulaUpdateService`/`FormulaUpdateService`** — entirely dead (see Functionality). Pure dead code added by the consolidation phase, never wired in.
- **Stale comments:** `BaseElementCollectionService.cs:117-122` reads like a journal entry from a debugging session ("For now, the user goal is to see styles that AREN'T showing up... The previous logic skipped if Enum.IsDefined, which might have been too aggressive...") — should be either removed or moved to commit history.
- **The 6-character-then-tooltip `_` button** (`SearchReplaceView.xaml:102, 233`) is duplicated in two places, both bound to different commands.
- **Phase 1 consolidation cleanliness assessment:** the split into `BaseElementCollectionService` + `SearchReplacePreviewService` + `BatchRenameExecutionService` + `RenameRulePipelineService` is conceptually clean and the interfaces are minimal — but the `SearchReplaceService` facade was kept "for compatibility" and adds no value, the `FormulaUpdateService` is a stillborn extraction never consumed, and `ElementData`/`ReplaceItem` were never moved out of their original files. Consolidation is **structurally good but cosmetically incomplete**.

### Architecture concerns

- **`SearchReplaceService` facade leaks `ReplaceItem`** (a ViewModel type from `LECG.ViewModels`) across the service boundary (`ISearchReplaceService.cs:19-26`). Services depend on ViewModels. This is backwards — the rename pipeline should use a domain DTO, and the ViewModel should adapt.
- **`RenameRuleContext` is a record holding `ReplaceRule`/`AddRule`/etc.** (`Models/RenameRuleContext.cs:6-27`) which are themselves `ObservableObject`s from CommunityToolkit.Mvvm. The "context" that gets passed to the pure pipeline is therefore a mutable, INPC-eventing object — making the pipeline non-deterministic if the UI is re-binding while a background `Task.Run` is reading. The 150ms debounce papers over this; it doesn't fix it. The pipeline should take a snapshot of immutable rule options.
- **`ElementData` defined in `SearchReplaceService.cs:13-25`** is in the `LECG.Services` namespace but not in a model file — discoverability is poor.
- **`SearchReplaceCommand` constructs `RevitCommandProgressReporter` inline** (`SearchReplaceCommand.cs:39`) — the command knows about the reporter implementation. Should be DI-injected via a factory.
- **`Logger.Instance` is a singleton** with `_uiDispatcher` captured from `Dispatcher.CurrentDispatcher` at construction time (`Logger.cs:43-46`). If the singleton is first touched on a non-UI thread, the dispatcher is wrong forever. The `SetDispatcher` helper exists but the command doesn't call it.
- **`RefreshScope` calls `_service.CollectBaseElements` synchronously** (`SearchReplaceViewModel.cs:164`). For a 50k-element model with FamilyParameters, this freezes the UI thread for several seconds when the user toggles scope. Should be `await Task.Run(...)`.
- **The `TransactionService` call in `BatchRenameExecutionService` runs on the UI thread** (since the command is a Revit external command). With thousands of items this is fine because Revit transactions must run on the main thread anyway, but there's no progress pump — the WPF dispatcher is starved during the loop, so progress updates only repaint between items.
- **`SwapStyle` only handles CurveElements** (`BatchRenameExecutionService.cs:497-520`). Object Styles are used by far more element categories than just lines. Calling this a "rename" is misleading; it's a "rename if it's a line style, otherwise leave dangling references." Should at least log a list of element categories whose references were not migrated.

## Overlap with planned phases

- **REQ-01 (blank Name/Category in grid).** Root cause: `ReplaceItem` model is missing `Name`/`Category` properties; `ElementName` is declared but never assigned in `SearchReplacePreviewService.cs:110-118`. Fix sketch: (a) add `Name` and `Category` properties to `ReplaceItem`; (b) populate them from `ElementData.Name` and `ElementData.Category` when constructing `ReplaceItem`; (c) add `<DataGridTextColumn Header="Name">` / `Category` columns to `SearchReplaceView.xaml:424-431` (or rename `OriginalValue` -> `Name` if that was the intent). Same fix removes the `Processing ...` empty-name log spam.

- **REQ-02 / REQ-03 (Safe Rename for formula-referenced & dimension-label parameters).** Already partially implemented as a *skip*, not a *safe rename*. `BatchRenameExecutionService.GetRenameSkipReason` (line 334-375) returns `"drives a dimension label"` and `"referenced in another parameter's formula"` and the parameter is skipped entirely. The "Safe Rename" upgrade would change this from skip-with-reason to rename-and-update-references: rename the parameter, then update every formula via `FormulaNameUpdater.UpdateFormula` (already exists in `LECG.Core.Rename`) and every dimension label via `dim.FamilyLabel = newParam`. **The orphaned `IFormulaUpdateService`** is presumably the placeholder for this — Phase 1 already extracted it, now Phase 4/5 needs to consume it. The dimension-label path has no service yet.

- **REQ-04 (Reason for Skip in UI/Logs).** Logs already include skip reasons for FamilyParameter via `LogWarning` (line 274). UI does not — `ReplaceItem` has no `SkipReason` property and the grid has no column for it. Standard items have no skip-reason mechanism at all. Recommended: (a) add `SkipReason` to `ReplaceItem`; (b) add a `GetRenameSkipReason`-equivalent for standard items (read-only types, system families, locked sheet numbers); (c) add a column or row tooltip; (d) when a row has a skip reason, render with a muted color and disable the checkbox.

- **REQ-05 (unit tests).** Current Renaming-related test files: `LECG.Tests/Services/RenameRuleEngineTests.cs` and `LECG.Tests/Services/FormulaNameUpdaterTests.cs`. **Zero tests** for `SearchReplacePreviewService`, `RenameRulePipelineService`, `BaseElementCollectionService`, `BatchRenameExecutionService`, or `SearchReplaceService`. The pure ones (`SearchReplacePreviewService`, `RenameRulePipelineService`) are perfectly testable today — they take pure inputs. The Revit-bound ones (`BaseElementCollectionService`, `BatchRenameExecutionService`) are not testable as written without abstracting the `FilteredElementCollector` and `FamilyManager` behind interfaces. REQ-05 implicitly requires that abstraction work first.

## Recommended upgrade themes (top 5, ranked)

1. **Fix the model/grid binding contract (REQ-01) and add structured `SkipReason` (REQ-04) in one pass.** *Why:* both are symptoms of `ReplaceItem` being incomplete. Adding `Name`, `Category`, `SkipReason`, `Status` (Pending/Skipped/Renamed/Failed) properties and surfacing them in the grid solves REQ-01, sets up REQ-04, and gives the user trustworthy feedback. *Effort:* S. *Roadmap:* REQ-01 + REQ-04 — combine them.

2. **Consume `IFormulaUpdateService` and add `IDimensionLabelUpdateService` to ship Safe Rename (REQ-02/03).** *Why:* the skip-list infrastructure is already there; flipping it to "rename + update references" is the natural next step, and `FormulaUpdateService` is sitting unused. Will require a transaction-safety review (rename param -> update all formulas -> ensure FamilyManager state is consistent before LoadFamily). *Effort:* M. *Roadmap:* REQ-02 + REQ-03.

3. **Test-cover the two pure services (`SearchReplacePreviewService`, `RenameRulePipelineService`) and abstract the Revit collectors behind testable seams.** *Why:* REQ-05 + every future change becomes safer. The pipeline + filter logic is the brain of the plugin and is currently exercised only manually. *Effort:* M. *Roadmap:* REQ-05 — but expand it to include the abstraction work for the Revit-bound services.

4. **Fix the count/progress/transaction-rollback bugs in `BatchRenameExecutionService`.** *Why:* `count` is incremented inside a conditional transaction lambda even on rollback (line 197), the standard-item progress maxes out at the standard/total ratio (line 64), and "no-change" early-exit is silent (line 77). These produce wrong-looking summary lines that erode user trust. *Effort:* S. *Roadmap:* net-new — not in current roadmap.

5. **Decompose `BaseElementCollectionService` and kill the `SearchReplaceService` facade.** *Why:* the 290-line collector is the largest debt item in the plugin and prevents per-scope optimization (caching, lazy parameter scan, parallel collection). Removing the pass-through facade removes a layer that adds zero value. Together they unlock collector-level tests and make adding a new scope (REQ candidates: Schedules, Tags, Title Blocks) a single-file change. *Effort:* L. *Roadmap:* net-new (follows from REQ-06 consolidation work).

## Out-of-scope observations

- **`Logger` singleton with eager `Dispatcher.CurrentDispatcher` capture** (`Logger.cs:43-46`) is a global problem for any command that runs on a non-UI thread first. Affects every plugin, not just Batch Rename.
- **`ServiceLocator` use in command bodies** (`SearchReplaceCommand.cs:29-30`) — this is the project's pattern across all commands, but it makes `SearchReplaceCommand` itself untestable. A constructor-injection pattern with a small factory at the Revit boundary would help every command.
- **`LecgDataGrid` reflection-based `SetAllBooleanProperty`** (`LecgDataGrid.cs:55-77`) is dead code in this view (Select All/None go through ViewModel commands), but may be live elsewhere — worth a global check before removing.
- **`Bootstrapper.cs:131`** registers `IFormulaUpdateService` with no consumer; a DI-graph linter / integration test would have caught this immediately.
- **`Apply Rename` button has no progress UI in the dialog itself** — the dialog closes and the log window pops up. For long batches the user sees a frozen Revit, no spinner, no busy cursor. That's a `RevitCommand` base-class concern, not Batch-Rename-specific.
- **The `EnumBindingSource` markup extension** referenced in `SearchReplaceView.xaml:98, 249, 343` is a project-wide utility — worth verifying it handles enum description attributes or returns the raw enum value (`CaseMode.Capitalize` reading awkwardly in the dropdown).
