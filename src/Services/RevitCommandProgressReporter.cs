using System;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RevitCommandProgressReporter : IProgressReporter
    {
        private readonly Action<string> _log;
        private readonly Action<double, string> _progress;

        public RevitCommandProgressReporter(Action<string> log, Action<double, string> progress)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public void Report(string message, double percentage)
        {
            _progress(percentage, message);
        }

        public void Log(string message)
        {
            _log(message);
        }

        public void LogWarning(string message)
        {
            _log(message);
        }

        public void LogError(string message)
        {
            _log(message);
        }
    }
}
