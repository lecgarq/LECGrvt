using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;
using LECG.Services.Logging;

namespace LECG.Core
{
    public abstract class RevitCommand : IExternalCommand
    {
        protected LECG.Views.LogView? _logWindow;
        protected LECG.ViewModels.LogViewModel? _logViewModel;
        protected Services.Logging.ILogger _logger = null!;
        private readonly Stopwatch _progressUpdateTimer = new Stopwatch();
        private double _lastProgressPercent = double.NaN;
        private string? _lastProgressStatus;

        protected Document Doc { get; private set; } = null!;
        protected UIDocument UIDoc { get; private set; } = null!;
        protected ExternalCommandData CommandData { get; private set; } = null!;

        public abstract void Execute(UIDocument uiDoc, Document doc);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            IDisposable? logScope = null;
            Microsoft.Extensions.Logging.ILogger? structuredLogger = null;
            string commandName = GetType().Name;

            try
            {
                InitializeCommandContext(commandData);
                logScope = CommandLogContext.Begin(commandName, GetDocumentTitle(Doc), commandData.Application.Application.VersionNumber);
                structuredLogger = GetStructuredLogger();
                structuredLogger?.LogInformation("Executing Revit command {CommandName}", commandName);
                PrepareCommandExecution();
                Execute(UIDoc, Doc);
                structuredLogger?.LogInformation("Completed Revit command {CommandName}", commandName);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                structuredLogger ??= GetStructuredLogger();
                structuredLogger?.LogError(ex, "Revit command {CommandName} failed", commandName);
                string detailed = BuildExceptionMessage(ex);
                message = detailed;
                Log($"ERROR: {detailed}");
                if (_logWindow == null) ShowLogWindow("Error Detected");
                return Result.Failed;
            }
            finally
            {
                logScope?.Dispose();
            }
        }

        protected void Log(string text) => _logger.Log(text, scope: GetType().Name);

        protected void UpdateProgress(double percent, string status)
        {
            if (!ShouldPublishProgress(percent, status)) return;
            RunOnUI(() => _logViewModel?.UpdateProgress(percent, status));
        }

        protected void ShowLogWindow(string title)
        {
            RunOnUI(() =>
            {
                if (_logWindow == null)
                {
                    var vm = ServiceLocator.GetRequiredService<LECG.ViewModels.LogViewModel>();
                    vm.Title = title;
                    _logViewModel = vm;
                    _logWindow = ServiceLocator.CreateWith<LECG.Views.LogView>(vm);
                    _logWindow.Topmost = true;
                    _logWindow.Closed += (s, e) => _logWindow = null;
                    _logWindow.Show();
                }
                else
                {
                    if (_logViewModel != null) _logViewModel.Title = title;
                    _logWindow.Activate();
                }
            });
        }

        protected void RunOnUI(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                if (dispatcher.CheckAccess()) action();
                else dispatcher.Invoke(action);
            }
            else action();
        }

        protected void InitializeCommandContext(ExternalCommandData commandData)
        {
            ArgumentNullException.ThrowIfNull(commandData);
            UIDoc = commandData.Application.ActiveUIDocument ?? throw new InvalidOperationException("No active document.");
            Doc = UIDoc.Document;
            CommandData = commandData;
        }

        public void PrepareAsSubCommand(ExternalCommandData commandData)
        {
            InitializeCommandContext(commandData);
            PrepareCommandExecution();
        }

        protected void PrepareCommandExecution()
        {
            _logger = ServiceLocator.GetRequiredService<Services.Logging.ILogger>();
            _logger.Clear();
            _logger.SetDispatcher(System.Windows.Application.Current?.Dispatcher);
            _lastProgressPercent = double.NaN;
            _lastProgressStatus = null;
            _progressUpdateTimer.Restart();
        }

        private bool ShouldPublishProgress(double percent, string status)
        {
            if (double.IsNaN(_lastProgressPercent) || percent >= 100)
            {
                _lastProgressPercent = percent;
                _lastProgressStatus = status;
                _progressUpdateTimer.Restart();
                return true;
            }
            if (!string.Equals(_lastProgressStatus, status, StringComparison.Ordinal) ||
                Math.Abs(percent - _lastProgressPercent) >= 1d ||
                _progressUpdateTimer.ElapsedMilliseconds >= 250)
            {
                _lastProgressPercent = percent;
                _lastProgressStatus = status;
                _progressUpdateTimer.Restart();
                return true;
            }
            return false;
        }

        private static string BuildExceptionMessage(Exception ex)
        {
            var sb = new StringBuilder();
            Exception? current = ex;
            for (int i = 0; i < 5 && current != null; i++)
            {
                if (i > 0) sb.Append(" | Inner: ");
                sb.Append(current.Message);
                current = current.InnerException;
            }
            return sb.ToString();
        }

        private Microsoft.Extensions.Logging.ILogger? GetStructuredLogger()
        {
            ILoggerFactory? loggerFactory = ServiceLocator.GetService<ILoggerFactory>();
            return loggerFactory?.CreateLogger(GetType().FullName ?? GetType().Name);
        }

        private static string? GetDocumentTitle(Document? doc)
        {
            if (doc == null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(doc.Title) ? null : doc.Title;
        }
    }
}
