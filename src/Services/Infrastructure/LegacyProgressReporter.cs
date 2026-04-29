using System;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class LegacyProgressReporter : IProgressReporter
    {
        private readonly Action<double, string>? _progressCallback;
        private readonly Action<string>? _logCallback;

        public LegacyProgressReporter(Action<double, string>? progressCallback = null, Action<string>? logCallback = null)
        {
            _progressCallback = progressCallback;
            _logCallback = logCallback;
        }

        public void Report(string message, double percentage)
        {
            _progressCallback?.Invoke(percentage, message);
        }

        public void Log(string message)
        {
            _logCallback?.Invoke(message);
        }

        public void LogWarning(string message)
        {
            _logCallback?.Invoke(message);
        }

        public void LogError(string message)
        {
            _logCallback?.Invoke(message);
        }
    }
}
