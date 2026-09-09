namespace LECG.Services.Interfaces
{
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
}
