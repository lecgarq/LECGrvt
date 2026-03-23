# Current Technical Debt Audit

## Verification Snapshot

- `dotnet build LECG.sln -c Debug -v minimal`: passing
- `dotnet test LECG.Tests/LECG.Tests.csproj -c Debug`: passing (`58` tests)
- `dotnet format whitespace LECG.csproj` completed successfully
- current project builds are clean with `0` warnings and `0` errors on this machine

## High-Signal Debt

### 1. Broad exception handling is now mostly confined to outer safety boundaries

- Unfiltered `catch (Exception)` boundaries in production code: `2`
- Remaining unfiltered boundaries are the intentional outer host guards in `src/App.cs` and `src/Core/RevitCommand.cs`
- Main residual risk: the largest service files are still exception-heavy and orchestration-heavy even when those catches are now filtered to expected Revit/runtime shapes.
- Recent service-level cleanup in this pass:
- `src/Services/ConversionService.cs`, `src/Services/LinkedModelExportService.cs`, and `src/Services/SplitBoundariesService.cs` now filter their former broad per-item workflow catches to expected Revit/runtime or IO exception shapes instead of swallowing every exception.
- Remaining structural hotspots:
- `src/Services/FamilyConversionService.cs` was partially tightened in this pass:
  - replacement placement now uses explicit level resolution and explicit fallback placement
  - broad inner catches were replaced with narrower placement/apply handling
  - the outer family-group catch now only swallows expected conversion failures instead of every exception shape
  - per-family-group processing, execute/finalize handling, template resolution, family instance capture, old-instance deletion, replacement symbol lookup, and replacement placement now live behind focused helpers instead of one dense inline block
- `src/Commands/FormulaAutoGroupingCommand.cs` was partially tightened in this pass:
  - `EditFamily`, `LoadFamily`, and `Close(false)` now use localized expected-failure handling
  - the inner broad `ProcessProjectFamily` catch was removed in favor of explicit lifecycle helpers
  - the project-family loop now only swallows expected family-processing exceptions
  - formula reads, lock checks, type-value preservation, and restore paths now use explicit helper methods instead of multiple bare catches
- Additional medium-size service hotspots were tightened in this pass and are no longer top broad-catch risks:
  - `src/Services/FamilyConversionExecutionService.cs`
  - `src/Services/FamilyTemplatePathService.cs`
  - `src/Services/MaterialPbrService.cs`
  - `src/Services/PurgeReferenceScannerService.cs`
  - `src/Services/SexySunSettingsService.cs`
  - `src/Services/OffsetService.cs`
  - `src/Services/TextStyleCompactionService.cs`
  - `src/Services/SimplifyPointsService.cs`
  - `src/Services/CadCurveFlattenService.cs`
  - `src/Services/CadCurveTessellationService.cs`
  - `src/Services/CadLineMergeService.cs`
  - `src/Services/CadPolylineExtractionService.cs`
- Additional UI/framework helpers were tightened in this pass and are no longer broad-catch hotspots:
  - `src/Core/SelectionCoordinator.cs`
  - `src/Core/Ribbon/RibbonService.cs`
  - `src/Core/Ribbon/RibbonFactory.cs`
  - `src/Views/Base/LecgWindow.cs`
  - `src/ViewModels/CategoryChangerViewModel.cs`
  - `src/ViewModels/SearchReplaceViewModel.cs`
  - `src/App.cs` resource-dictionary initialization path
- Additional guard cleanup landed in this pass:
  - `src/App.cs` global exception-handler logging fallbacks now filter expected WPF/runtime failures instead of using bare catches
  - `src/Commands/CategoryChangerCommand.cs` platform-limit transplant fallback is now tied to expected category-change exceptions instead of any exception whose message happened to match
- Remaining intentionally broad boundaries are now mostly outer host guards:
  - `src/Core/RevitCommand.cs` command execution boundary now has explicit active-document initialization and extracted failure handling, but it intentionally remains the outer command safety net
  - `src/App.cs` startup boundary remains the outer add-in startup safety net
  - global unhandled-exception registration in `src/App.cs` now uses shared helper methods, but it intentionally remains a last-resort host guard
- `src/Services/PurgeContext.cs` was partially tightened in this pass:
  - material-reference scan helpers now use explicit expected-failure handling instead of broad `Exception` catches
  - targeted element/category access helpers were narrowed the same way
  - `Create` now delegates type, instance, fill-pattern, and level collection to focused helpers, so the bootstrap path is less monolithic
  - the service still has broad catch density in other scan paths and remains a top review hotspot
