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
                _uiDispatcher.BeginInvoke(() => OnProgressUpdate?.Invoke(percent, status));
            }
            else
            {
                OnProgressUpdate?.Invoke(percent, status);
            }
        }

        private void AddEntry(LogEntry entry)
        {
            if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.BeginInvoke(() => Entries.Add(entry));
            }
            else
            {
                Entries.Add(entry);
            }
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

    }
}
