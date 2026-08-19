# Phase 1 Context

## Goal

Changing a rule or a scope no longer discards the user's checkboxes and no longer freezes the window. The visible behaviour change: deselect three rows, tweak the Replace text, and those three rows are still deselected.

**Covers:** R5, R12 (restated), R13, R14 (restated), R20

## Decisions

- **R12 — `CollectBaseElements` cannot leave the UI thread.** It is pure Revit API: `FilteredElementCollector` plus `ElementLabelService.GetLabels(el)` per element (`src/Services/Renaming/BaseElementCollectionService.cs:37-59`), and `SEMANTICS.md` is unambiguous that the API is main-thread only. Decided: **cache the collected `List<ElementData>` per scope, and show an honest busy state for the first collect of each scope.** The collect stays synchronous on the dispatcher; the second click on any scope pill becomes free. Rejected: `Dispatcher.Yield()` chunking (lets the user click mid-collect inside a modal — the reentrancy bug class already open on `ExternalEventCommand`), and optimising the collectors (unbounded, and it edits collection logic that 5 skipped tests would otherwise have covered).
- **R5 — check state is remembered for the session, not just across a refresh.** A set of explicitly-unchecked keys, held for the life of the dialog. Uncheck three rows, filter them away, filter back — still unchecked. Rejected: snapshot-and-restore of the current list only, because filtering away and back would silently re-arm rows the user deliberately excluded, and re-arming is the dangerous direction for a rename.
- **R5 key is `(Type, Id, OriginalValue)`, not `Id + Type`** as the roadmap first said. FamilyParameter rows set `Id = familyId` (`BaseElementCollectionService.cs:264`), so every parameter of a family shares one `Id`. `OriginalValue` disambiguates them and is stable across refreshes (`NewValue` is not).
- **R14 measures the largest real model available, with its row count recorded**, plus a synthetic 5,000-row unit test for the pipeline cost. The original "5,000-row preview" threshold asserted a scale nobody has confirmed is reachable.

## Assumptions confirmed

- `ProcessPreview` is pure — it takes a materialized `List<ElementData>` and never touches `Document` (`src/Services/Renaming/SearchReplacePreviewService.cs:28-80`). The existing `Task.Run` around it (`SearchReplaceViewModel.cs:246`) is safe and stays.
- `BaseViewModel.IsBusy` already exists (`src/ViewModels/BaseViewModel.cs:19`); `ConvertCadView.xaml:161` is the busy-overlay precedent. No new mechanism.
- R13 gets a small `ObservableCollection` subclass raising a single `Reset`, **not** a swapped collection instance. `_previewView` is `CollectionViewSource.GetDefaultView(PreviewItems)` bound to that instance (`SearchReplaceViewModel.cs:122`); replacing the collection would drop the sort, the filter and the scroll position.
- The `ICollectionView` shape stays as-is. Phase 3 binds against it.
- Debounce stays at 150 ms (`SearchReplaceViewModel.cs:243`) unless the measurement says otherwise.

## Constraints

- **Revit API is main-thread only.** No `Task.Run`, no background thread touching `Document`. `docs/ai/revit-api/SEMANTICS.md` § API context and threading.
- **`SearchReplaceView.xaml:15-22` declares its own `Resources` block**, which replaces the dictionary `LecgWindow`'s constructor populates. The `LecgTheme.xaml` merge inside it is load-bearing — remove it and every `StaticResource` fails at runtime, invisible to compiler and test suite.
- **Validation flags are mandatory**: `dotnet build -p:SkipRevitDeploy=true`, `dotnet test -c Debug -p:SkipRevitDeploy=true`. A plain build overwrites the live Revit 2026 add-in folder.
- **5 tests skip outside Revit** in `BaseElementCollectionServiceTests` and `SearchReplaceServiceTests` — both in this phase's blast radius. A green suite does not mean those paths are covered; check what they would have proven before trusting it.
- **XAML compiling proves nothing about bindings.** Any busy-overlay binding fails only at runtime.

## Out of scope

- Selection commands (`SelectAll`/`SelectNone`/Invert) and the count displays — Phase 2. This phase preserves check state; it does not change who sets it.
- Per-column filters, the Category dropdown, regex validation — Phase 3.
- Section collapsing, grid grouping, the Extension tile, the scope control's appearance — Phase 4.
- Any change to `ProcessPreview`, `SearchCriteria`, `RenameRuleContext`, or `BatchRenameExecutionService`.
- Optimising the collectors themselves. Caching avoids repeat cost; it does not make the first collect faster.

## Open

- **How slow is the first collect, actually?** Nobody has measured it. If Types or FamilyParameters on a real model takes 30 s, a busy overlay is not enough and the phase needs a cancel path. Resolved by the R14 measurement, which should be taken **first**, before the caching work — the number decides whether cancellation gets added to this phase or deferred.
- Whether the per-scope cache needs invalidating if the user edits the model behind the modal dialog. The dialog is modal, so the document cannot change while it is open — but confirm `ShowDialog` is genuinely modal here before relying on it.