- `src/Services/CompactionSharedHelper.cs` was partially tightened in this pass:
  - parameter rewiring and view/filter override indexing now use explicit expected-failure handling
  - `TryDeleteElement` no longer swallows every exception shape
  - shared view/category/filter override indexing and rewiring now route through focused helper methods instead of repeating the same orchestration inline
  - the file remains worth another pass, but the main override plumbing is less dense
- `src/Services/PurgeExtendedElementService.cs` was partially tightened in this pass:
  - `PurgeConstraints` now validates `doc` and only swallows expected delete failures
  - view filter access now uses explicit Revit/runtime exception handling instead of a bare catch
- `src/Services/AlignEdgesBoundaryCollectionService.cs` was tightened in this pass:
  - toposolid sketch boundary collection now skips only expected Revit/runtime access failures
- `src/Services/PurgeLinePatternService.cs` was partially tightened in this pass:
  - category pattern lookups, view category overrides, and filter override scans now use explicit expected-failure handling
  - the service no longer downgrades every unexpected exception in those scan paths into a warning
- `src/Services/BatchRenameExecutionService.cs` was partially tightened in this pass:
  - family parameter rename processing now closes edited family documents through a guarded `finally` path
  - family parameter renames and object-style swap/delete paths now use explicit expected-failure handling instead of broad workflow catches
  - generic element rename and graphics-style rename failures are now filtered to expected Revit/runtime exception shapes
  - checked family-parameter grouping and rename execution are now split into helper methods, so the main workflow is less dense and easier to review
  - the file still has structural weight, but it is no longer a top broad-catch hotspot
- `src/Services/PurgeDeleteElementService.cs` was tightened in this pass:
  - element deletion now only downgrades expected delete failures instead of every exception shape
- `src/Services/RenderAppearanceSingleSyncService.cs` was tightened in this pass:
  - `UseRenderAppearanceForShading` assignment now logs only expected Revit/runtime setter failures
- `src/Services/FilterCopyService.cs` was tightened in this pass:
  - the filter-copy workflow now returns failures only for expected Revit/runtime transaction errors instead of swallowing every exception shape
- `src/Services/PurgeParameterService.cs` was partially tightened in this pass:
  - family-processing, parameter-delete, and family-reload failures now use expected Revit/runtime exception filters instead of blanket workflow catches
  - family document close failures now go through a guarded helper instead of a bare swallow
  - project-family collection, family usage analysis, parameter-delete execution, and family reload now live behind focused helpers/types, so the main family-edit workflow is less monolithic
  - the service still has structural weight, but the main family-edit workflow is materially less opaque
- `src/Services/LinePatternCompactionService.cs` was partially tightened in this pass:
  - canonical creation fallback, category pattern indexing, and category rewiring now filter to expected Revit/runtime exceptions
  - duplicate-group index setup now lives behind a dedicated helper/type, so compaction orchestration is less dense
  - per-group duplicate compaction and finalization now live behind focused helpers/types, so `CompactDuplicateGroups` acts more like a coordinator than an inline workflow
  - the service remains a collector-heavy hotspot, but those line-pattern mutation paths no longer swallow every exception shape
- `src/Services/FillPatternCompactionService.cs` was partially tightened in this pass:
  - canonical fill-pattern creation failures now filter to expected Revit/runtime exceptions instead of a blanket catch
  - duplicate-group building and reference-index setup now live behind dedicated helpers/types, so the main compaction workflow is easier to scan
  - group log-header creation, canonical-creation bookkeeping, and per-group reference rewiring now live behind focused helpers, and the delete pass tracks only groups instead of unused tuple state
  - original-pattern deletion now lives behind a focused helper, so `Compact` owns less cleanup orchestration directly
  - the service remains collector-heavy, but the canonical-create fallback path is less opaque during compaction
- `src/Services/DeepPurgeService.cs` was partially tightened in this pass:
  - family reload and outer family-processing failures now filter to expected Revit/runtime exceptions instead of blanket workflow catches
  - family document close now goes through a guarded helper instead of a direct close in `finally`
  - shared purge-pass execution and family reload handling now live behind focused helpers, so the deep-purge workflow is less monolithic
  - loaded-family processing now lives behind a summary-returning helper, so the public `Purge` method is less orchestration-heavy
