using Autodesk.Revit.UI;
using LECG.Configuration;
using LECG.Core.Ribbon;
using LECG.Utilities;
using LECG.Views.Base;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Markup;

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

                InitializeGlobalWpfDictionaries();

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
                LecgDialog.Show("LECG Startup Error", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            Core.Bootstrapper.Shutdown();
            return Result.Succeeded;
        }

        private static void RegisterGlobalExceptionHandlers()
        {
            if (_globalHandlersRegistered) return;
            _globalHandlersRegistered = true;

            Dispatcher.CurrentDispatcher.UnhandledException += (_, e) => HandleDispatcherUnhandledException(e);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => LogUnhandledException("Unhandled domain exception", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) => HandleUnobservedTaskException(e);
        }

        private static void InitializeGlobalWpfDictionaries()
        {
            try
            {
                System.Windows.Application app = System.Windows.Application.Current ?? new System.Windows.Application();

                var uri = new Uri("pack://application:,,,/LECG;component/src/Resources/Themes/LecgTheme.xaml", UriKind.Absolute);
                var dict = new System.Windows.ResourceDictionary { Source = uri };

                // Add or merge dictionary to global application resources
                bool exists = false;
                foreach (var md in app.Resources.MergedDictionaries)
                {
                    if (md.Source == uri)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    app.Resources.MergedDictionaries.Add(dict);
                }
            }
            catch (Exception ex) when (IsExpectedResourceInitializationException(ex))
            {
                // App-lifecycle exception path; ServiceLocator may not be initialized yet
                (Core.ServiceLocator.GetService<Services.Logging.ILogger>() as Services.Logging.ILogger)
                    ?.Log($"Failed to load global WPF resources: {ex.Message}", scope: "App");
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load global WPF resources: {ex.Message}");
            }
        }

        private static bool IsExpectedResourceInitializationException(Exception ex)
        {
            return ex is IOException
                or InvalidOperationException
                or NotSupportedException
                or UriFormatException
                or XamlParseException;
        }

        private static void HandleDispatcherUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            RunGlobalHandlerSafely(() =>
            {
                // App-lifecycle exception path; ServiceLocator may not be initialized yet
                (Core.ServiceLocator.GetService<Services.Logging.ILogger>() as Services.Logging.ILogger)
                    ?.Log($"Unhandled dispatcher exception: {e.Exception}", scope: "App");
                System.Diagnostics.Debug.WriteLine($"[App] Unhandled dispatcher exception: {e.Exception}");
                LecgDialog.Show("LECG Error", e.Exception.Message);
            });

            e.Handled = true;
        }

        private static void HandleUnobservedTaskException(UnobservedTaskExceptionEventArgs e)
        {
            LogUnhandledException("Unobserved task exception", e.Exception);
            e.SetObserved();
        }

        private static void LogUnhandledException(string messagePrefix, Exception? exception)
        {
            if (exception == null)
            {
                return;
            }

            RunGlobalHandlerSafely(() =>
            {
                // App-lifecycle exception path; ServiceLocator may not be initialized yet
                (Core.ServiceLocator.GetService<Services.Logging.ILogger>() as Services.Logging.ILogger)
                    ?.Log($"{messagePrefix}: {exception}", scope: "App");
                System.Diagnostics.Debug.WriteLine($"[App] {messagePrefix}: {exception}");
            });
        }

        private static void RunGlobalHandlerSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (IsExpectedGlobalHandlerException(ex))
            {
            }
        }

        private static bool IsExpectedGlobalHandlerException(Exception ex)
        {
            return ex is IOException
                or InvalidOperationException
                or NotSupportedException
                or XamlParseException;
        }
    }
}
