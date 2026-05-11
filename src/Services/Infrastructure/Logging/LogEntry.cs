using System;

namespace LECG.Services.Logging
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; }
        public LogLevel Level { get; }
        public string Message { get; }
        public string? Scope { get; }

        public LogEntry(string message, LogLevel level = LogLevel.Info, string? scope = null)
        {
            Timestamp = DateTime.Now;
            Message = message;
            Level = level;
            Scope = scope;
        }

        public string FormattedTime => Timestamp.ToString("HH:mm:ss");
    }
}
