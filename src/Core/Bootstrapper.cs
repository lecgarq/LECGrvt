using Microsoft.Extensions.DependencyInjection;
using LECG.ViewModels;
using LECG.Services.Interfaces;
using LECG.Services;
using LECG.Core.Ribbon;
using LECG.Models;
using LECG.Services.Logging;
using LECG.Validation;
using LECG.Validation.Validators;
using MsLoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;

namespace LECG.Core
{
    public static class Bootstrapper
    {
        private static IServiceProvider? _provider;

        public static void Initialize()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            MsLoggerFactory loggerFactory = SerilogBootstrapper.CreateLoggerFactory(out string? loggingInitializationError);

            ConfigureServices(services, loggerFactory);
            ConfigureViewModels(services);
            ConfigureViews(services);

            _provider = services.BuildServiceProvider();
            ServiceLocator.Initialize(_provider);

            if (!string.IsNullOrWhiteSpace(loggingInitializationError))
            {
                Logger.Instance.LogWarning($"Structured logging fallback enabled: {loggingInitializationError}");
            }
        }

        public static void Shutdown()
        {
            if (_provider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _provider = null;
        }

        private static void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services, MsLoggerFactory loggerFactory)
        {
            // Core
            Logger.Instance.ConfigureStructuredLogger(loggerFactory);
            services.AddSingleton<MsLoggerFactory>(_ => loggerFactory);
            services.AddSingleton<IRibbonService, RibbonService>();
            services.AddSingleton<LECG.Services.Logging.ILogger>(_ => Logger.Instance);
            services.AddSingleton<IValidationService, ValidationService>();
            services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions()));
            services.AddSingleton<IAppMemoryCache, AppMemoryCache>();
            services.AddSingleton<ISelectionCoordinator, SelectionCoordinator>();
            services.AddSingleton<ITransactionService, TransactionService>();

            // Keep most registrations manual. Closed-generic validator scanning is safe
            // because implementations map 1:1 to their interfaces and lifetimes stay singleton.
            services.AddValidatorsFromAssemblyContaining<OffsetElevationsViewModelValidator>();

            // Domain Services
            services.AddSingleton<ISlabService, SlabService>();
            services.AddSingleton<IOffsetService, OffsetService>();
            services.AddSingleton<IRenderSolidFillPatternService, RenderSolidFillPatternService>();
            services.AddSingleton<IRenderMaterialSyncCheckService, RenderMaterialSyncCheckService>();
            services.AddSingleton<IRenderAppearanceRefreshService, RenderAppearanceRefreshService>();
            services.AddSingleton<IRenderBatchProgressService, RenderBatchProgressService>();
            services.AddSingleton<IRenderMaterialGraphicsApplyService, RenderMaterialGraphicsApplyService>();
            services.AddSingleton<IRenderMaterialSyncExecutionService, RenderMaterialSyncExecutionService>();
            services.AddSingleton<IRenderAppearanceSingleSyncService, RenderAppearanceSingleSyncService>();
            services.AddSingleton<IRenderAppearanceBatchSyncService, RenderAppearanceBatchSyncService>();
            services.AddSingleton<IRenderAppearanceService, RenderAppearanceService>();
            services.AddSingleton<IMaterialTypeAssignmentService, MaterialTypeAssignmentService>();
            services.AddSingleton<IMaterialCreationService, MaterialCreationService>();
            services.AddSingleton<IMaterialColorSequenceService, MaterialColorSequenceService>();
            services.AddSingleton<IMaterialTextureLookupService, MaterialTextureLookupService>();
            services.AddSingleton<IMaterialBitmapPropertyService, MaterialBitmapPropertyService>();
            services.AddSingleton<IImageColorExtractionService, ImageColorExtractionService>();
            services.AddSingleton<IMaterialAppearanceAssetService, MaterialAppearanceAssetService>();
            services.AddSingleton<IMaterialPbrService, MaterialPbrService>();
            services.AddSingleton<IMaterialElementGroupingService, MaterialElementGroupingService>();
            services.AddSingleton<IMaterialTypeEligibilityService, MaterialTypeEligibilityService>();
            services.AddSingleton<IMaterialAssignmentProgressService, MaterialAssignmentProgressService>();
            services.AddSingleton<IMaterialElementTypeResolverService, MaterialElementTypeResolverService>();
            services.AddSingleton<IMaterialTypeAssignmentProcessService, MaterialTypeAssignmentProcessService>();
            services.AddSingleton<IMaterialAssignmentExecutionService, MaterialAssignmentExecutionService>();
            services.AddSingleton<IMaterialService, MaterialService>();
            services.AddSingleton<IPurgeReferenceScannerService, PurgeReferenceScannerService>();
            services.AddSingleton<IPurgeReferencedLevelService, PurgeReferencedLevelService>();
            services.AddSingleton<IPurgeDeleteElementService, PurgeDeleteElementService>();
            services.AddSingleton<ILinePatternCompactionService, LinePatternCompactionService>();
            services.AddSingleton<IFillPatternCompactionService, FillPatternCompactionService>();
            services.AddSingleton<ITextStyleCompactionService, TextStyleCompactionService>();
            services.AddSingleton<ILineStyleCompactionService, LineStyleCompactionService>();
            services.AddSingleton<IPurgeMaterialUsageCollectorService, PurgeMaterialUsageCollectorService>();
            services.AddSingleton<IPurgeMaterialService, PurgeMaterialService>();
            services.AddSingleton<IPurgeLineStyleService, PurgeLineStyleService>();
            services.AddSingleton<IPurgeLinePatternService, PurgeLinePatternService>();
            services.AddSingleton<IPurgeFillPatternService, PurgeFillPatternService>();
            services.AddSingleton<IPurgeLevelService, PurgeLevelService>();
            services.AddSingleton<IPurgeExtendedElementService, PurgeExtendedElementService>();
            services.AddSingleton<IPurgeParameterService, PurgeParameterService>();
            services.AddSingleton<IPurgeSummaryService, PurgeSummaryService>();
            services.AddSingleton<IPurgePassMessagingService, PurgePassMessagingService>();
            services.AddSingleton<IPurgePassSequenceService, PurgePassSequenceService>();
            services.AddSingleton<IPurgePassExecutionService, PurgePassExecutionService>();
            services.AddSingleton<IPurgeExecutionCoordinatorService, PurgeExecutionCoordinatorService>();
            services.AddSingleton<INativePurgeDocumentService, NativePurgeDocumentService>();
            services.AddSingleton<IDeepPurgeService, DeepPurgeService>();
            services.AddSingleton<IPurgeService, PurgeService>();
            services.AddSingleton<ISchemaVendorFilterService, SchemaVendorFilterService>();
            services.AddSingleton<ISchemaElementScanService, SchemaElementScanService>();
            services.AddSingleton<ISchemaDataStorageScanService, SchemaDataStorageScanService>();
            services.AddSingleton<ISchemaDataStorageDeleteService, SchemaDataStorageDeleteService>();
            services.AddSingleton<ISchemaEraseService, SchemaEraseService>();
            services.AddSingleton<ISchemaCleanerService, SchemaCleanerService>();
            services.AddSingleton<ISexyGraphicsApplyService, SexyGraphicsApplyService>();
            services.AddSingleton<ISexySunSettingsService, SexySunSettingsService>();
            services.AddSingleton<ISexyCategoryVisibilityService, SexyCategoryVisibilityService>();
            services.AddSingleton<ISexySectionBoxVisibilityService, SexySectionBoxVisibilityService>();
            services.AddSingleton<ISexyRevitService, SexyRevitService>();
            services.AddSingleton<IFilterCopyService, FilterCopyService>();
            services.AddSingleton<IBaseElementCollectionService, BaseElementCollectionService>();
            services.AddSingleton<IRenameRulePipelineService, RenameRulePipelineService>();
            services.AddSingleton<ISearchReplacePreviewService, SearchReplacePreviewService>();
            services.AddSingleton<IBatchRenameExecutionService, BatchRenameExecutionService>();
            services.AddSingleton<ISearchReplaceService, SearchReplaceService>();
            services.AddSingleton<IFamilyTemplatePathService, FamilyTemplatePathService>();
            services.AddSingleton<IFamilyGeometryCollectionService, FamilyGeometryCollectionService>();
            services.AddSingleton<IFamilyTempFileCleanupService, FamilyTempFileCleanupService>();
            services.AddSingleton<IFamilyConversionFinalizeService, FamilyConversionFinalizeService>();
            services.AddSingleton<IFamilyLoadOptionsFactory, FamilyLoadOptionsFactory>();
            services.AddSingleton<IFamilyParameterSetupService, FamilyParameterSetupService>();
            services.AddSingleton<IFamilyConversionNamingService, FamilyConversionNamingService>();
            services.AddSingleton<IFamilyConversionLoggingService, FamilyConversionLoggingService>();
            services.AddSingleton<IFamilySourceDocumentService, FamilySourceDocumentService>();
            services.AddSingleton<IFamilyConversionExecutionService, FamilyConversionExecutionService>();
            services.AddSingleton<IFamilyGeometryCopyService, FamilyGeometryCopyService>();
            services.AddSingleton<IFamilySaveService, FamilySaveService>();
            services.AddSingleton<IFamilySaveLoadService, FamilySaveLoadService>();
            services.AddSingleton<IFamilyTargetDocumentService, FamilyTargetDocumentService>();
            services.AddSingleton<IFamilyProjectLoadService, FamilyProjectLoadService>();
            services.AddSingleton<IFamilyConversionService, FamilyConversionService>();
            services.AddSingleton<IReferenceRaycastService, ReferenceRaycastService>();
            services.AddSingleton<IAlignEdgesCurveDivisionService, AlignEdgesCurveDivisionService>();
            services.AddSingleton<IAlignEdgesCurveHitService, AlignEdgesCurveHitService>();
            services.AddSingleton<IAlignEdgesHitPointProjectionService, AlignEdgesHitPointProjectionService>();
            services.AddSingleton<IAlignEdgesIntersectorService, AlignEdgesIntersectorService>();
            services.AddSingleton<IAlignEdgesBoundaryPointService, AlignEdgesBoundaryPointService>();
            services.AddSingleton<IAlignEdgesBoundaryCollectionService, AlignEdgesBoundaryCollectionService>();
            services.AddSingleton<IAlignEdgesPointInsertionService, AlignEdgesPointInsertionService>();
            services.AddSingleton<IToposolidBaseElevationService, ToposolidBaseElevationService>();
            services.AddSingleton<IAlignEdgesVertexAlignmentService, AlignEdgesVertexAlignmentService>();
            services.AddSingleton<IAlignEdgesToposolidProcessingService, AlignEdgesToposolidProcessingService>();
            services.AddSingleton<IAlignEdgesService, AlignEdgesService>();
            services.AddSingleton<IToposolidService, ToposolidService>();
            services.AddSingleton<IChangeLevelElementUpdateService, ChangeLevelElementUpdateService>();
            services.AddSingleton<IChangeLevelService, ChangeLevelService>();
            services.AddSingleton<ISimplifyPointsService, SimplifyPointsService>();
            services.AddSingleton<IFixPointsService, FixPointsService>();
            services.AddSingleton<IAlignElementsTranslationService, AlignElementsTranslationService>();
            services.AddSingleton<IAlignElementsDistributionItemService, AlignElementsDistributionItemService>();
            services.AddSingleton<IAlignElementsDistributionMoveService, AlignElementsDistributionMoveService>();
            services.AddSingleton<IAlignElementsService, AlignElementsService>();
            services.AddSingleton<ICadPlacementViewService, CadPlacementViewService>();
            services.AddSingleton<ICadFamilySymbolService, CadFamilySymbolService>();
            services.AddSingleton<ICadFamilyLoadResolveService, CadFamilyLoadResolveService>();
            services.AddSingleton<ICadFamilyInstancePlacementService, CadFamilyInstancePlacementService>();
            services.AddSingleton<ICadLineStyleService, CadLineStyleService>();
            services.AddSingleton<ICadLineMergeService, CadLineMergeService>();
            services.AddSingleton<ICadPointFlattenService, CadPointFlattenService>();
            services.AddSingleton<ICadDoubleArrayConversionService, CadDoubleArrayConversionService>();
            services.AddSingleton<ICadCurveTessellationService, CadCurveTessellationService>();
            services.AddSingleton<ICadSplineFlattenService, CadSplineFlattenService>();
            services.AddSingleton<ICadCurveFlattenService, CadCurveFlattenService>();
            services.AddSingleton<ICadFilledRegionTypeService, CadFilledRegionTypeService>();
            services.AddSingleton<ICadFamilyLoadPlacementService, CadFamilyLoadPlacementService>();
            services.AddSingleton<ICadTempFileCleanupService, CadTempFileCleanupService>();
            services.AddSingleton<ICadSolidHatchExtractionService, CadSolidHatchExtractionService>();
            services.AddSingleton<ICadPolylineExtractionService, CadPolylineExtractionService>();
            services.AddSingleton<ICadGeometryExtractionService, CadGeometryExtractionService>();
            services.AddSingleton<ICadGeometryOptimizationService, CadGeometryOptimizationService>();
            services.AddSingleton<ICadImportDataPreparationService, CadImportDataPreparationService>();
            services.AddSingleton<ICadImportFamilyCreationService, CadImportFamilyCreationService>();
            services.AddSingleton<ICadDataValidationService, CadDataValidationService>();
            services.AddSingleton<ICadImportInstanceCenterService, CadImportInstanceCenterService>();
            services.AddSingleton<ICadDrawingViewService, CadDrawingViewService>();
            services.AddSingleton<ICadSourceCleanupService, CadSourceCleanupService>();
            services.AddSingleton<ICadRenderContextService, CadRenderContextService>();
            services.AddSingleton<ICadCurveRenderService, CadCurveRenderService>();
            services.AddSingleton<ICadHatchProgressService, CadHatchProgressService>();
            services.AddSingleton<ICadHatchLoopPreparationService, CadHatchLoopPreparationService>();
            services.AddSingleton<ICadHatchRenderService, CadHatchRenderService>();
            services.AddSingleton<ICadDataDrawService, CadDataDrawService>();
            services.AddSingleton<ICadFamilySaveService, CadFamilySaveService>();
            services.AddSingleton<ICadTempDwgExtractionService, CadTempDwgExtractionService>();
            services.AddSingleton<ICadFamilyBuildService, CadFamilyBuildService>();
            services.AddSingleton<ICadDwgFamilyCreationService, CadDwgFamilyCreationService>();
            services.AddSingleton<ICadConversionService, CadConversionService>();
            services.AddSingleton<IFamilyEditorService, FamilyEditorService>();
            services.AddSingleton<IConversionService, ConversionService>();
            services.AddSingleton<IGeometryBoundaryService, GeometryBoundaryService>();
            services.AddSingleton<ISplitBoundariesService, SplitBoundariesService>();
            services.AddSingleton<ILinkedModelExportService, LinkedModelExportService>();
            // Add other services here as we refactor
        }

        private static void ConfigureViewModels(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            services.AddTransient<ResetSlabsViewModel>();
            services.AddTransient<ConvertCadViewModel>();
            services.AddTransient<SexyRevitViewModel>();
            services.AddTransient<PurgeViewModel>();
            services.AddTransient<SearchReplaceViewModel>();
            services.AddTransient<AssignMaterialViewModel>();
            services.AddTransient<OffsetElevationsViewModel>();
            services.AddTransient<AlignEdgesViewModel>();
            services.AddTransient<UpdateContoursViewModel>();
            services.AddTransient<ChangeLevelViewModel>();
            services.AddTransient<AlignElementsViewModel>();
            services.AddTransient<SimplifyPointsViewModel>();
            services.AddTransient<FixPointsViewModel>();
            services.AddTransient<FilterCopyViewModel>();
            services.AddTransient<LogViewModel>();
            services.AddTransient<CategoryChangerViewModel>();
            services.AddTransient<RenderAppearanceViewModel>();
            services.AddTransient<PbrMaterialCreatorViewModel>();
            services.AddTransient<SplitBoundariesViewModel>();
            services.AddTransient<TypeToLinkedModelsViewModel>();
            services.AddTransient<ConvertFloorToToposolidViewModel>();
            services.AddTransient<ConvertToposolidToFloorViewModel>();
        }

        private static void ConfigureViews(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            // Views are often created by ViewModels or via a DialogService, 
            // but registering them can be useful if we use a Factory pattern.
            services.AddTransient<Views.ResetSlabsView>();
            services.AddTransient<Views.SexyRevitView>();
            services.AddTransient<Views.PurgeView>();
            services.AddTransient<Views.SearchReplaceView>();
            services.AddTransient<Views.AssignMaterialView>();
            services.AddTransient<Views.OffsetElevationsView>();
            services.AddTransient<Views.AlignEdgesView>();
            services.AddTransient<Views.UpdateContoursView>();
            services.AddTransient<Views.ChangeLevelView>();
            services.AddTransient<Views.AlignElementsView>();
            services.AddTransient<Views.SimplifyPointsView>();
            services.AddTransient<Views.FixPointsView>();
            services.AddTransient<Views.FilterCopyView>();
            services.AddTransient<Views.ConvertCadView>();
            services.AddTransient<Views.CategoryChangerView>();
            services.AddTransient<Views.LogView>();
            services.AddTransient<Views.HomeView>();
            services.AddTransient<Views.AlignDashboardView>();
            services.AddTransient<Views.RenderAppearanceView>();
            services.AddTransient<Views.PbrMaterialCreatorView>();
            services.AddTransient<Views.SplitBoundariesView>();
            services.AddTransient<Views.TypeToLinkedModelsView>();
            services.AddTransient<Views.ConvertFloorToToposolidView>();
            services.AddTransient<Views.ConvertToposolidToFloorView>();
        }
    }
}
