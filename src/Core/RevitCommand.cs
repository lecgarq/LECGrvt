using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LECG.Core
{
    /// <summary>
    /// Enhanced base class for LECG commands.
    /// Handles Exception logging, Transaction management, and UI updates automatically.
    /// </summary>
    public abstract class RevitCommand : IExternalCommand
    {
        // Internal fields
        protected Views.LogView? _logWindow;
        protected ViewModels.LogViewModel? _logViewModel;
        private readonly Stopwatch _progressUpdateTimer = new Stopwatch();
        private double _lastProgressPercent = double.NaN;
        private string? _lastProgressStatus;
        
        protected Document Doc { get; private set; } = null!;
        protected UIDocument UIDoc { get; private set; } = null!;

        /// <summary>
        /// Main implementation method for commands.
        /// </summary>
        public abstract void Execute(UIDocument uiDoc, Document doc);

        /// <summary>
        /// Legacy compatibility hook kept so existing commands compile.
        /// Automatic transactions are no longer started by the base class.
        /// </summary>
        protected virtual string? TransactionName => null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ArgumentNullException.ThrowIfNull(commandData);

            try
            {
                // Setup Context
                UIDoc = commandData.Application.ActiveUIDocument;
                Doc = UIDoc.Document;
                
                // Reset Logger for new command execution
                Services.Logging.Logger.Instance.Clear();
                Services.Logging.Logger.Instance.SetDispatcher(System.Windows.Application.Current?.Dispatcher);
                ResetProgressTracking();

                Execute(UIDoc, Doc);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string detailed = BuildExceptionMessage(ex);
                message = detailed;
                Log($"ERROR: {detailed}");
                // Ensure log window is visible on error
                if (_logWindow == null) ShowLogWindow("Error Detected");
                return Result.Failed;
            }
        }

        // ============================================
        // LOGGING & UI HELPERS
        // ============================================

        protected void Log(string text)
        {
            // Use the centralized Logger service
            Services.Logging.Logger.Instance.Log(text);
        }

        protected void UpdateProgress(double percent, string status)
        {
            if (!ShouldPublishProgress(percent, status))
            {
                return;
            }

            RunOnUI(() => 
            {
                _logViewModel?.UpdateProgress(percent, status);
            });
        }

        protected void ShowLogWindow(string title)
        {
            RunOnUI(() =>
            {
                try 
                {
                    if (_logWindow == null)
                    {
                        _logViewModel = ServiceLocator.GetRequiredService<ViewModels.LogViewModel>();
                        _logViewModel.Title = title;
                        
                        _logWindow = ServiceLocator.CreateWith<Views.LogView>(_logViewModel);
                        _logWindow.Topmost = true;
                        
                        // Handle window closing to release reference
                        _logWindow.Closed += (s, e) => _logWindow = null;
                        
                        _logWindow.Show();
                    }
                    else
                    {
                        if (_logViewModel != null) _logViewModel.Title = title;
                        _logWindow.Activate();
                    }
                }
                catch (Exception ex)
                {
                    // Fallback if Log Window fails (e.g. XAML error)
                    System.Windows.MessageBox.Show($"Could not show Log Window: {ex.Message}", "LECG Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        protected void RunOnUI(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    if (dispatcher.CheckAccess())
                    {
                        action();
                    }
                    else
                    {
                        dispatcher.Invoke(action);
                    }
                }
                else
                    action();
            }
            catch (Exception ex)
            {
                 // Avoid showing too many popups during a loop
                 System.Diagnostics.Debug.WriteLine($"UI Dispatch Error: {ex.Message}");
            }
        }

        private void ResetProgressTracking()
        {
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

            bool statusChanged = !string.Equals(_lastProgressStatus, status, StringComparison.Ordinal);
            bool percentAdvanced = Math.Abs(percent - _lastProgressPercent) >= 1d;
            bool timeElapsed = _progressUpdateTimer.ElapsedMilliseconds >= 250;

            if (!statusChanged && !percentAdvanced && !timeElapsed)
            {
                return false;
            }

            _lastProgressPercent = percent;
            _lastProgressStatus = status;
            _progressUpdateTimer.Restart();
            return true;
        }

        private static string BuildExceptionMessage(Exception ex)
        {
            var sb = new StringBuilder();
            int depth = 0;
            Exception? current = ex;

            while (current != null && depth < 5)
            {
                if (depth > 0) sb.Append(" | Inner: ");
                sb.Append(current.Message);
                current = current.InnerException;
                depth++;
            }

            return sb.ToString();
        }

        protected void ClearLog() => Services.Logging.Logger.Instance.Clear();
    }
}
