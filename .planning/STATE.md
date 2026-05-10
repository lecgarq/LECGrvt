---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: Plugin Maturity
status: unknown
last_updated: "2026-05-10T21:44:11.804Z"
progress:
  total_phases: 6
  completed_phases: 4
  total_plans: 26
  completed_plans: 22
---

# Project State

## Current Position
- **Phase**: 05-verification-and-polish (IN PROGRESS)
- **Plan**: 1 of 5 COMPLETE (05-00 done)
- **Status**: Phase 5 Wave 0 complete. 3 new xUnit fixtures created + BaseElementCollectionServiceTests deepened + 05-VERIFICATION.md matrix scaffolded + 05-VALIDATION.md wave-0-locked. Suite: 134 GREEN, 45 Skipped, 0 Failed. Plans 05-01/02/03/04 remain.

## Phase 4 Progress
- Plan 04-00 COMPLETE: Wave 0 test scaffolds — IsRenameable field on ElementRowViewModel; skip-gated RED fixtures for FormulaUpdate, BatchRenameSafeRename; VALIDATION.md per-task map.
- Plan 04-01 COMPLETE: Narrowed GetRenameSkipReason to built-in/reporting/name-conflict; added EvaluateFamilyParamSkipReason + EvaluateStandardItemSkipReason pure-data helpers; ApplyPreFlightSkipReasons delegate pattern; GetStandardItemSkipReason via Family.IsInPlace proxy (REQ-04 detection).
- Plan 04-02 COMPLETE: SearchReplacePreviewService Status + IsRenameable + side-effect count (formulaCount + dimCount composite); SearchReplaceView.xaml IsEnabled + TextMutedBrush muted-row + tooltip binding (REQ-04 UI).
- Plan 04-03 COMPLETE: IFormulaUpdateService injected into BatchRenameExecutionService; CollectFormulaUpdates pure-data helper; formula-update loop inside per-param SubTransaction (REQ-02).
- Plan 04-04 COMPLETE: BuildDimensionsByLabelName collector; ExecuteDimensionReassignments + FormatSafeRenameLog pure-data helpers; dimension reassignment loop in SubTransaction with FindFamilyParameterByName re-fetch (Pitfall 2 guard); LogRenameSuccess dimCount=0 default (REQ-03). 3 REQ-03 skip-gated tests GREEN.
- Plan 04-05 COMPLETE: Phase-end verification sign-off. 23/24 checklist items PASS; C1 (Dimension.FamilyLabel null-clear) marked N-A with follow-up note. REQUIREMENTS.md REQ-02/03/04 flipped to Complete. Phase 4 closed.

## Phase 1 Summary
Phase 1 (Research & Foundation) is complete. Service consolidation and formula update foundation are in place.

## Phase 2 Progress
- Plan 02-01 COMPLETE: SubTransaction loop, EnsureCurrentType guard, in-transaction false-negative check removal, EnsureParametersPersistInGroup restricted to post-reload.
- Plan 02-02 COMPLETE: TryReplaceSharedParameterGroup clear-restore sequence for shared parameters with cross-references.

