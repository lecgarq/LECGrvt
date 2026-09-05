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

        /// <summary>Short exception message shown on first expand.</summary>
        public string? Detail { get; }

        /// <summary>Full stack trace shown on second expand.</summary>
        public string? StackTrace { get; }

        /// <summary>True when this entry has expandable detail.</summary>
        public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

        public LogEntry(string message, LogLevel level = LogLevel.Info, string? detail = null, string? stackTrace = null)
        {
            Timestamp = DateTime.Now;
            Message = message;
            Level = level;
            Detail = detail;
            StackTrace = stackTrace;
        }

        public string FormattedTime => Timestamp.ToString("HH:mm:ss");

        public string ToCopyText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{FormattedTime}] [{Level}] {Message}");
            if (!string.IsNullOrWhiteSpace(Detail))
                sb.AppendLine(Detail);
            if (!string.IsNullOrWhiteSpace(StackTrace))
                sb.AppendLine(StackTrace);
            return sb.ToString();
        }
    }
}