- `src/Services/FamilyEditorService.cs` was partially tightened in this pass:
  - `ProcessFamily` now filters workflow failures to expected Revit/runtime exceptions instead of swallowing every exception shape
  - family-document close moved through a guarded helper, so close failures are logged without hiding unexpected errors
  - category bridge fallback and nested-family transplant now also filter to expected Revit/runtime exception shapes, and temp-file cleanup is explicit instead of a bare swallow
- `src/Services/FamilyParameterSetupService.cs` was partially tightened in this pass:
  - parameter creation, formula copy, and family-type creation now filter to expected Revit/runtime exceptions instead of broad workflow catches
  - the service still has a later value-copy bare catch, but its higher-level parameter setup path is now less opaque
- `src/Commands/CategoryChangerCommand.cs` was partially tightened in this pass:
  - category-change, transplant, and instance-swap logging paths now filter to expected Revit/runtime exceptions instead of swallowing every exception shape
  - level-placement fallback still remains, but unexpected failures in this command are less likely to disappear into routine error logs
- `src/Services/MaterialBitmapPropertyService.cs` was partially tightened in this pass:
  - connected-asset creation and asset-property mutation now filter to expected Revit/runtime exceptions instead of blanket warning catches
  - the service still logs expected asset-edit failures, but unexpected visual-asset defects are less likely to be silently downgraded
- `src/Commands/ConvertCadCommand.cs` was partially tightened in this pass:
  - active-document access is now explicit instead of assuming `ActiveUIDocument` always exists
  - top-level operation and placement handling now filter to expected Revit/runtime exceptions instead of swallowing every exception shape
- `src/Commands/UpdateContoursCommand.cs` was partially tightened in this pass:
  - per-toposolid contour update failures now filter to expected Revit/runtime exceptions instead of swallowing every exception shape in the batch loop
  - the command still runs best-effort per selected type, but unexpected contour-update defects are less likely to be downgraded into routine item log entries
- `src/Commands/ResetSlabsCommand.cs` was partially tightened in this pass:
  - duplicate/copy failures now filter to expected Revit/runtime exceptions instead of swallowing every exception shape in the processing loop
  - slab reset still continues best-effort across selected elements, but unexpected duplication defects are less likely to be downgraded into routine copy failures
- `src/Commands/PurgeCommand.cs` was partially tightened in this pass:
  - the custom WPF dialog fallback now filters to expected Revit/runtime exceptions instead of swallowing every exception shape before dropping to the native fallback
  - purge still has a native fallback path, but unexpected dialog defects are less likely to be silently downgraded into routine fallback behavior

### 2. Revit data access is concentrated in large collector-heavy services

- Approximate `FilteredElementCollector` call-site count: `90`
- This is not wrong by itself, but it increases the cost of verifying correctness, disposal, and query intent.
- Largest current hotspots:
  - `src/Services/LinePatternCompactionService.cs`
  - `src/Services/FillPatternCompactionService.cs`
  - `src/Services/BatchRenameExecutionService.cs`
  - `src/Services/PurgeContext.cs`
  - `src/Services/PurgeParameterService.cs`

### 3. Large service files still hold mixed responsibilities

- Biggest files currently in `src/`:
  - `LinePatternCompactionService.cs` (`28866` bytes)
  - `FillPatternCompactionService.cs` (`23613` bytes)
  - `BatchRenameExecutionService.cs` (`21162` bytes)
  - `PurgeContext.cs` (`19446` bytes)
  - `PurgeParameterService.cs` (`18572` bytes)
- Main risk: these classes combine orchestration, querying, transformation, and logging, which makes regression fixes slower.

### 4. Analyzer signal is currently clean on the add-in project

- The current `LECG.csproj` build is back to `0` warnings and `0` errors on this machine.
- The earlier `IDE0055` backlog was materially reduced by the repo formatting pass.
- The main remaining debt is structural and maintenance-oriented, not a live analyzer backlog in the add-in project.

### 5. Offline API extraction now works for both DLLs, but with two code paths

- `RevitAPI.dll` exported successfully.
- `RevitAPIUI.dll` now exports successfully through metadata fallback when runtime loading fails.
- Main remaining risk: the two assemblies are not extracted through exactly the same mechanism:
  - `RevitAPI.dll`: runtime reflection path
  - `RevitAPIUI.dll`: metadata fallback path
- That is acceptable for offline export, but record richness can differ slightly between assemblies.

## Fixes Landed In This Pass

