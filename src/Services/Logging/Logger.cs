using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace LECG.Services.Logging
{
    public interface ILogger
    {
        ObservableCollection<LogEntry> Entries { get; }
        void Log(string message);
        void LogSuccess(string message);
        void LogWarning(string message);
        void LogError(string message);
        void Clear();
        void SetDispatcher(Dispatcher dispatcher);
        void UpdateProgress(double percent, string status);
        event Action<double, string> OnProgressUpdate;
    }

    public class Logger : ILogger
    {
        private static Logger? _instance;
        public static Logger Instance => _instance ??= new Logger();

        public ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();
        private Dispatcher? _uiDispatcher;
        public event Action<double, string>? OnProgressUpdate;

        private Logger()
        {
            // Attempt to capture current dispatcher, can be overwritten
            if (Dispatcher.CurrentDispatcher != null)
            {
                _uiDispatcher = Dispatcher.CurrentDispatcher;
            }
        }

        public void SetDispatcher(Dispatcher? dispatcher)
        {
            _uiDispatcher = dispatcher;
        }

        public void UpdateProgress(double percent, string status)
        {
            if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(() => OnProgressUpdate?.Invoke(percent, status));
            }
            else
            {
                OnProgressUpdate?.Invoke(percent, status);
            }
            DoEvents();
        }

        private void AddEntry(LogEntry entry)
        {
            if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(() => Entries.Add(entry));
            }
            else
            {
                Entries.Add(entry);
            }
            
            // Force UI update for real-time feel if on valid thread
            DoEvents(); 
        }

        public void Log(string message) => AddEntry(new LogEntry(message, LogLevel.Info));
        public void LogSuccess(string message) => AddEntry(new LogEntry(message, LogLevel.Success));
        public void LogWarning(string message) => AddEntry(new LogEntry(message, LogLevel.Warning));
        public void LogError(string message) => AddEntry(new LogEntry(message, LogLevel.Error));

        public void Clear()
        {
            if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(() => Entries.Clear());
            }
            else
            {
                Entries.Clear();
            }
        }

        private void DoEvents()
        {
            if (_uiDispatcher == null) return;
            
            try 
            {
                if (_uiDispatcher.CheckAccess())
                {
                    DispatcherFrame frame = new DispatcherFrame();
                    _uiDispatcher.BeginInvoke(DispatcherPriority.Background,
                        new DispatcherOperationCallback(ExitFrame), frame);
                    Dispatcher.PushFrame(frame);
                }
            }
            catch { }
        }

        private object? ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }
    }
}
