using Autodesk.Revit.UI;
using LECG.Configuration;
using LECG.Core.Ribbon;
using LECG.Utils;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LECG
{
    public class App : IExternalApplication
    {
        private static bool _globalHandlersRegistered;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Ensure pack URI scheme is registered (Fixes "The URI prefix is not recognized" in .NET 8 / Revit 2026)
                if (!System.UriParser.IsKnownScheme("pack"))
                {
                    // This static access triggers the registration of the pack:// scheme
                    _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
                }

                RegisterGlobalExceptionHandlers();

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

        private static void RegisterGlobalExceptionHandlers()
        {
            if (_globalHandlersRegistered) return;
            _globalHandlersRegistered = true;

            Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
            {
                try
                {
                    Services.Logging.Logger.Instance.Log($"Unhandled dispatcher exception: {e.Exception}");
                    TaskDialog.Show("LECG Error", e.Exception.Message);
                }
                catch
                {
                }

                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    if (e.ExceptionObject is Exception ex)
                    {
                        Services.Logging.Logger.Instance.Log($"Unhandled domain exception: {ex}");
                    }
                }
                catch
                {
                }
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                try
                {
                    Services.Logging.Logger.Instance.Log($"Unobserved task exception: {e.Exception}");
                }
                catch
                {
                }

                e.SetObserved();
            };
        }
    }
}
