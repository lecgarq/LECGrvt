# Codebase Map

Generated at: d393c0f34e5a9a9f32972aa3ff828b231c217b7d
Refreshed: 2026-08-18

Edges, not prose. Query with `/lecg-map`; patch rows when you change the code they describe.

## Commands

All in `src/Commands/`. No `[CommandAvailability]` attributes — availability is a class-name string per ribbon button (see Ribbon). Bases: `src/Core/RevitCommand.cs`, `src/Core/ExternalEventCommand.cs`.

| Command | Base | Services resolved | View | Tests |
|---|---|---|---|---|
| HomeCommand | RevitCommand | dispatches to ~24 commands via `ServiceLocator.CreateWith<T>()` | HomeView, AlignDashboardView | — |
| Align{Left,Center,Right,Top,Middle,Bottom}, Distribute{H,V}Command | AlignCommandBase : RevitCommand | AlignElementsService | AlignElementsView | — |
| AlignEdgesCommand | RevitCommand | AlignEdgesService | AlignEdgesView | — |
| AssignMaterialCommand | RevitCommand | IMaterialService | AssignMaterialView | — |
| CategoryChangerCommand | ExternalEventCommand\<CategoryChangerEventHandler> | FamilyEditorService, ITransactionService | CategoryChangerView (modeless) | — |
| ChangeLevelCommand | RevitCommand | ChangeLevelService | ChangeLevelView | — |
| CleanSchemasCommand | RevitCommand | SchemaCleanerService, ITransactionService | log window | — |
| CompactingStylesCommand | RevitCommand | 4× *CompactionService, ITransactionService | log window | — |
| ConvertCadCommand | ExternalEventCommand\<ConvertCadEventHandler> | ICadConversionService, ITransactionService | ConvertCadView (modeless) | — |
| ConvertFamilyCommand | RevitCommand | FamilyConversionService, DialogWhitelist | log window | — |
| ConvertFloorToToposolidCommand | RevitCommand | ConversionService | ConvertFloorToToposolidView | — |
| ConvertSharedCommand | RevitCommand | ITransactionService | LecgDialog | — |
| ConvertToposolidToFloorCommand | RevitCommand | ConversionService | ConvertToposolidToFloorView | — |
| DivideToposolidCommand | RevitCommand | DivideToposolidService | DivideToposolidView | — |
| FilterCopyCommand | RevitCommand | (VM uses IFilterCopyService) | FilterCopyView | — |
| FixPointsCommand | RevitCommand | FixPointsService | FixPointsView | — |
| FormulaAutoGroupingCommand | RevitCommand | IFamilyLoadOptionsFactory, FamilySaveLoadService, FormulaNameUpdater (Core) | log window | FormulaAutoGroupingCommandTests |
| OffsetElevationsCommand | RevitCommand | OffsetService, ITransactionService | OffsetElevationsView | — |
| PbrMaterialCreatorCommand | RevitCommand | IMaterialService, IMaterialTextureLookupService (VM `new`'d, not DI-resolved) | PbrMaterialCreatorView | PbrMaterialCreatorViewModelTests, MaterialPageViewModelTests |
| PurgeCommand | RevitCommand | PurgeService, DeepPurgeService, DialogWhitelist | PurgeView | PurgeSequenceTests (Core logic only) |
| RenderAppearanceMatchCommand | RevitCommand | IMaterialService | RenderAppearanceView | — |
| ResetSlabsCommand | RevitCommand | SlabService, ITransactionService | ResetSlabsView | — |
| SearchReplaceCommand | RevitCommand | ISearchReplaceService | SearchReplaceView | SearchReplace* tests (service + VM level) |
| SexyRevitCommand | RevitCommand | SexyRevitService | SexyRevitView | SexyRevitServiceTests |
| SharedToFamilyParameterCommand | RevitCommand | ISharedToFamilyParameterService | LecgDialog | — |
| SimplifyPointsCommand | RevitCommand | SimplifyPointsService | SimplifyPointsView | — |
| SplitBoundariesCommand | RevitCommand | SplitBoundariesService | SplitBoundariesView | — |
| TypeToLinkedModelsCommand | RevitCommand | ILinkedModelExportService | TypeToLinkedModelsView | — |
| UpdateContoursCommand | RevitCommand | ToposolidService, ITransactionService | UpdateContoursView | — |
| WarningsCommand | RevitCommand | (VM uses WarningsService) | WarningsView (modal) | WarningsService/ViewModel/GroupingPolicy tests |

Both `IExternalEventHandler` types are nested in their command files: `CategoryChangerEventHandler` (CategoryChangerCommand.cs:56), `ConvertCadEventHandler` (ConvertCadCommand.cs:62).

## Services

All registered in `src/Core/Bootstrapper.cs` → `ConfigureServices()` (single method, everything **singleton**). "Writes doc" = injects ITransactionService — the usual write path. One deliberate exception: `WarningsService.Isolate` uses a raw `Transaction`, because injecting `ITransactionService` would load the Revit type graph at construction and make the service unconstructible in tests (see `src/Services/Health/WarningsService.cs:83-88`).

### Infrastructure
| Service | Interface | Used by | Writes doc |
|---|---|---|---|
| TransactionService | ITransactionService | 38 files | IS the write mechanism |
| AppMemoryCache | IAppMemoryCache | GeometryBoundaryService | no |
| FilterCopyService | IFilterCopyService | FilterCopyViewModel | yes |
| LinkedModelExportService | ILinkedModelExportService | TypeToLinkedModelsCommand | yes |
| SettingsManager | — (not registered) | ViewModels/Views (window placement) | no |
| Logger / SerilogBootstrapper | ILogger | 45 files | no |

### Topography
| Service | Used by | Writes doc |
|---|---|---|
| SlabService | ResetSlabs cmd; SplitBoundaries/DivideToposolid/FixPoints/ConversionService; AlignEdgesToposolidProcessingService | via callers |
| OffsetService | OffsetElevationsCommand | via caller |
| ToposolidService | UpdateContoursCommand | via caller |
| ChangeLevelService | ChangeLevelCommand | yes |
| ConversionService | ConvertFloorToToposolid / ConvertToposolidToFloor cmds | yes |
| GeometryBoundaryService | Conversion/SplitBoundaries/DivideToposolidService, AlignEdgesBoundaryCollectionService | no |
| SimplifyPointsService, FixPointsService, SplitBoundariesService, DivideToposolidService | their commands | yes |

### Alignment
| Service | Used by | Writes doc |
|---|---|---|
| AlignEdgesService (orchestrator) | AlignEdgesCommand | yes |
| AlignEdgesToposolidProcessingService | AlignEdgesService | no (caller txn) |
| AlignEdgesBoundaryCollectionService | AlignEdgesToposolidProcessingService | no |
| AlignEdgesBoundaryPointService | AlignEdgesBoundaryCollectionService | no |
| AlignEdgesHitPointProjectionService, AlignEdgesCurveDivisionService | AlignEdgesBoundaryPointService | no |
| AlignEdgesIntersectorService | AlignEdgesService | no |
| AlignEdgesPointInsertionService, AlignEdgesVertexAlignmentService | AlignEdgesToposolidProcessingService | no |
| ReferenceRaycastService | AlignEdgesHitPointProjection/VertexAlignmentService | no |
| AlignElementsService | AlignCommandBase (8 commands) | yes |

### CadConversion — 35 micro-services, each with a 1:1 `ICad*` interface, all singleton
Top orchestrator: **CadConversionService : ICadConversionService** ← ConvertCadCommand. Chain (consumer ← consumed):
- CadConversionService ← CadImportDataPreparation, CadImportFamilyCreation, CadTempDwgExtraction, CadDwgFamilyCreation, CadDataValidation, CadImportInstanceCenter
- CadImportDataPreparation ← CadGeometryExtraction, CadGeometryOptimization, CadDataValidation
- CadGeometryExtraction ← CadSolidHatchExtraction, CadPolylineExtraction
- CadGeometryOptimization ← CadCurveFlatten, CadLineMerge
- CadCurveFlatten ← CadCurveTessellation, CadPointFlatten, CadSplineFlatten; CadSplineFlatten ← CadCurveTessellation, CadPointFlatten, CadDoubleArrayConversion
- CadDwgFamilyCreation / CadImportFamilyCreation ← CadFamilyBuild, CadFamilyLoadPlacement
- CadFamilyBuild ← CadDataDraw, CadFamilySave, ITransactionService (writes)
- CadDataDraw ← CadRenderContext, CadCurveRender, CadHatchRender
- CadRenderContext ← CadLineStyle, CadDrawingView; CadCurveRender ← CadCurveFlatten
- CadHatchRender ← CadFilledRegionType, CadHatchProgress, CadHatchLoopPreparation (← CadCurveFlatten)
- CadFamilyLoadPlacement ← CadFamilyLoadResolve (← IFamilyLoadOptionsFactory, CadFamilySymbol), CadTempFileCleanup, CadSourceCleanup, CadFamilyInstancePlacement (← CadPlacementView), ITransactionService (writes)
- CadTempDwgExtraction ← CadGeometryExtraction, ITransactionService (writes)

### FamilyConversion
| Service | Used by | Writes doc |
|---|---|---|
| FamilyConversionService (orchestrator) | ConvertFamilyCommand | yes |
| FamilyConversionExecutionService | FamilyConversionService | via children |
| FamilyGeometryCopyService | FamilyConversionExecutionService | yes |
| FamilySaveLoadService | FamilyConversionExecutionService, FormulaAutoGroupingCommand | via children |
| FamilySaveService / FamilyProjectLoadService | FamilySaveLoadService | load: yes |
| FamilyTemplatePath / SourceDocument / TargetDocument / ParameterSetup / GeometryCollection / ConversionLogging / ConversionFinalize / TempFileCleanupService | FamilyConversionService chain | no |
| FamilyLoadOptionsFactory (IFamilyLoadOptionsFactory) | 7 consumers: BatchRename, FamilyEditor, FamilyProjectLoad, PurgeParameter, DeepPurge, CadFamilyLoadResolve, FormulaAutoGroupingCommand | no |
| FamilyEditorService | CategoryChangerCommand | yes |
| SharedToFamilyParameterService (ISharedToFamilyParameterService) | SharedToFamilyParameterCommand | yes |

### PurgeAndCompaction
| Service | Used by | Writes doc |
|---|---|---|
| PurgeService (orchestrator), DeepPurgeService | PurgeCommand | yes |
| PurgePassExecutionService | PurgeService | via children |
| PurgePassSequenceService | PurgeService, DeepPurgeService | no (Core PurgeSequence) |
| Purge{Material,LineStyle,LinePattern,FillPattern,Level,ExtendedElement}Service | PurgeService, PurgePassExecutionService | via PurgeDeleteElementService |
| PurgeDeleteElementService | the 6 Purge*Services | deletes inside caller txn |
| PurgeParameterService | PurgeService | yes |
| {LinePattern,FillPattern,TextStyle,LineStyle}CompactionService | CompactingStylesCommand | inside command txn |

### Materials
| Service | Used by | Writes doc |
|---|---|---|
| MaterialService (facade, IMaterialService) | AssignMaterial / PbrMaterialCreator / RenderAppearanceMatch cmds | via children |
| MaterialPbrService (IMaterialPbrService) | MaterialService | yes |
| MaterialAppearanceAssetService (IMaterialAppearanceAssetService) | MaterialPbrService | yes |
| MaterialBitmapPropertyService | MaterialAppearanceAssetService | no |
| MaterialBumpMapNormalizer (not registered) | MaterialAppearanceAssetService area | no |
| MaterialCreationService (IMaterialCreationService) | MaterialService, MaterialPbrService, MaterialTypeAssignmentProcessService | no (inside txn) |
| MaterialColorSequenceService (IMaterialColorSequenceService) | same three | no |
| MaterialTextureLookupService (IMaterialTextureLookupService) | PbrMaterialCreatorCommand/VM, MaterialPbrService | no |
| ImageColorExtractionService (IImageColorExtractionService) | MaterialPbrService, RenderMaterialSyncExecutionService | no |
| MaterialAssignmentExecutionService | MaterialService | yes |
| Material{ElementGrouping,AssignmentProgress,ElementTypeResolver,TypeAssignmentProcess,TypeAssignment,TypeEligibility}Service | MaterialAssignmentExecutionService chain | no |

### RenderAppearance
| Service | Used by | Writes doc |
|---|---|---|
| RenderAppearanceService | MaterialService | via children |
| RenderAppearanceSingleSyncService / BatchSyncService | RenderAppearanceService | batch: yes |
| RenderMaterialSyncExecutionService | RenderAppearanceBatchSyncService | no |
| RenderSolidFillPatternService (IRenderSolidFillPatternService) | Single/BatchSync, MaterialPbrService | no |
| RenderMaterialGraphicsApplyService (IRenderMaterialGraphicsApplyService) | SingleSync, SyncExecution, MaterialPbrService | no |

### Renaming
| Service | Used by | Writes doc |
|---|---|---|
| SearchReplaceService (ISearchReplaceService, orchestrator) | SearchReplaceCommand, SearchReplaceViewModel | via children |
| BulkObservableCollection&lt;T&gt; (src/ViewModels/Components/) | SearchReplaceViewModel.PreviewItems | no — ObservableCollection subclass, one Reset per bulk replace |
| SearchReplacePreviewService | SearchReplaceService | no |
| BatchRenameExecutionService | SearchReplaceService | yes |
| BaseElementCollectionService | SearchReplaceService | no |
| RenameRulePipelineService | SearchReplacePreviewService | no |
| FormulaUpdateService | BatchRenameExecutionService | no |

### Graphics / Schemas / misc
| Service | Used by | Writes doc |
|---|---|---|
| SexyRevitService | SexyRevitCommand | yes |
| RevitViewGraphicsFacade (IViewGraphicsFacade, `new`'d per view, not registered) | SexyRevitService | no |
| SchemaCleanerService | CleanSchemasCommand | inside command txn |
| ElementLabelService (static, not registered) | ViewModels (row labels) | no |
| ValidationService (IValidationService) | ViewModels via ValidationServiceExtensions | no |
| SelectionCoordinator (ISelectionCoordinator) | SelectionViewModel, several VMs | no |
| WarningsService (src/Services/Health/) | WarningsViewModel | no persistent write — but `Isolate` needs a raw `Transaction` (Revit treats temporary isolate as a modification); R6 restated |

## ViewModels ↔ Views

All VMs `AddTransient` in `Bootstrapper.ConfigureViewModels`, all Views `AddTransient` in `ConfigureViews`. Pattern: command resolves VM, then `ServiceLocator.CreateWith<View>(vm)`. Pairings are Name↔NameView for all 24 registered VMs. Exceptions worth knowing:

- FilterCopyViewModel — built via `CreateWith(doc)`, not resolved
- PbrMaterialCreatorViewModel — registration bypassed; command `new`s it
- LogViewModel/LogView — used by the RevitCommand base log window
- HomeView, AlignDashboardView — no VM
- SelectionViewModel / ElementRowViewModel + SelectionControl (Components) — not registered, composed by parent VMs

## Ribbon

`RibbonService.InitializeRibbon` (src/Core/Ribbon/RibbonService.cs): tab + 7 panels. Availability: P = ProjectDocumentAvailability, F = FamilyDocumentAvailability, — = always.

| Panel | Buttons |
|---|---|
| Home | Home (P) |
| Project Health | Clean Schemas (P), Compacting Styles (P), Purge (P), Formula Grouping (P), Warnings (P) |
| Standards | Convert CAD (P), Search/Replace (P), Convert Family (—), Convert Shared (F), Category Changer (P), Shared→Family Param (P), Filter Copy (P) |
| Toposolids | Assign Material, Offset, Reset Slabs, Simplify Points, Align Edges, Update Contours, Change Level, Floor→Toposolid, Toposolid→Floor, Fix Points, Split Boundaries, Divide Toposolid (all P) |
| Align | pulldown "Align Elements" → 8 align/distribute commands (—) |
| Model Organization | Type to Linked (P) |
| Visualization | Render Match (P), Material Creator (P), Sexy Revit (P) |

## LECG.Core

Pure (no Revit API) policy project. Namespace `LECG.Core.*` merges with src's.

| Folder | Types | Consumed by (src) |
|---|---|---|
| Naming | FamilyNamePolicy, DetailFamilyNamePolicy, FamilySelectionPolicy, LinePatternNamingPolicy | ConvertCadViewModel, FamilyInstanceFilter, LinePatternCompactionService; **FamilyNamePolicy: tests only, no src consumer** |
| Purge | PurgeOptions, PurgeResult, PurgeSequence | PurgeCommand, PurgeService, PurgePassExecution/SequenceService |
| Rename | FormulaNameUpdater, RenameRuleEngine | FormulaAutoGroupingCommand, BatchRenameExecutionService, FormulaUpdateService, RenameRules, SearchReplacePreviewService |
| Filtering | SearchTermPolicy | FilterCopyViewModel |
| Graphics | SexyRevitGraphicsPolicy | SexyRevitService |
| Warnings | WarningItem, WarningGroup, WarningGroupingPolicy | WarningsService, WarningsViewModel |
| (root) | Result / Result\<T> | shared return type across services |

## Hot spots

| File | Inbound (approx) | Why expensive |
|---|---|---|
| src/Core/ServiceLocator.cs | 128 refs / 38 files | every command resolves through it |
| src/Services/Infrastructure/Logging/Logger.cs (ILogger) | 106 refs / 45 files | injected everywhere incl. CadConversion micro-services |
| src/Services/Infrastructure/ITransactionService.cs | 70 refs / 38 files | sole document-write gateway |
| src/Core/Bootstrapper.cs | ~145 registrations | single wiring point; missing registration throws at runtime |
| src/ViewModels/BaseViewModel.cs | 23 subclasses | property-change plumbing for all VMs |
| src/Views/Base/LecgWindow.cs + LecgDialog | ~64 files | every View derives from it; theme scoping guard lives here |
| src/Services/Topography/SlabService.cs | 6 consumers | shared by six toposolid flows |
| src/Services/FamilyConversion/FamilyLoadOptionsFactory.cs | 7 consumers | overwriteParameterValues fans out to purge, rename, CAD, category-change |
| src/Core/Ribbon/RibbonService.cs | 30 buttons | command class names as **strings** — rename a command class and its button silently dies |

## Untested

Covered: Core policies (all 6 folders), Result, Renaming pipeline, PurgeSequence, SexyRevitService+policy, AppMemoryCache, DialogWhitelist, validator registration, ValidationService, Purge/Window validators, Logger severity, ElementLabelService, MaterialBumpMapNormalizer, MaterialTextureLookupService, FormulaAutoGroupingCommand, SearchReplace/PbrMaterialCreator/MaterialPage VMs, theme scoping.

No covering test:

- 29 of 30 command classes (incl. HomeCommand's string-keyed dispatch)
- Entire CadConversion folder (~35 services) + ConvertCadViewModel (open CS0618 ILogger re-plumb)
- Entire FamilyConversion folder incl. FamilyLoadOptionsFactory
- Entire Topography folder
- Entire Alignment folder (AlignEdges* chain, AlignElementsService, ReferenceRaycastService)
- PurgeAndCompaction runtime services (only Core PurgeSequence logic tested)
- Materials runtime services (only BumpMapNormalizer + TextureLookup tested)
- Entire RenderAppearance folder
- Infrastructure: TransactionService, SettingsManager, FilterCopyService, LinkedModelExportService
- SchemaCleanerService, RibbonService/RibbonFactory (button→command string mapping unverified)
- 20 of 24 registered ViewModels
- Core plumbing: RevitCommand/ExternalEventCommand, SelectionCoordinator, SafeFailureHandler, WarningSwallower, RevitIdlingRunner
