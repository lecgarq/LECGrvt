using System.Reflection;
using Autodesk.Revit.UI;
using LECG.Configuration;
using LECG.Core.Ribbon;
using LECG.Utils;

namespace LECG
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            // Resolve Dependency Conflicts
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Name)) return null;
                
                var requestedName = new AssemblyName(args.Name);
                if (requestedName.Name != null && requestedName.Name.StartsWith("Microsoft.Extensions.DependencyInjection"))
                {
                    // 1. Check if ANY version is already loaded
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name == requestedName.Name) return asm;
                    }
                    
                    // 2. Try to load from our own directory
                    string? folderPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (string.IsNullOrEmpty(folderPath)) return null;
                    
                    string assemblyPath = System.IO.Path.Combine(folderPath, requestedName.Name + ".dll");
                    if (System.IO.File.Exists(assemblyPath))
                    {
                        return Assembly.LoadFrom(assemblyPath);
                    }
                }
                return null;
            };

            try
            {
                // 0. Initialize Services
                Core.Bootstrapper.Initialize();

                // 1. Initialize Ribbon
                var ribbonService = Core.ServiceLocator.GetRequiredService<IRibbonService>();
                ribbonService.InitializeRibbon(application);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("LECG Startup Error", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
