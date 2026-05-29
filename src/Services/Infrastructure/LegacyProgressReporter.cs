using System;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    /// <summary>
    /// IProgressReporter backed by ILogger. Forwards LogWarning/LogError with severity preserved (CROSS-02).
    /// The optional progressCallback drives the status-bar channel separately from log entries.
    /// </summary>
    public class LegacyProgressReporter : IProgressReporter
    {
        private readonly ILogger _logger;
        private readonly Action<double, string>? _progressCallback;
        private const string Scope = "Progress";

        /// <summary>
        /// Severity-preserving reporter that routes through ILogger.
        /// </summary>
        public LegacyProgressReporter(ILogger logger, Action<double, string>? progressCallback = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _progressCallback = progressCallback;
        }

        public void Report(string message, double percentage)
        {
            _progressCallback?.Invoke(percentage, message);
            _logger.UpdateProgress(percentage, message);
        }

        public void Log(string message)
        {
            _logger.Log(message, Scope);
        }

        public void LogWarning(string message)
        {
            _logger.LogWarning(message, Scope);
        }

        public void LogError(string message)
        {
            _logger.LogError(message, Scope);
        }
    }
}
