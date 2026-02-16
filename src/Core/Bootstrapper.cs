using Microsoft.Extensions.DependencyInjection;
using LECG.ViewModels;
using LECG.Interfaces;
using LECG.Services;
using LECG.Core.Ribbon;

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
            
            // Domain Services
            services.AddSingleton<ISlabService, SlabService>();
            services.AddSingleton<IOffsetService, OffsetService>();
            services.AddSingleton<IMaterialService, MaterialService>();
            services.AddSingleton<IPurgeService, PurgeService>();
            services.AddSingleton<ISchemaCleanerService, SchemaCleanerService>();
            services.AddSingleton<ISexyRevitService, SexyRevitService>();
            services.AddSingleton<ISearchReplaceService, SearchReplaceService>();
            services.AddSingleton<IFamilyConversionService, FamilyConversionService>();
            services.AddSingleton<IAlignEdgesService, AlignEdgesService>();
            services.AddSingleton<IToposolidService, ToposolidService>();
            services.AddSingleton<IChangeLevelService, ChangeLevelService>();
            services.AddSingleton<ISimplifyPointsService, SimplifyPointsService>();
            services.AddSingleton<IAlignElementsService, AlignElementsService>();
            services.AddSingleton<ICadConversionService, CadConversionService>();
            // Add other services here as we refactor
        }

        private static void ConfigureViewModels(IServiceCollection services)
        {
            services.AddTransient<ResetSlabsVM>();
            services.AddTransient<ConvertFamilyViewModel>();
            services.AddTransient<ConvertCadViewModel>();
            // Add other ViewModels here
        }

        private static void ConfigureViews(IServiceCollection services)
        {
            // Views are often created by ViewModels or via a DialogService, 
            // but registering them can be useful if we use a Factory pattern.
            services.AddTransient<Views.ResetSlabsView>();
        }
    }
}
