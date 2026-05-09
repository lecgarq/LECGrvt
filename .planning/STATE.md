---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: Plugin Maturity
status: in_progress
last_updated: "2026-05-09T23:52:01.320Z"
progress:
  total_phases: 6
  completed_phases: 2
  total_plans: 15
  completed_plans: 14
---

# Project State

## Current Position
- **Phase**: 03-grid-collection-fixes
- **Plan**: 00 + 01 + 02 + 03 + 04 + 05 + 06 + 07 + 08 of 10 (COMPLETE) — Waves 0-5 done; Wave 6 (03-09 manual Revit verification) next
- **Status**: Plans 03-00..03-08 complete; build clean (0 errors, 0 warnings), 100/100 tests GREEN. Plan 03-08 swept the 8 selection-backed screens (AlignEdges, AlignElements, AssignMaterial, CategoryChanger, ChangeLevel, OffsetElevations, ResetSlabs, SimplifyPoints) via Path A: `RowItems` + `SetSelectionRows` lifted into shared `SelectionViewModel`; `ElementGridControl` embedded directly in `SelectionControl.xaml`; zero per-View XAML edits required. Plan 03-09 (Wave 6: phase-end manual Revit verification — REQ-01 acceptance + REQ-08/09/10 verification from Phase 02.5) is next.

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

## Last Session
- **Stopped at**: Completed 03-08-PLAN.md (selection-backed screen sweep — 8 VMs wired to shared SelectionViewModel.RowItems via Path A; ElementGridControl embedded in SelectionControl)
- **Date**: 2026-05-09

## Next Steps
1. Phase 03 Wave 6: Plan 03-09 phase-end manual Revit verification (REQ-01 acceptance) + REQ-08/09/10 verification from Phase 02.5.
2. Begin v1.2 milestone planning or next phase.
3. v1.2 follow-up: per-column header funnel chrome (visual UI) deferred from Plan 03-05 — `SetColumnFilter` API is functional; visual popup of distinct values per column remains.
