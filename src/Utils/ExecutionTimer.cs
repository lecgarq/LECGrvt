using System;
using System.Diagnostics;
using LECG.Services.Logging;

namespace LECG.Utils
{
    /// <summary>
    /// Utility to measure and log execution time of code blocks using the disposable pattern.
    /// </summary>
    public class ExecutionTimer : IDisposable
    {
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public ExecutionTimer(string operationName)
        {
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
            Logger.Instance.Log($"[PERF] Starting: {_operationName}");
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
                Logger.Instance.Log($"[PERF] Completed: {_operationName} | Elapsed: {_stopwatch.Elapsed.TotalSeconds:F2}s");
            }

            _disposed = true;
        }
    }
}