## Phase 3 Progress
- Plan 03-00 COMPLETE: Wave 0 test scaffolds (REQ-01) — 4 xUnit fixtures (1 GREEN consumed by 03-01, 3 RED skip-gated for plans 03-03/04/05); VALIDATION.md flags `nyquist_compliant: true`, `wave_0_complete: true`; per-task map covers plans 03-01..09.
- Plan 03-01 COMPLETE: ElementLabelService — locale-safe GetLabels(Element) + GetLabelsFromRaw pure-string overload; 11/11 GREEN.
- Plan 03-02 COMPLETE: ElementRowViewModel shared row model under LECG.ViewModels.Components.
- Plan 03-03 COMPLETE: BaseElementCollectionService null-Category skip removed (REQ-01) — typeCollector + GraphicsStyle paths now route through ElementLabelService.GetLabels; FamilyInstance null-Family skip retained but now logs LogView warning. BaseElementCollectionServiceTests 4/4 GREEN. Pre-existing CS0029/CS1503 errors in SearchReplaceService/SearchReplacePreviewService deferred to Plan 03-04 (per the wave-1→wave-2 handoff decision).
- Plan 03-04 COMPLETE: SearchReplacePreviewService.ProcessPreview returns List<ElementRowViewModel> with Category/ParamGroup/IsInstance/IsReadOnly propagated; ReplaceItem class deleted; consumer chain (BatchRenameExecutionService, ISearchReplaceService, SearchReplaceService) migrated; XAML rebound (ElementId→Id, ElementName→Name); 3-W0-03 RED tests un-Skipped and GREEN; build clean; 98/100 tests pass.
- Plan 03-05 COMPLETE: ElementGridControl shared UserControl (Sel|Type|Category|Name|Status, wraps LecgDataGrid) shipped; SearchReplaceViewModel.PreviewView ICollectionView wires Category-asc default sort + AND-combined filter (FilterCategory dropdown + SetColumnFilter per-column predicate API); SearchReplaceView.xaml column order locked to ☑|Type|Category|Original|New; FilterCategory filtering removed from ProcessPreview; 3-W0-04 RED tests un-Skipped and GREEN (100/100 suite GREEN, build clean). User visually verified Batch Rename grid in Revit (column order, default sort, sort flip, FilterCategory, no blank rows, FamilyParameter scope all confirmed). Per-column header funnel chrome deferred to v1.2 follow-up — functional plumbing (SetColumnFilter API) proven via unit test.
- Plan 03-06 COMPLETE: Text-summary batch A migration (REQ-01) — DivideToposolid, FixPoints, ConvertCad VMs/Views migrated from `ObservableCollection<string> SelectedElementSummaries` to `ObservableCollection<ElementRowViewModel> RowItems` rendered through `ElementGridControl`. Name/Category resolved via `ElementLabelService.GetLabels` (non-blank invariant). Per-screen Status: DivideToposolid layer-count outcome ("Ready to divide (N layers)" / "Already single layer" / "Layer count unavailable"); FixPoints "Level: {name}"; ConvertCad "Type: {typeName}". ConvertCad had no legacy SelectedElementSummaries (single-select screen) — RowItems populated on `SetSelection` with one row to honour must_haves contract. Build clean, 100/100 tests GREEN. Manual Revit verification deferred to Plan 03-09.
- Plan 03-07 COMPLETE: Text-summary batch B migration (REQ-01) — SplitBoundaries, ConvertToposolidToFloor, ConvertFloorToToposolid VMs/Views migrated from `ObservableCollection<string> SelectedElementSummaries` to `ObservableCollection<ElementRowViewModel> RowItems` rendered through `ElementGridControl`. SplitBoundaries Status: boundary-count outcome ("Ready to split (N boundaries)" / "No split needed" / "Boundary count unavailable"). Conversion screens Status: "<SourceTypeName> @ <LevelName>" (preserves prior DescribeElement summary verbatim). Cross-VM grep `ObservableCollection<string> SelectedElementSummaries` returns zero hits across all six text-summary VMs. Build clean (0/0); 100/100 tests GREEN.
- Plan 03-08 COMPLETE: Selection-backed screen sweep (REQ-01) — Path A chosen. `SelectionViewModel` upgraded with `ObservableCollection<ElementRowViewModel> RowItems` + dual `SetSelectionRows` overloads (Reference+Document for 7 ref-driven VMs, Element for ChangeLevel) + Dispatcher-marshalled mutations. `SelectionControl.xaml` embeds `ElementGridControl` (visibility bound to `HasSelection`). 8 VMs (AlignEdges, AlignElements, AssignMaterial, CategoryChanger, ChangeLevel, OffsetElevations, ResetSlabs, SimplifyPoints) call `Selection.SetSelectionRows` after `UpdateSelection`; 5 VMs gained a `Document` parameter; 5 Views + 5 Commands updated for the cascade. Build clean (0/0); 100/100 tests GREEN.
- Plan 03-09 COMPLETE: Phase-end manual Revit verification (REQ-01 acceptance) — 22/22 verification items PASS across Batch Rename group A (column order, default Category sort, sort flip, FilterCategory, AND-combined filter, no blank rows, FamilyParameter scope fallback), 6 text-summary screens (DivideToposolid, FixPoints, ConvertCad, SplitBoundaries, ConvertToposolidToFloor, ConvertFloorToToposolid), 8 selection-backed screens (AlignEdges, AlignElements, AssignMaterial, CategoryChanger, ChangeLevel, OffsetElevations, ResetSlabs, SimplifyPoints), LogView fallback warnings observed for null-Category elements. REQ-01 flipped Pending → Complete in REQUIREMENTS.md. Phase 3 closed.

## Phase 2.5 Progress
- Plan 02.5-01 COMPLETE: Compact Styles (REQ-08) — locale-safe text-style signature using BuiltInParameter.TEXT_ALIGNMENT + orientation sentinel.
- Plan 02.5-02 COMPLETE: Category Changer (REQ-09) — refuse-all pre-flight in SwapInstances. Manual Revit verification pending (phase-end session per CONTEXT.md §D).
- Plan 02.5-03 COMPLETE: Convert Family (REQ-10) — pre-flight validator + load-before-delete ordering + exact symbol-name match + hosted/curve placement overloads. Manual Revit verification pending (phase-end session per CONTEXT.md §D).

