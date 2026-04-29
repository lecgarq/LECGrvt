using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MsLoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

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
        private readonly object _sync = new object();
        private readonly Queue<LogEntry> _pendingEntries = new Queue<LogEntry>();
        private const int VisibleEntryFlushIntervalMs = 250;
        private const int MaxVisibleEntries = 300;

        public ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();
        private Dispatcher? _uiDispatcher;
        private DispatcherTimer? _flushTimer;
        private MsLoggerFactory? _loggerFactory;
        private string _defaultCategoryName = "LECG.UI";
        public event Action<double, string>? OnProgressUpdate;

        private Logger()
        {
            // Attempt to capture current dispatcher, can be overwritten
            if (Dispatcher.CurrentDispatcher != null)
            {
                SetDispatcher(Dispatcher.CurrentDispatcher);
            }
        }

        public void SetDispatcher(Dispatcher? dispatcher)
        {
            if (_uiDispatcher == dispatcher)
            {
                return;
            }

            _flushTimer?.Stop();
            _uiDispatcher = dispatcher;
            if (_uiDispatcher != null)
            {
                _flushTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(VisibleEntryFlushIntervalMs),
                    DispatcherPriority.Background,
                    FlushTimerTick,
                    _uiDispatcher);
                _flushTimer.Stop();
            }
            else
            {
                _flushTimer = null;
            }
        }

        public void ConfigureStructuredLogger(MsLoggerFactory loggerFactory, string defaultCategoryName = "LECG.UI")
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);

            lock (_sync)
            {
                _loggerFactory = loggerFactory;
                _defaultCategoryName = string.IsNullOrWhiteSpace(defaultCategoryName)
                    ? "LECG.UI"
                    : defaultCategoryName;
            }
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

        private void AddEntry(LogEntry entry, Exception? exception = null, string? categoryName = null)
        {
            if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.BeginInvoke(() => EnqueueVisibleEntry(entry));
            }
            else
            {
                EnqueueVisibleEntry(entry);
            }

            ForwardToStructuredLogger(entry, exception, categoryName);
        }

        public void Log(string message) => AddEntry(new LogEntry(message, LogLevel.Info));
        public void LogSuccess(string message) => AddEntry(new LogEntry(message, LogLevel.Success));
        public void LogWarning(string message) => AddEntry(new LogEntry(message, LogLevel.Warning));
        public void LogError(string message) => AddEntry(new LogEntry(message, LogLevel.Error));
        public void LogWarning(string message, string categoryName, Exception? exception = null) => AddEntry(new LogEntry(message, LogLevel.Warning), exception, categoryName);
        public void LogError(string message, string categoryName, Exception? exception = null) => AddEntry(new LogEntry(message, LogLevel.Error), exception, categoryName);

        public void Clear()
        {
            void ClearCore()
            {
                _flushTimer?.Stop();
                _pendingEntries.Clear();
                Entries.Clear();
            }

            if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(ClearCore);
            }
            else
            {
                ClearCore();
            }
        }

        private void EnqueueVisibleEntry(LogEntry entry)
        {
            if (_flushTimer == null)
            {
                Entries.Add(entry);
                TrimVisibleEntries();
                return;
            }

            _pendingEntries.Enqueue(entry);
            if (!_flushTimer.IsEnabled)
            {
                _flushTimer.Start();
            }
        }

        private void FlushTimerTick(object? sender, EventArgs e)
        {
            if (_pendingEntries.Count == 0)
            {
                _flushTimer?.Stop();
                return;
            }

            while (_pendingEntries.Count > 0)
            {
                Entries.Add(_pendingEntries.Dequeue());
            }

            TrimVisibleEntries();
            _flushTimer?.Stop();
        }

        private void TrimVisibleEntries()
        {
            while (Entries.Count > MaxVisibleEntries)
            {
                Entries.RemoveAt(0);
            }
        }

        private void ForwardToStructuredLogger(LogEntry entry, Exception? exception, string? categoryName)
        {
            MsLoggerFactory? loggerFactory;
            string fallbackCategoryName;

            lock (_sync)
            {
                loggerFactory = _loggerFactory;
                fallbackCategoryName = _defaultCategoryName;
            }

            if (loggerFactory == null)
            {
                return;
            }

            string loggerCategory = string.IsNullOrWhiteSpace(categoryName) ? fallbackCategoryName : categoryName;
            Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger(loggerCategory);
            MsLogLevel level = ToMicrosoftLogLevel(entry.Level);

            if (exception == null)
            {
                logger.Log(level, "{Message}", entry.Message);
                return;
            }

            logger.Log(level, exception, "{Message}", entry.Message);
        }

        private static MsLogLevel ToMicrosoftLogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Warning => MsLogLevel.Warning,
                LogLevel.Error => MsLogLevel.Error,
                LogLevel.Success => MsLogLevel.Information,
                _ => MsLogLevel.Information
            };
        }

    }
}
