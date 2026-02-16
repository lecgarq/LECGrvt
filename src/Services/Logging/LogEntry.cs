using System;
using System.Windows.Media;

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

        public LogEntry(string message, LogLevel level = LogLevel.Info)
        {
            Timestamp = DateTime.Now;
            Message = message;
            Level = level;
        }

        public string FormattedTime => Timestamp.ToString("HH:mm:ss");
    }
}