## Decisions
- SubTransaction per parameter in MoveParamsToGroup: one failed move logs and continues rather than aborting all params.
- Remove GetGroupTypeId() post-call check from TrySetParameterGroup: in-transaction read is a false-negative; group persistence verified post-reload.
- EnsureParametersPersistInGroup now only checks group membership, not formula presence.
- [Phase 02]: Add using LECG.Core.Rename to FormulaAutoGroupingCommand.cs; clear formula with string.Empty not null; restore is best-effort using FindParamByName after ReplaceParameter
- [Phase 02.5]: Use BuiltInParameter.TEXT_ALIGNMENT (not TEXT_ALIGN_HORZ) for type-level horizontal alignment — TEXT_ALIGN_HORZ is instance-level on TextNote
- [Phase 02.5]: Orientation sentinel: LookupParameter first (English path), fall back to ORIENTATION_UNREADABLE:{type.Id} when null — safe-by-default, never falsely merges distinct types
- [Phase 02.5-02]: Refuse-all (not partial-success) for mixed batches — any unsupported instance aborts entire SwapInstances swap
- [Phase 02.5-02]: GetUnsupportedReason check order: LocationCurve > HostFace > Host > non-LocationPoint; HostFace before Host gives more precise label for face-hosted instances with both set
- [Phase 02.5-02]: LocationCurve / hosted / face-hosted placement paths deferred to v1.2; no SubTransaction; logging surface stays LogView only
- [Phase 02.5]: FamilyLoadOptionsFactory verified to force overwrite — load-before-delete safe with live instances (REQ-10)
- [Phase 02.5]: Face-hosted preservation deferred to v1.2: FamilyInstanceData lacks HostFace capture; Branch 3 fallback documented as known limitation
- [Phase 02.5]: FindReplacementSymbolByName uses ordinal exact-name match; no first-symbol fallback; missing type name throws before delete (REQ-10)
- [Phase 03]: ElementRowViewModel uses [ObservableProperty] only for IsChecked; identity/display fields are plain auto-properties (set once at row creation)
- [Phase 03]: ReplaceItem retained intact in Plan 03-02; Plan 03-04 owns SearchReplacePreviewService migration to keep wave-1 builds clean
- [Phase 03]: ElementLabelService placed in src/Services (LECG.csproj) — GetLabels(Element) requires Revit API; pure-string overload supports unit-testing without Revit Document
- [Phase 03]: Locale-safe category labels via LabelUtils.GetLabelFor((BuiltInCategory)cat.Id.Value); explicit null-check on Category (NOT element.Category?.Id.Value chain — short-circuits to nullable long which mis-casts)
- [Phase 03]: ALL_MODEL_TYPE_NAME parameter fallback for blank Element.Name (non-English Revit pitfall); Logger.LogWarning fires only when fallback actually triggered
- [Phase 03-00]: Skip-gated RED tests with reason strings naming the implementing plan — keeps suite green and gives plan authors a greppable flip-point
- [Phase 03-00]: One non-skipped anchor test per fixture ensures `dotnet test --filter` discovers it even when all behavioral tests are skip-gated
- [Phase 03-00]: SearchReplacePreviewService anchor DisplayName-marked "delete in 03-04" — Plan 03-04 must remove it during ElementRowViewModel migration
- [Phase 03-00]: Canonical local build command while Revit is open: `dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` (bypasses MSB3027 file-lock on ProgramData addins folder)
- [Phase 03-03]: BaseElementCollectionService unit tests target ElementLabelService.GetLabelsFromRaw (the SSoT) instead of introducing a NormalizeForRow shim — keeps the public surface focused on a single helper
- [Phase 03-03]: GraphicsStyle null-GraphicsStyleCategory now produces a fallback row + LogView warning (not a silent skip) — REQ-01's "every collected element produces a row" applies even when category metadata is missing
- [Phase 03-03]: FamilyInstance null-Family skip retained (no usable parent label) but now emits Logger.LogWarning so the skip is observable
- [Phase 03]: [Phase 03-04]: Atomic ReplaceItem -> ElementRowViewModel swap across preview + execution + VM + XAML in single plan; transient broken state between Task 1/2 commits is acceptable per RESEARCH Pitfall 2
- [Phase 03]: [Phase 03-04]: ProcessPreview is pure pass-through for Category — no fallback computation; the collection layer (Plan 03-03) owns the non-blank invariant
- [Phase 03]: [Phase 03-04]: Field-rename at consumers — ElementId -> Id, ElementName -> Name (single naming convention via ElementRowViewModel)
- [Phase 03-05]: Batch Rename keeps inline LecgDataGrid (not ElementGridControl) because Original/New columns don't fit the generic Sel|Type|Category|Name|Status row shape; ElementGridControl reserved for migration sweep in Plans 03-06..08
- [Phase 03-05]: Per-column filter exposed as SetColumnFilter(name, predicate) API on the ViewModel; visual WPF header funnel chrome (popup of distinct values) deferred to v1.2 follow-up — functional plumbing proven via unit test
- [Phase 03-05]: ICollectionView ownership lives on the ViewModel (PreviewView), not the control — each screen owns its own sort/filter semantics
- [Phase 03-05]: FilterCategory predicate moved out of SearchReplacePreviewService.ProcessPreview; collection layer (Plan 03-03) owns invariants, ICollectionView owns post-collection filtering (no preview re-run on filter change)
- [Phase 03]: [Phase 03-07]: Conversion screens (ConvertToposolidToFloor / ConvertFloorToToposolid) Status format = '<TypeName> @ <LevelName>' — preserves prior DescribeElement summary verbatim; eligibility detection deferred (no IConversionService eligibility API exists; out of scope for REQ-01)
- [Phase 03]: [Phase 03-06]: ConvertCad single-select screen — RowItems populated on SetSelection with one row; legacy screen had no SelectedElementSummaries to migrate, but must_haves contract honoured by adding the grid below SelectionControl in Selection-mode only
- [Phase 03]: [Phase 03-06]: Status column content per screen — DivideToposolid='Ready to divide (N layers)/Already single layer/Layer count unavailable'; FixPoints='Level: {name}'; ConvertCad='Type: {typeName}'
- [Phase 03-08]: Path A chosen — embed ElementGridControl in SelectionControl.xaml; eliminates 8 per-View XAML edits and guarantees grid consistency across selection-backed screens
- [Phase 03-08]: SelectionViewModel.SetSelectionRows ships two overloads (Reference+Document, Element) — picks the right one per call-site without forcing premature Reference-resolve at the View layer
- [Phase 03-08]: WPF Dispatcher.Invoke wrapping inside SetSelectionRows — defends against future Revit ExternalEvent callers without mandating a thread contract on consumers
- [Phase 03-08]: Grid Visibility bound to HasSelection — empty state stays clean; no empty grid below 'No X selected' summary
- [Phase 04-00]: Wave 0 skip-gated RED pattern reused from Phase 03-00 — one anchor per fixture, all behavioural tests skip-gated naming the implementing plan ID
- [Phase 04-00]: IsRenameable is a plain auto-property on ElementRowViewModel (NOT [ObservableProperty]) — follows Phase 03 decision that only IsChecked is observable
- [Phase 04-00]: FormulaUpdateServiceTests and BatchRenameSafeRenameTests are separate fixtures — FormulaUpdate covers wiring (04-03), BatchRenameSafeRename covers execution paths (04-03 REQ-02 + 04-04 REQ-03 + 04-01 REQ-04)
- [Phase 04-02]: ElementData.Formula + IsDimensionLabel fields carry side-effect metadata from collection to preview without Revit API
- [Phase 04-02]: Preview skip detection uses IsReadOnly as proxy for read-only/reporting FamilyParameter skip conditions
- [Phase 04-02]: TextMutedBrush used for muted-row Foreground in SearchReplaceView (Brushes.xaml project convention); Sel column converted to DataGridTemplateColumn for IsEnabled binding
- [Phase 04-01]: Extracted EvaluateFamilyParamSkipReason + EvaluateStandardItemSkipReason pure-data helpers for unit testing without Revit API
- [Phase 04-01]: ApplyPreFlightSkipReasons takes Func<ElementRowViewModel, string?> delegate — decouples loop from Revit API; loop runs OUTSIDE _transactionService.Run (pre-flight read-only pass)
- [Phase 04-01]: GetStandardItemSkipReason uses Family.IsInPlace (not IsSystemFamily) as proxy — Revit 2026 API does not expose Family.IsSystemFamily directly
- [Phase 04-03]: IFormulaUpdateService injected as last constructor parameter in BatchRenameExecutionService; DI registration in Bootstrapper.cs unchanged — MS DI resolves automatically
- [Phase 04-03]: Injectable constructor test uses reflection instead of NSubstitute — Castle DynamicProxy cannot proxy ITransactionService without RevitAPI.dll; reflection confirms the parameter type is accepted without RevitAPI dependency
- [Phase 04-03]: CollectFormulaUpdates extracted as internal static pure-data helper — enables unit testing of formula-update logic without Revit API; formulaReferenced.Contains opt-in skips loop for non-referenced params
- [Phase 04-04]: ExecuteDimensionReassignments accepts List<Action> not List<Dimension> — decouples helper from Revit API; unit tests inject mock actions, production builds dim.FamilyLabel setter closures
- [Phase 04-04]: LogRenameSuccess dimCount=0 default — backward-compatible extension; FormatSafeRenameLog 4-branch composite format centralized for testability
- [Phase 04-04]: SearchReplacePreviewService dimensionCount stays IsDimensionLabel?1:0 — preview layer has only bool flag, not Dimension object count; consistent at single-param-label granularity
- [Phase 04-05]: Verification accepted as trust-based (131/131 unit tests); C1 Dimension.FamilyLabel null-clear behavior not observed live — deferred follow-up if production issues surface
- [Phase 05]: Wave-0 skip-gated RED pattern reused from Phases 03-00/04-00 — one anchor per new fixture, all behavioural tests skip-gated naming the implementing plan ID
- [Phase 05]: [Phase 05-00]: BaseElementCollectionServiceTests uses existing 4 GREEN tests as anchor — no separate Fixture_Anchor_Exists added (fixture already enumerates)
- [Phase 05]: [Phase 05-00]: Revit-bound collector paths (FilteredElementCollector, GraphicsStyle null-category) skip-gated with manual verification pointer to 05-VERIFICATION.md

