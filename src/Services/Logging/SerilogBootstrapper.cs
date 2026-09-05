using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Events;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MsLogger = Microsoft.Extensions.Logging.ILogger;
using MsLoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace LECG.Services.Logging
{
    internal static class SerilogBootstrapper
    {
        public static MsLoggerFactory CreateLoggerFactory(out string? initializationError)
        {
            initializationError = null;

            try
            {
                string logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LECG",
                    "Logs");

                Directory.CreateDirectory(logDirectory);

                Serilog.ILogger logger = new Serilog.LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "LECG")
                    .WriteTo.File(
                        path: Path.Combine(logDirectory, "lecg-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        fileSizeLimitBytes: 5_000_000,
                        rollOnFileSizeLimit: true,
                        shared: true,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                    .CreateLogger();

                return new SerilogBackedLoggerFactory(logger, disposeLogger: true);
            }
            catch (Exception ex) when (IsExpectedLoggingBootstrapException(ex))
            {
                initializationError = ex.Message;
                return NullLoggerFactory.Instance;
            }
        }

        private static bool IsExpectedLoggingBootstrapException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or IOException
                or NotSupportedException
                or UnauthorizedAccessException;
        }

        private sealed class SerilogBackedLoggerFactory : MsLoggerFactory
        {
            private readonly Serilog.ILogger _rootLogger;
            private readonly bool _disposeLogger;

            public SerilogBackedLoggerFactory(Serilog.ILogger rootLogger, bool disposeLogger)
            {
                _rootLogger = rootLogger ?? throw new ArgumentNullException(nameof(rootLogger));
                _disposeLogger = disposeLogger;
            }

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public MsLogger CreateLogger(string categoryName)
            {
                string effectiveCategoryName = string.IsNullOrWhiteSpace(categoryName) ? "LECG" : categoryName;
                return new SerilogBackedLogger(_rootLogger.ForContext("SourceContext", effectiveCategoryName));
            }

            public void Dispose()
            {
                if (_disposeLogger && _rootLogger is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private sealed class SerilogBackedLogger : MsLogger
        {
            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();

                public void Dispose()
                {
                }
            }

            private readonly Serilog.ILogger _logger;

            public SerilogBackedLogger(Serilog.ILogger logger)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(MsLogLevel logLevel)
            {
                LogEventLevel? level = ToSerilogLevel(logLevel);
                return level.HasValue && _logger.IsEnabled(level.Value);
            }

            public void Log<TState>(
                MsLogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                LogEventLevel? level = ToSerilogLevel(logLevel);
                if (!level.HasValue || !IsEnabled(logLevel))
                {
                    return;
                }

                string message = formatter != null
                    ? formatter(state, exception)
                    : state is null
                        ? string.Empty
                        : state.ToString() ?? string.Empty;

                Serilog.ILogger logger = _logger;
                if (eventId.Id != 0)
                {
                    logger = logger.ForContext("EventId", eventId.Id);
                }

                if (!string.IsNullOrWhiteSpace(eventId.Name))
                {
                    logger = logger.ForContext("EventName", eventId.Name);
                }

                logger.Write(level.Value, exception, "{Message}", message);
            }

            private static LogEventLevel? ToSerilogLevel(MsLogLevel logLevel)
            {
                return logLevel switch
                {
                    MsLogLevel.Trace => LogEventLevel.Verbose,
                    MsLogLevel.Debug => LogEventLevel.Debug,
                    MsLogLevel.Information => LogEventLevel.Information,
                    MsLogLevel.Warning => LogEventLevel.Warning,
                    MsLogLevel.Error => LogEventLevel.Error,
                    MsLogLevel.Critical => LogEventLevel.Fatal,
                    _ => null
                };
            }
        }
    }
}
