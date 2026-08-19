# Phase 1 Plan

## Goal

The Batch Rename preview stops throwing away the user's checkboxes and stops freezing the window on a scope click. Deselect three rows, change the Replace text, and those three rows are still deselected — even if a filter hid them in between.

Covers R5, R12 (restated), R13, R14 (restated), R20.

## Evidence

- `src/ViewModels/SearchReplaceViewModel.cs:256-257` — `PreviewItems.Clear()` then one `Add` per result. Every manual check is discarded, and every `Add` raises a `CollectionChanged` the `ICollectionView` re-filters and re-sorts against. This is both defects (R5 and R13) in two lines.
- `src/ViewModels/SearchReplaceViewModel.cs:219-222` — `RefreshScope()` calls `CollectBaseElements` inline on the dispatcher. Called from `SetExclusiveScope` (`:213`) and `Initialize` (`:192`), so every scope pill click runs it.
- `src/Services/Renaming/BaseElementCollectionService.cs:37-59` — `CollectBaseElements` is `FilteredElementCollector` plus `ElementLabelService.GetLabels(el)` per element. Pure Revit API; **cannot** be moved off the UI thread.
- `src/Services/Renaming/BaseElementCollectionService.cs:264` — FamilyParameter rows set `Id = familyId`. Every parameter of a family shares one id, so `Id` alone cannot key check-state.
- `src/Services/Renaming/SearchReplacePreviewService.cs:28-80` — `ProcessPreview` takes a materialized `List<ElementData>` and never touches `Document`. The existing `Task.Run` at `SearchReplaceViewModel.cs:246` is safe and stays.
- `src/Services/Renaming/SearchReplacePreviewService.cs:157,230` — `ProcessPreview` itself sets `IsChecked = false` on rows it marks non-renameable. Restoring check-state must therefore only ever *clear* a check, never set one.
- `src/InternalsVisibleTo.Tests.cs:3` — `InternalsVisibleTo("LECG.Tests")`. The repo's established test seam (`BatchRenameExecutionService.cs:416-535` uses it throughout). New helper methods go `internal`, not `public`.
- `src/ViewModels/BaseViewModel.cs:19` — `IsBusy` already exists. `src/Views/ConvertCadView.xaml:161` is the busy-panel precedent: a `Border` with `Visibility="{Binding IsBusy, Converter={StaticResource BoolToVis}}"`.
- `src/Views/SearchReplaceView.xaml:15-22` — the view declares its own `Resources` block with the `LecgTheme.xaml` merge inside it. That merge is load-bearing.
- `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs:23` — the VM constructs parameterless in tests. Nothing added may break that.
- `src/Controls/LecgDataGrid.cs:23-26` — row and column virtualization already on with recycling. R13/R14 are about the rebuild, not scrolling.

## Files changing

- `src/ViewModels/Components/BulkObservableCollection.cs` — **new**
- `src/ViewModels/SearchReplaceViewModel.cs`
- `src/Views/SearchReplaceView.xaml`
- `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs`
- `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs`

Anything outside this list is a deviation and gets said out loud.

## Steps

**1 — Baseline measurement (R14a, before).**
Add one test to `SearchReplacePreviewServiceTests`: 5,000 synthetic `ElementData` through `ProcessPreview` with an active Replace rule. Assert the returned count, not the duration — a timing assert is flaky. Run it, record the elapsed ms in this file. This is the "before" number and a scale regression guard.

**2 — `BulkObservableCollection<T>` (R13).**
~18 lines in `src/ViewModels/Components/`: subclass `ObservableCollection<T>` with one `ReplaceAll(IEnumerable<T>)` that mutates the protected `Items` and raises a single `Reset`. Change `_previewItems`/`PreviewItems` (`:111`) to that type — `Add`, `Count` and `GetDefaultView` all still work, so the existing tests and the two XAML `PreviewItems.Count` bindings are unaffected. Replace `:256-257` with one `ReplaceAll(results)`.
*Verifiable:* existing `SearchReplaceViewModelTests` still pass; a new test asserts one `CollectionChanged` (`Reset`) for an N-row replace.

**3 — Check-state memory (R5).**
Three `internal` members on the VM plus a `HashSet<string>` field:
- `internal static string CheckKey(ElementRowViewModel r)` → `$"{r.Type}|{r.Id}|{r.OriginalValue}"`
- `internal void HarvestCheckState()` — walk current `PreviewItems`; for rows with `IsRenameable == true`, add the key when unchecked, remove it when checked. Non-renameable rows are `ProcessPreview`'s decision, not the user's, and are skipped.
- `internal void ApplyCheckState(IEnumerable<ElementRowViewModel> rows)` — `if (memory contains key) row.IsChecked = false;` **with no else.** Never sets `true`, so it cannot resurrect a row `ProcessPreview` unchecked at `:157,230`.

