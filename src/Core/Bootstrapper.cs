using Microsoft.Extensions.DependencyInjection;
using LECG.ViewModels;
using LECG.Services.Interfaces;
using LECG.Services;
using LECG.Core.Ribbon;
using LECG.Services.Logging;

namespace LECG.Core
{
    public static class Bootstrapper
    {
        public static void Initialize()
        {
            var services = new ServiceCollection();

            ConfigureServices(services);
            ConfigureViewModels(services);
            ConfigureViews(services);

            var provider = services.BuildServiceProvider();
            ServiceLocator.Initialize(provider);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Core
            services.AddSingleton<IRibbonService, RibbonService>();
            services.AddSingleton<ILogger>(_ => Logger.Instance);
            
            // Domain Services
            services.AddSingleton<ISlabService, SlabService>();
            services.AddSingleton<IOffsetService, OffsetService>();
            services.AddSingleton<IRenderAppearanceService, RenderAppearanceService>();
            services.AddSingleton<IMaterialTypeAssignmentService, MaterialTypeAssignmentService>();
            services.AddSingleton<IMaterialCreationService, MaterialCreationService>();
            services.AddSingleton<IMaterialService, MaterialService>();
            services.AddSingleton<IPurgeReferenceScannerService, PurgeReferenceScannerService>();
            services.AddSingleton<IPurgeMaterialService, PurgeMaterialService>();
            services.AddSingleton<IPurgeLineStyleService, PurgeLineStyleService>();
            services.AddSingleton<IPurgeFillPatternService, PurgeFillPatternService>();
            services.AddSingleton<IPurgeService, PurgeService>();
            services.AddSingleton<ISchemaCleanerService, SchemaCleanerService>();
            services.AddSingleton<ISexyRevitService, SexyRevitService>();
            services.AddSingleton<IRenameRulePipelineService, RenameRulePipelineService>();
            services.AddSingleton<ISearchReplaceService, SearchReplaceService>();
            services.AddSingleton<IFamilyConversionService, FamilyConversionService>();
            services.AddSingleton<IReferenceRaycastService, ReferenceRaycastService>();
            services.AddSingleton<IAlignEdgesIntersectorService, AlignEdgesIntersectorService>();
            services.AddSingleton<IAlignEdgesService, AlignEdgesService>();
            services.AddSingleton<IToposolidService, ToposolidService>();
            services.AddSingleton<IChangeLevelService, ChangeLevelService>();
            services.AddSingleton<ISimplifyPointsService, SimplifyPointsService>();
            services.AddSingleton<IAlignElementsService, AlignElementsService>();
            services.AddSingleton<ICadPlacementViewService, CadPlacementViewService>();
            services.AddSingleton<ICadFamilySymbolService, CadFamilySymbolService>();
            services.AddSingleton<ICadLineStyleService, CadLineStyleService>();
            services.AddSingleton<ICadLineMergeService, CadLineMergeService>();
            services.AddSingleton<ICadCurveFlattenService, CadCurveFlattenService>();
            services.AddSingleton<ICadFilledRegionTypeService, CadFilledRegionTypeService>();
            services.AddSingleton<ICadFamilyLoadPlacementService, CadFamilyLoadPlacementService>();
            services.AddSingleton<ICadGeometryExtractionService, CadGeometryExtractionService>();
            services.AddSingleton<ICadConversionService, CadConversionService>();
            // Add other services here as we refactor
        }

        private static void ConfigureViewModels(IServiceCollection services)
        {
            services.AddTransient<ResetSlabsVM>();
            services.AddTransient<ConvertFamilyViewModel>();
            services.AddTransient<ConvertCadViewModel>();
            services.AddTransient<SexyRevitViewModel>();
            services.AddTransient<PurgeViewModel>();
            services.AddTransient<SearchReplaceViewModel>();
            services.AddTransient<AssignMaterialViewModel>();
            services.AddTransient<OffsetElevationsVM>();
            services.AddTransient<AlignEdgesViewModel>();
            services.AddTransient<UpdateContoursViewModel>();
            services.AddTransient<ChangeLevelViewModel>();
            services.AddTransient<AlignElementsViewModel>();
            services.AddTransient<SimplifyPointsViewModel>();
            services.AddTransient<FilterCopyViewModel>();
            services.AddTransient<LogViewModel>();
            services.AddTransient<RenderAppearanceViewModel>();
        }

        private static void ConfigureViews(IServiceCollection services)
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
            services.AddTransient<Views.FilterCopyView>();
            services.AddTransient<Views.ConvertCadView>();
            services.AddTransient<Views.ConvertFamilyView>();
            services.AddTransient<Views.LogView>();
            services.AddTransient<Views.HomeView>();
            services.AddTransient<Views.AlignDashboardView>();
            services.AddTransient<Views.RenderAppearanceView>();
        }
    }
}
