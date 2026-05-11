using System;
using System.Diagnostics;
using LECG.Services.Logging;

namespace LECG.Utilities
{
    /// <summary>
    /// Utility to measure and log execution time of code blocks using the disposable pattern.
    /// </summary>
    public class ExecutionTimer : IDisposable
    {
        private readonly string _operationName;
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public ExecutionTimer(string operationName, ILogger logger)
        {
            _operationName = operationName;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stopwatch = Stopwatch.StartNew();
            _logger.Log($"[PERF] Starting: {_operationName}", scope: "ExecutionTimer");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _stopwatch.Stop();
                _logger.Log($"[PERF] Completed: {_operationName} | Elapsed: {_stopwatch.Elapsed.TotalSeconds:F2}s", scope: "ExecutionTimer");
            }

            _disposed = true;
        }
    }
}