Wire into `UpdatePreviewAsync`: harvest immediately before the replace, apply to `results` immediately after. Both inside the existing dispatcher block.
*Verifiable:* tests drive the three methods directly on a parameterless VM — no Revit. Cases: survives a rebuild; survives a round-trip where the row is absent from one rebuild; never re-checks a non-renameable row; two FamilyParameter rows sharing one `Id` are keyed independently.

**4 — Per-scope cache and busy state (R12).**
In `RefreshScope()` (`:219`): build a scope key from the nine scope bools, look it up in a `Dictionary<string, List<ElementData>>`, and only call `CollectBaseElements` on a miss. Around the miss path only:

```
IsBusy = true; BusyMessage = "Collecting …";
System.Windows.Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
try { collect; cache } finally { IsBusy = false; }
```

`DispatcherPriority.Render` (7) is above `Input` (5), so the overlay paints and no click is processed while the collector runs — a repaint without the reentrancy a `Dispatcher.Yield()` would open. `Application.Current` is null-guarded because the test runner has no WPF application. The cache is cleared in `Initialize` (`:192`); it is not invalidated mid-dialog because the dialog is modal — confirmed by `SearchReplaceCommand.cs:31` calling `view.ShowDialog()`.

Add `BusyMessage` to the VM and a busy `Border` over the preview grid in `SearchReplaceView.xaml`, copying the `ConvertCadView.xaml:161` shape and using theme tokens only.
*Verifiable:* build; a test asserts the second `RefreshScope` for an identical scope does not call the collection service again (NSubstitute `Received(1)`).

**5 — After measurement and full validation (R14a, after).**
Re-run the step-1 test, record the "after" ms here. Full build and suite.

**6 — Live measurement (R14b) and runtime check (R20) — deferred.**
Revit is not running: `mcp-server-for-revit` returned `connect to revit client failed` on 2026-08-18. The largest-real-model row count and the keystroke timing cannot be taken now, and neither can the busy-overlay binding be confirmed. This step is written down as pending, not silently dropped.

## Risks

| Risk | This plan |
|---|---|
| Transaction safety | **N/A** — the preview path performs no document writes. Nothing in the five changed files opens a transaction. |
| API context | `CollectBaseElements` stays on the UI thread, inside `IExternalCommand.Execute`'s call stack — a valid API context. The plan explicitly refuses to wrap it in `Task.Run`. The existing `Task.Run` around `ProcessPreview` is safe because that method never touches `Document` (`SearchReplacePreviewService.cs:28-80`). |
| Modeless WPF | **N/A** — `SearchReplaceCommand.cs:31` uses `ShowDialog()`. Modal, no `ExternalEvent`. |
| Units | **N/A** — no geometry, no measurements in internal units. |
| Geometry tolerance | **N/A** — no geometry. |
| Linked models | **N/A** — collectors run against the active `Document` only. |
| Parameters | `CollectBaseElements` reads `p.IsShared`, `p.IsReadOnly`, `p.Definition` — unchanged by this phase. No parameter writes. |
| Collectors | No collector is constructed inside a loop, and none is added. The cache *reduces* collector construction: repeat scope selections stop building one at all. |
| XAML binding | The busy `Border` binds `IsBusy` and `BusyMessage`; `BoolToVis` already exists in the view's resources (`SearchReplaceView.xaml:20`). **Compilation proves none of this** — the binding is confirmed only in step 6, in Revit. The `LecgTheme.xaml` merge at `:15-22` is not touched. |
| Deployment | No `.csproj`, `.addin`, or target-framework change. All builds use `-p:SkipRevitDeploy=true`. |

**Known limitation, accepted for this phase.** `ProcessPreview` computes cross-batch name collisions using each row's `IsChecked` at `:212-213`, which runs *before* `ApplyCheckState` restores the user's deselections. A row the user unchecked will still claim its name during collision detection, so another row can show a spurious collision Status. It is display-only — execution renames checked rows only. Fixing it properly means passing the memory into `ProcessPreview`, which CONTEXT.md puts out of scope. It gets a `ponytail:` comment naming the ceiling and lands in the Open list.

## Validation

```bash
dotnet build -p:SkipRevitDeploy=true
```

```bash
dotnet test -c Debug -p:SkipRevitDeploy=true
```

Expected: 0 errors, 0 warnings; suite green with the new tests (baseline 222 passed / 0 failed / 5 skipped).

