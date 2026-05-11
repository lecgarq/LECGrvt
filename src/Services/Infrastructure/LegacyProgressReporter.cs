using System;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    /// <summary>
    /// IProgressReporter backed by ILogger. Forwards LogWarning/LogError with severity preserved (CROSS-02).
    /// The optional progressCallback drives the status-bar channel separately from log entries.
    /// Legacy callback constructor retained for existing callers; removed in Wave 2.
    /// </summary>
    public class LegacyProgressReporter : IProgressReporter
    {
        private readonly ILogger? _logger;
        private readonly Action<double, string>? _progressCallback;
        private readonly Action<string>? _logCallback;
        private const string Scope = "Progress";

        /// <summary>
        /// Preferred constructor (Wave 1+): severity-preserving, routes through ILogger.
        /// </summary>
        public LegacyProgressReporter(ILogger logger, Action<double, string>? progressCallback = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _progressCallback = progressCallback;
        }

        /// <summary>
        /// Legacy callback constructor retained for existing callers. Removed in Wave 2.
        /// </summary>
        [Obsolete("Use LegacyProgressReporter(ILogger, Action<double,string>?) instead. Removed in Wave 2.")]
        public LegacyProgressReporter(Action<double, string>? progressCallback = null, Action<string>? logCallback = null)
        {
            _progressCallback = progressCallback;
            _logCallback = logCallback;
        }

        public void Report(string message, double percentage)
        {
            _progressCallback?.Invoke(percentage, message);
            _logger?.UpdateProgress(percentage, message);
        }

        public void Log(string message)
        {
            if (_logger != null)
                _logger.Log(message, Scope);
            else
                _logCallback?.Invoke(message);
        }

        public void LogWarning(string message)
        {
            if (_logger != null)
                _logger.LogWarning(message, Scope);
            else
                _logCallback?.Invoke(message);
        }

        public void LogError(string message)
        {
            if (_logger != null)
                _logger.LogError(message, Scope);
            else
                _logCallback?.Invoke(message);
        }
    }
}