- Replaced `NotImplementedException` `ConvertBack` paths in WPF converters with `Binding.DoNothing`
- Tightened null-safety in `FamilyInstanceData`, `CadTempDwgExtractionService`, and `MaterialAssignmentExecutionService`
- Tightened null-safety in app/bootstrap and WPF helper code (`App`, `LecgTreeView`, `MultiSelectCheckboxBehavior`, `SimpleDi`)
- Hardened public Revit/WPF entry points (`SafeFailureHandler`, `LecgDialogWindow.SetOptions`, `TestHarvestGeometryCommand.Execute`)
- Hardened `CategoryChangerCommand` instance swapping so point-based replacement is validated and level-based placement has a fallback path
- Narrowed `FamilyConversionService` replacement handling by extracting level resolution / placement helpers and reducing broad catch usage in instance deletion and apply paths
- Narrowed `FormulaAutoGroupingCommand` family document handling by localizing `EditFamily` / `LoadFamily` / `Close` failures and removing one broad inner catch
- Narrowed `FormulaAutoGroupingCommand` formula/lock/value preservation paths by replacing multiple bare catches with explicit helper-based expected-failure handling
- Narrowed `FamilyTemplatePathService`, `MaterialPbrService`, `PurgeReferenceScannerService`, and `SexySunSettingsService` by filtering broad catches to expected Revit/runtime or IO exception shapes
- Narrowed `OffsetService`, `TextStyleCompactionService`, `FamilyConversionExecutionService`, and `SimplifyPointsService` by filtering broad workflow catches to expected Revit/runtime exception shapes
- Narrowed CAD helper services (`CadCurveFlattenService`, `CadCurveTessellationService`, `CadLineMergeService`, and `CadPolylineExtractionService`) by replacing silent curve-construction catches with explicit expected-failure handling
- Narrowed UI/ribbon/state helpers (`SelectionCoordinator`, `RibbonService`, `RibbonFactory`, `LecgWindow`, `CategoryChangerViewModel`, `SearchReplaceViewModel`, and the `App` resource initialization path) by filtering broad catches to expected WPF/Revit/runtime exception shapes
- Narrowed `App` global exception-handler fallback logging and `CategoryChangerCommand` platform-limit detection so they no longer rely on bare catches or overly broad message-only matching
- Extracted `App` global exception registration helpers and `RevitCommand` execution/context helpers so the intentional top-level safety guards are easier to reason about without removing them
- Applied a broad `dotnet format whitespace LECG.csproj` pass to reduce the live `IDE0055` formatting backlog and restore clean project builds
- Reduced runtime assumption debt in `ConvertCadCommand`, `TestHarvestGeometryCommand`, `ConvertCadViewModel`, and `SearchReplaceViewModel` by removing several `null!` / first-item assumptions and validating selection or initialization state before use
- Reduced small collection/index assumption debt in `SlabService`, `BaseElementCollectionService`, `ImageColorExtractionService`, `AlignElementsDistributionMoveService`, `CadSplineFlattenService`, and `LinePatternCompactionService` by adding empty-collection/frame guards before first-item access
- Reduced residual null/empty-result assumption debt in `CadTempDwgExtractionService` and `LineStyleCompactionService` by replacing a `null!` extraction result and a bare survivor `First()` assumption with explicit guard clauses
- Narrowed `PurgeContext` material and element/category scan helpers by replacing broad catch blocks with expected Revit/runtime exception handling
- Narrowed `CompactionSharedHelper` parameter and override-index access paths by replacing broad catch blocks with expected Revit/runtime exception handling
- Narrowed `PurgeExtendedElementService` constraint deletion and applied-filter access paths by replacing bare catches with expected Revit/runtime exception handling
- Narrowed `AlignEdgesBoundaryCollectionService` sketch boundary collection by replacing a broad `Exception` catch with expected Revit/runtime exception handling
- Narrowed `PurgeLinePatternService` category/view/filter pattern scans by replacing broad `Exception` catches with expected Revit/runtime exception handling
- Added null guards to current `CA1062` entry points in purge/render/base-collection/family-conversion services so the live build is back to style-only warning noise on this machine
- Narrowed `FamilyConversionService` family-group error handling by filtering the remaining outer catch to expected conversion failures
- Narrowed `PurgeDeleteElementService` deletion helper by replacing a broad `Exception` catch with expected Revit/runtime exception handling
- Narrowed `RenderAppearanceSingleSyncService` render-appearance shading sync by replacing a broad `Exception` catch with expected Revit/runtime exception handling
- Narrowed `FilterCopyService` filter-copy workflow by replacing a broad `Exception` catch with expected Revit/runtime exception handling
- Narrowed `BatchRenameExecutionService` family parameter, object-style swap, and generic rename flows by replacing broad catches with expected Revit/runtime exception handling and guarding family document close
- Narrowed `PurgeParameterService` family purge workflow by filtering family-processing, delete, reload, and close failures to expected Revit/runtime exception shapes
- Narrowed `LinePatternCompactionService` canonical-create fallback and category line-pattern mutation paths by filtering broad catches to expected Revit/runtime exception shapes
- Narrowed `FillPatternCompactionService` canonical-create fallback by filtering its broad catch to expected Revit/runtime exception shapes
- Narrowed `DeepPurgeService` family purge reload/close/workflow paths by filtering broad catches to expected Revit/runtime exception shapes and guarding family close
- Added null guards for the current live `CA1062` entry points in `ConvertFamilyCommand`, `BatchRenameExecutionService`, `FamilyEditorService`, and `PurgeExecutionCoordinatorService`
- Narrowed `FamilyEditorService` reusable family-edit workflow by filtering `ProcessFamily` failures to expected Revit/runtime exception shapes and guarding family close
- Narrowed `FamilyEditorService` category-change fallback and nested-family transplant cleanup by filtering broad catches to expected Revit/runtime exception shapes and replacing bare temp-file cleanup
- Narrowed `FamilyParameterSetupService` parameter creation, formula copy, and family-type creation flows by filtering broad catches to expected Revit/runtime exception shapes
- Narrowed `CategoryChangerCommand` family-change, transplant, and instance-swap handling by filtering broad catches to expected Revit/runtime exception shapes
- Narrowed `MaterialBitmapPropertyService` connected-asset and bitmap-property mutation paths by filtering broad catches to expected Revit/runtime exception shapes
- Narrowed `ConvertCadCommand` operation and placement handling by filtering broad catches to expected Revit/runtime exception shapes and making active-document access explicit
- Narrowed `UpdateContoursCommand` per-toposolid update loop by filtering its broad catch to expected Revit/runtime exception shapes
- Narrowed `ResetSlabsCommand` duplicate-element handling by filtering its broad catch to expected Revit/runtime exception shapes
- Narrowed `PurgeCommand` custom-dialog fallback by filtering its broad catch to expected Revit/runtime exception shapes
- Excluded `tools/**/*.cs` from `LECG.csproj` so standalone tooling does not get compiled into the add-in by default
- Added an offline Revit API extractor under `tools/RevitApiExtractor/`
- Added public-surface, DB/UI-focused, and namespace-index artifacts for LLM retrieval
- Cleaned remaining mojibake log prefixes in `MaterialCreationService` and `MaterialTypeAssignmentService`
- Cleaned mojibake comment text in `SearchReplaceView.xaml`
- Extracted setup/index orchestration out of `FillPatternCompactionService`, `LinePatternCompactionService`, and `PurgeContext` so those large services are less monolithic
- Extracted fill-pattern group log-header/success bookkeeping/rewire orchestration and line-pattern per-group compaction/finalization into focused helpers so both compaction services are more coordinator-driven
- Extracted per-group duplicate compaction and finalization out of `LinePatternCompactionService` so the line-pattern compaction path is more helper-driven
- Extracted family collection, usage analysis, delete execution, and reload handling out of `PurgeParameterService` so the family-purge workflow is less monolithic
- Extracted repeated override indexing/rewiring plumbing out of `CompactionSharedHelper` so the shared compaction code is less monolithic
- Extracted shared purge-pass execution and family reload handling out of `DeepPurgeService` so the deep-purge workflow is less monolithic
- Extracted loaded-family purge orchestration out of `DeepPurgeService` and original-pattern deletion out of `FillPatternCompactionService`
- Extracted per-family-group orchestration plus execute/finalize, family instance capture/delete/find-symbol/replace handling out of `FamilyConversionService` so the batch conversion workflow is less monolithic

## Recommended Next Pass

1. Carve query and mutation helpers out of the remaining largest collector-heavy services so Revit traversal is easier to review and test.
2. Continue slimming the largest remaining orchestration-heavy services, especially the remaining dense family-conversion and collector-heavy purge flows.
3. Normalize analyzer policy:
   - keep `IDE0055` out of correctness triage
   - drive down new nullability and `CA1062` findings if they reappear in production services
4. Normalize extractor parity so runtime and metadata-fallback exports stay as close as possible in shape and fidelity.