Revit runtime: **not executable this session** — MCP reported `connect to revit client failed`. Pending steps, to be run when Revit is open:
1. Open Batch Rename; confirm the dialog renders and the grid populates.
2. Click a scope pill that is slow — confirm the busy panel appears and its text renders.
3. Click back to a previously loaded scope — confirm it returns instantly (cache hit).
4. Uncheck three rows, change the Replace text, confirm all three stay unchecked.
5. Uncheck a row, filter it out of the grid, filter back, confirm it is still unchecked.
6. Record the largest available model's row count and the keystroke-to-grid timing (R14b).

## Backwards check

If every step passes: check state survives rebuilds and filter round-trips (R5 ✓), the rebuild raises one notification instead of N (R13 ✓), repeat scope clicks cost nothing and the first is an explained wait (R12 ✓), the pipeline number is recorded before and after (R14a ✓). R14b and R20 are explicitly **not** achieved this session and are carried as pending — the phase cannot be called done until they run.

## Deviation from plan (step 4)

The plan said step 4 would be verified by "a test asserts the second `RefreshScope` for an
identical scope does not call the collection service again (NSubstitute `Received(1)`)".
**That test is not achievable and was replaced.**

Reaching the cache requires `Initialize(ISearchReplaceService, Document)`, and a `Document`
cannot be constructed in the xUnit runner — the same reason five tests in
`BaseElementCollectionServiceTests` / `SearchReplaceServiceTests` are already skipped. Worse,
the first attempt found that *setting any scope property at all* throws
`FileNotFoundException: RevitAPI` in tests: `SetExclusiveScope` calls `RefreshScope`, whose
body references `ISearchReplaceService`, and the JIT resolves that interface — loading
`RevitAPI` — before the method's `_service == null` guard ever runs. This is pre-existing, not
a regression; no earlier test had set a scope flag, so it was latent.

Replacement: the scope key moved into a pure `internal static BuildScopeKey(...)` and is
tested directly — distinctness across all nine scopes, stability for identical flags, and
distinctness for combined flags. That covers the part that could silently be wrong (one
scope's elements served for another). The cache **hit** is now a Revit smoke-test step, added
to the pending list below.

## Measurements

- **R14a before: 8 ms** for 5,000 rows through `ProcessPreview` (2026-08-18).
- **R14a after: 8 ms** — unchanged, and expected: this phase did not touch `ProcessPreview`.
### R14b — live collector measurement (2026-08-18)

Taken through `mcp-server-for-revit` against **Snowdon Towers Sample Architectural** (the
largest document open: 1,881 types, 37,877 non-type elements, 7,598 `FamilyInstance`s, 286
families, 7 linked models). Code mirrors `CollectBaseElements`'s real paths per scope. This
measures the **collector**, not the dialog — the deployed add-in still predates this phase.

| Scope | Rows | Collect ms |
|---|---:|---:|
| Types | 1,881 | 10 |
| Families | 286 | 7 |
| Views / Sheets | 304 / 55 | 7 |
| Materials | 220 | 6 |
| FillPatterns | 71 | 6 |
| **FamilyParameters** | **1,171** | **437** |

Breakdown of the FamilyParameters scope: grouping symbols 6 ms, Phase A (per-symbol parameter
scan plus `LabelUtils.GetLabelForGroup` per row) +53 ms, **Phase B (scanning all 7,598
`FamilyInstance`s for instance-only parameters) +378 ms — 86% of the total.**

**Three conclusions.**

1. **No cancel path is needed in this phase.** The worst scope is 437 ms, not tens of seconds.
   The busy panel is the right answer and the CONTEXT.md open question is closed. Revisit only
   if a model an order of magnitude larger shows up.
2. **The cache earns its place on exactly one scope.** Eight scopes are single-digit
   milliseconds; FamilyParameters is 437 ms and is the one a user pays for repeatedly today.
3. **The largest real scope is 1,881 rows**, so the synthetic 5,000-row test is ~2.7× headroom
   over anything this model produces. It is a regression guard, not a proxy for real load.

**Phase B is the obvious next optimisation** — it does 86% of the work to find instance-only
parameters, and one instance per family would do (the existing code already tracks
`processedInstanceFamilies` for exactly that but still enumerates every instance). Out of scope
here; recorded for a later phase.

**What the measurement established.** `ProcessPreview` is not the bottleneck and never was.
8 ms for 5,000 rows means the debounce and the `Task.Run` around it are essentially free, and
the cost this phase targeted is entirely (a) the UI rebuild, now one `Reset` instead of 5,000
notifications, and (b) `CollectBaseElements`, now paid once per scope instead of on every
click. Had this measurement been skipped, the obvious-looking move would have been to
optimise `ProcessPreview` — which would have bought nothing.
