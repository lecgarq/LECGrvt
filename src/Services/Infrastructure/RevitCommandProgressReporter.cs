using System;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    /// <summary>
    /// IProgressReporter backed by ILogger. Forwards LogWarning/LogError with severity preserved (CROSS-02).
    /// The progress Action drives the status-bar channel (UpdateProgress) separately from log entries.
    /// </summary>
    public class RevitCommandProgressReporter : IProgressReporter
    {
        private readonly ILogger _logger;
        private readonly Action<double, string> _progress;
        private const string Scope = "Progress";

        public RevitCommandProgressReporter(ILogger logger, Action<double, string> progress)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public void Report(string message, double percentage)
        {
            _progress(percentage, message);
            _logger.UpdateProgress(percentage, message);
        }

        public void Log(string message) => _logger.Log(message, Scope);

        public void LogWarning(string message) => _logger.LogWarning(message, Scope);

        public void LogError(string message) => _logger.LogError(message, Scope);
    }
}
