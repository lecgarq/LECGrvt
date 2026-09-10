using Microsoft.Extensions.DependencyInjection;
using LECG.ViewModels;
using LECG.Services.Interfaces;
using LECG.Services;
using LECG.Core.Ribbon;
using LECG.Models;
using LECG.Services.Logging;
using MsLoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
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

            // Pre-DI: buffer any startup warnings so they can be replayed after the
            // container is built (Logger singleton is not yet available at this point).
            var startupBuffer = new System.Collections.Generic.List<(string msg, bool isWarning)>();
            if (!string.IsNullOrWhiteSpace(loggingInitializationError))
            {
                startupBuffer.Add(($"Structured logging fallback enabled: {loggingInitializationError}", true));
            }

            ConfigureServices(services, loggerFactory);
            ConfigureViewModels(services);
            ConfigureViews(services);

            _provider = services.BuildServiceProvider();
            ServiceLocator.Initialize(_provider);

            // Post-DI: configure structured logger, then flush buffered startup entries.
            var logger = _provider.GetRequiredService<LECG.Services.Logging.ILogger>();
            if (logger is LECG.Services.Logging.Logger concreteLogger)
                concreteLogger.ConfigureStructuredLogger(loggerFactory);
            foreach (var (msg, isWarning) in startupBuffer)
            {
                if (isWarning)
                    logger.LogWarning(msg, scope: "Bootstrapper");
                else
                    logger.Log(msg, scope: "Bootstrapper");
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
            services.AddSingleton<MsLoggerFactory>(_ => loggerFactory);
            services.AddSingleton<RibbonService>();
            services.AddSingleton<LECG.Services.Logging.ILogger, Logger>();
            services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions()));
            services.AddSingleton<ISelectionCoordinator, SelectionCoordinator>();
            services.AddSingleton<ITransactionService, TransactionService>();

            // Domain Services
            services.AddSingleton<SlabService>();
            services.AddSingleton<OffsetService>();
            services.AddSingleton<IRenderSolidFillPatternService, RenderSolidFillPatternService>();
            services.AddSingleton<IRenderMaterialGraphicsApplyService, RenderMaterialGraphicsApplyService>();
            services.AddSingleton<RenderMaterialSyncExecutionService>();
            services.AddSingleton<RenderAppearanceSingleSyncService>();
            services.AddSingleton<RenderAppearanceBatchSyncService>();
            services.AddSingleton<RenderAppearanceService>();
            services.AddSingleton<MaterialTypeAssignmentService>();
            services.AddSingleton<IMaterialCreationService, MaterialCreationService>();
            services.AddSingleton<IMaterialColorSequenceService, MaterialColorSequenceService>();
            services.AddSingleton<IMaterialTextureLookupService, MaterialTextureLookupService>();
            services.AddSingleton<MaterialBitmapPropertyService>();
            services.AddSingleton<IImageColorExtractionService, ImageColorExtractionService>();
            services.AddSingleton<IMaterialAppearanceAssetService, MaterialAppearanceAssetService>();
            services.AddSingleton<IMaterialPbrService, MaterialPbrService>();
            services.AddSingleton<IPbrTextureBakeService, PbrTextureBakeService>();
            services.AddSingleton<IAdvancedAppearanceAssetService, AdvancedAppearanceAssetService>();
            services.AddSingleton<IMetallicProbeService, MetallicProbeService>();
            services.AddSingleton<ISubstanceMaterialCreateService, SubstanceMaterialCreateService>();
            services.AddSingleton<MaterialElementGroupingService>();
            services.AddSingleton<MaterialTypeEligibilityService>();
            services.AddSingleton<MaterialTypeAssignmentProcessService>();
            services.AddSingleton<MaterialAssignmentExecutionService>();
            services.AddSingleton<PurgeDeleteElementService>();
            services.AddSingleton<LinePatternCompactionService>();
            services.AddSingleton<FillPatternCompactionService>();
            services.AddSingleton<TextStyleCompactionService>();
            services.AddSingleton<LineStyleCompactionService>();
            services.AddSingleton<PurgeMaterialService>();
            services.AddSingleton<PurgeLineStyleService>();
            services.AddSingleton<PurgeLinePatternService>();
            services.AddSingleton<PurgeFillPatternService>();
            services.AddSingleton<PurgeLevelService>();
            services.AddSingleton<PurgeExtendedElementService>();
            services.AddSingleton<PurgeParameterService>();
            services.AddSingleton<PurgePassExecutionService>();
            services.AddSingleton<DeepPurgeService>();
            services.AddSingleton<PurgeService>();
            services.AddSingleton<SchemaCleanerService>();
            services.AddSingleton<SexyRevitService>();
            services.AddSingleton<IFilterCopyService, FilterCopyService>();
            services.AddSingleton<IBaseElementCollectionService, BaseElementCollectionService>();
            services.AddSingleton<IRenameRulePipelineService, RenameRulePipelineService>();
            services.AddSingleton<ISearchReplacePreviewService, SearchReplacePreviewService>();
            services.AddSingleton<IBatchRenameExecutionService, BatchRenameExecutionService>();
            services.AddSingleton<IFormulaUpdateService, FormulaUpdateService>();
            services.AddSingleton<ISearchReplaceService, SearchReplaceService>();
            services.AddSingleton<FamilyTemplatePathService>();
            services.AddSingleton<FamilyGeometryCollectionService>();
            services.AddSingleton<FamilyTempFileCleanupService>();
            services.AddSingleton<FamilyConversionFinalizeService>();
            services.AddSingleton<FamilyLoadOptionsFactory>();
            services.AddSingleton<FamilyParameterSetupService>();
            services.AddSingleton<FamilyConversionLoggingService>();
            services.AddSingleton<FamilySourceDocumentService>();
            services.AddSingleton<FamilyConversionExecutionService>();
            services.AddSingleton<FamilyGeometryCopyService>();
            services.AddSingleton<FamilySaveService>();
            services.AddSingleton<FamilySaveLoadService>();
            services.AddSingleton<FamilyTargetDocumentService>();
            services.AddSingleton<FamilyProjectLoadService>();
            services.AddSingleton<FamilyConversionService>();
            services.AddSingleton<ReferenceRaycastService>();
            services.AddSingleton<AlignEdgesCurveDivisionService>();
            services.AddSingleton<AlignEdgesHitPointProjectionService>();
            services.AddSingleton<AlignEdgesIntersectorService>();
            services.AddSingleton<AlignEdgesBoundaryPointService>();
            services.AddSingleton<AlignEdgesBoundaryCollectionService>();
            services.AddSingleton<AlignEdgesPointInsertionService>();
            services.AddSingleton<AlignEdgesVertexAlignmentService>();
            services.AddSingleton<AlignEdgesToposolidProcessingService>();
            services.AddSingleton<AlignEdgesService>();
            services.AddSingleton<ToposolidService>();
            services.AddSingleton<ChangeLevelService>();
            services.AddSingleton<WarningsService>();
            services.AddSingleton<SimplifyPointsService>();
            services.AddSingleton<FixPointsService>();
            services.AddSingleton<AlignElementsService>();
            services.AddSingleton<CadPlacementViewService>();
            services.AddSingleton<CadFamilySymbolService>();
            services.AddSingleton<CadFamilyLoadResolveService>();
            services.AddSingleton<CadFamilyInstancePlacementService>();
            services.AddSingleton<CadLineStyleService>();
            services.AddSingleton<CadLineMergeService>();
            services.AddSingleton<CadPointFlattenService>();
            services.AddSingleton<CadDoubleArrayConversionService>();
            services.AddSingleton<CadCurveTessellationService>();
            services.AddSingleton<CadSplineFlattenService>();
            services.AddSingleton<CadCurveFlattenService>();
            services.AddSingleton<CadFilledRegionTypeService>();
            services.AddSingleton<CadFamilyLoadPlacementService>();
            services.AddSingleton<CadTempFileCleanupService>();
            services.AddSingleton<CadSolidHatchExtractionService>();
            services.AddSingleton<CadPolylineExtractionService>();
            services.AddSingleton<CadGeometryExtractionService>();
            services.AddSingleton<CadGeometryOptimizationService>();
            services.AddSingleton<CadImportDataPreparationService>();
            services.AddSingleton<CadImportFamilyCreationService>();
            services.AddSingleton<CadDataValidationService>();
            services.AddSingleton<CadImportInstanceCenterService>();
            services.AddSingleton<CadDrawingViewService>();
            services.AddSingleton<CadSourceCleanupService>();
            services.AddSingleton<CadRenderContextService>();
            services.AddSingleton<CadCurveRenderService>();
            services.AddSingleton<CadHatchProgressService>();
            services.AddSingleton<CadHatchLoopPreparationService>();
            services.AddSingleton<CadHatchRenderService>();
            services.AddSingleton<CadDataDrawService>();
            services.AddSingleton<CadFamilySaveService>();
            services.AddSingleton<CadTempDwgExtractionService>();
            services.AddSingleton<CadFamilyBuildService>();
            services.AddSingleton<CadDwgFamilyCreationService>();
            services.AddSingleton<CadConversionService>();
            services.AddSingleton<FamilyEditorService>();
            services.AddSingleton<ISharedToFamilyParameterService, SharedToFamilyParameterService>();
            services.AddSingleton<ConversionService>();
            services.AddSingleton<GeometryBoundaryService>();
            services.AddSingleton<SplitBoundariesService>();
            services.AddSingleton<DivideToposolidService>();
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
            services.AddTransient<WarningsViewModel>();
            services.AddTransient<AlignElementsViewModel>();
            services.AddTransient<SimplifyPointsViewModel>();
            services.AddTransient<FixPointsViewModel>();
            services.AddTransient<FilterCopyViewModel>();
            services.AddTransient<LogViewModel>();
            services.AddTransient<CategoryChangerViewModel>();
            services.AddTransient<RenderAppearanceViewModel>();
            services.AddTransient<PbrMaterialCreatorViewModel>();
            services.AddTransient<SubstanceBatchViewModel>();
            services.AddTransient<SplitBoundariesViewModel>();
            services.AddTransient<DivideToposolidViewModel>();
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
            services.AddTransient<Views.WarningsView>();
            services.AddTransient<Views.AlignElementsView>();
            services.AddTransient<Views.SimplifyPointsView>();
            services.AddTransient<Views.FixPointsView>();
            services.AddTransient<Views.FilterCopyView>();
            services.AddTransient<Views.ConvertCadView>();
            services.AddTransient<Views.CategoryChangerView>();
            services.AddTransient<Views.LogView>();
            services.AddTransient<Views.HomeView>();
            services.AddTransient<Views.RenderAppearanceView>();
            services.AddTransient<Views.PbrMaterialCreatorView>();
            services.AddTransient<Views.SubstanceBatchView>();
            services.AddTransient<Views.SplitBoundariesView>();
            services.AddTransient<Views.DivideToposolidView>();
            services.AddTransient<Views.TypeToLinkedModelsView>();
            services.AddTransient<Views.ConvertFloorToToposolidView>();
            services.AddTransient<Views.ConvertToposolidToFloorView>();
        }
    }
}
