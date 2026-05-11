using System;
using LECG.Services.Logging;

namespace LECG.Services.Interfaces
{
    /// <summary>
    /// Information about a progress update.
    /// </summary>
    public class ProgressReport
    {
        public string Message { get; set; } = string.Empty;
        public double Percentage { get; set; }
    }

    /// <summary>
    /// Contract for reporting progress from long-running services to the UI.
    /// </summary>
    public interface IProgressReporter
    {
        void Report(string message, double percentage);
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    /// <summary>
    /// IProgressReporter backed by ILogger. Forwards LogWarning/LogError with severity preserved (CROSS-02).
    /// The preferred constructor takes ILogger. The legacy Action<ProgressReport> overload is retained
    /// for callers that drive their own UI update channels (ConvertCad, DivideToposolid, etc.)
    /// and will be removed in Wave 2 when those commands are migrated to ILogger.
    /// </summary>
    public sealed class SimpleProgressReporter : IProgressReporter
    {
        private readonly ILogger? _logger;
        private readonly Action<ProgressReport>? _onReport;
        private const string Scope = "Progress";

        /// <summary>
        /// Preferred constructor (Wave 1+): severity-preserving, routes through ILogger.
        /// </summary>
        public SimpleProgressReporter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Legacy constructor retained for UI-callback callers. Removed in Wave 2.
        /// </summary>
        [Obsolete("Use SimpleProgressReporter(ILogger) instead. Removed in Wave 2.")]
        public SimpleProgressReporter(Action<ProgressReport> onReport)
        {
            _onReport = onReport;
        }

        public void Report(string message, double percentage)
        {
            if (_logger != null)
                _logger.UpdateProgress(percentage, message);
            else
                _onReport?.Invoke(new ProgressReport { Message = message, Percentage = percentage });
        }

        public void Log(string message)
        {
            if (_logger != null)
                _logger.Log(message, Scope);
            else
                _onReport?.Invoke(new ProgressReport { Message = message });
        }

        public void LogWarning(string message)
        {
            if (_logger != null)
                _logger.LogWarning(message, Scope);
            else
                _onReport?.Invoke(new ProgressReport { Message = message });
        }

        public void LogError(string message)
        {
            if (_logger != null)
                _logger.LogError(message, Scope);
            else
                _onReport?.Invoke(new ProgressReport { Message = message });
        }
    }
}
