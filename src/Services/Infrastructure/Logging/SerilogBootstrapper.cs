using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace LECG.Services.Logging
{
    internal static class SerilogBootstrapper
    {
        public static ILoggerFactory CreateLoggerFactory(out string? initializationError)
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

                return new SerilogLoggerFactory(logger, dispose: true);
            }
            catch (Exception ex) when (IsExpectedLoggingBootstrapException(ex))
            {
                initializationError = ex.Message;

                return LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
                });
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
    }
}
