using System;
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
        
        protected Document Doc { get; private set; } = null!;
        protected UIDocument UIDoc { get; private set; } = null!;

        /// <summary>
        /// Main implementation method for commands.
        /// </summary>
        public abstract void Execute(UIDocument uiDoc, Document doc);

        /// <summary>
        /// Optional: Override to provide a transaction name. 
        /// If not null, a Transaction will be automatically started and committed.
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

                // Auto-Transaction Wrapper
                if (!string.IsNullOrEmpty(TransactionName))
                {
                    using (Transaction t = new Transaction(Doc, TransactionName))
                    {
                        t.Start();
                        Execute(UIDoc, Doc);
                        t.Commit();
                    }
                }
                else
                {
                    // No automatic transaction, let the command handle it OR read-only
                    Execute(UIDoc, Doc);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Log($"ERROR: {ex.Message}");
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
                if (System.Windows.Application.Current?.Dispatcher != null)
                    System.Windows.Application.Current.Dispatcher.Invoke(action);
                else
                    action();
            }
            catch (Exception ex)
            {
                 System.Windows.MessageBox.Show($"UI Dispatch Error: {ex.Message}", "LECG Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected void ClearLog() => Services.Logging.Logger.Instance.Clear();
    }
}