## Performance Metrics
| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 02    | 01   | 15min    | 2     | 2     |
| Phase 02 P02 | 10min | 1 tasks | 1 files |
| Phase 02.5 P01 | 15min | 1 tasks | 1 files |
| 02.5  | 02   | 1min     | 1     | 1     |
| Phase 02.5 P03 | 18min | 3 tasks | 2 files |
| Phase 03 P02 | 6min | 1 tasks | 1 files |
| Phase 03 P01 | 8min | 1 tasks | 2 files |
| Phase 03 P00 | 6min | 3 tasks | 4 files |
| Phase 03 P03 | 12min | 2 tasks | 2 files |
| Phase 03 P04 | 18min | 2 tasks | 9 files |
| Phase 03 P05 | 45min | 3 tasks | 5 files |
| Phase 03 P07 | 12min | 2 tasks | 6 files |
| Phase 03 P06 | 3min | 2 tasks | 6 files |
| Phase 03 P08 | 9min | 2 tasks | 20 files |
| Phase 03 P09 | 1min | 1 tasks | 4 files |
| Phase 04 P02 | 35min | 2 tasks | 5 files |
| Phase 04 P01 | 40min | 2 tasks | 3 files |
| Phase 04 P03 | 20min | 2 tasks | 3 files |
| Phase 04 P04 | 12min | 1 tasks | 2 files |
| Phase 05 P00 | 15min | 3 tasks | 6 files |

