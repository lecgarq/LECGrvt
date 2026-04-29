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
    /// A simple implementation of IProgressReporter.
    /// </summary>
    public class SimpleProgressReporter : IProgressReporter
    {
        private readonly System.Action<ProgressReport>? _onReport;

        public SimpleProgressReporter(System.Action<ProgressReport> onReport)
        {
            _onReport = onReport;
        }

        public void Report(string message, double percentage)
        {
            _onReport?.Invoke(new ProgressReport { Message = message, Percentage = percentage });
        }

        public void Log(string message)
        {
            _onReport?.Invoke(new ProgressReport { Message = message });
        }

        public void LogWarning(string message)
        {
            _onReport?.Invoke(new ProgressReport { Message = message });
        }

        public void LogError(string message)
        {
            _onReport?.Invoke(new ProgressReport { Message = message });
        }
    }
}