## Phase 5 Progress
- Plan 05-00 COMPLETE: Wave 0 scaffolds — 3 new xUnit fixtures (RenameRulePipelineServiceTests, SearchReplaceServiceTests, BatchRenameExecutionServiceTests) with anchor + skip-gated RED rows; BaseElementCollectionServiceTests deepened with 12 skip-gated RED rows naming plans 05-01 and Revit-manual paths; 05-VERIFICATION.md 6-row matrix scaffold; 05-VALIDATION.md flipped to nyquist_compliant true, wave_0_complete true, wave-0-locked. Suite: 134 GREEN, 45 Skipped, 0 Failed.

## Last Session
- **Stopped at**: Completed 05-00-PLAN.md — Wave 0 scaffolds. 3 new fixtures + BaseElementCollectionServiceTests deepened + 05-VERIFICATION.md matrix + 05-VALIDATION.md flipped. 134 GREEN, 45 Skipped, 0 Failed.
- **Date**: 2026-05-10

## Next Steps
1. Phase 5 (Verification & Polish — REQ-05) planning — only requirement remaining in v1.1 milestone.
2. v1.2 follow-up: per-column header funnel chrome (visual UI) deferred from Plan 03-05 — `SetColumnFilter` API is functional; visual popup of distinct values per column remains.
3. v1.2 follow-up: investigate `Dimension.FamilyLabel` null-clear requirement — C1 was not observed live; if dimension reassignment issues surface in production use, add a null-clear step before setter assignment in the dimension reassignment loop.
