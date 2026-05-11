using System;
using System.IO;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyConversionLoggingService : IFamilyConversionLoggingService
    {
        private readonly ILogger _logger;

        public FamilyConversionLoggingService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void LogStart(string sourceFamilyName, string targetFamilyName, string templatePath, bool isTemporary)
        {
            _logger.Log($"Converting Family: {sourceFamilyName} -> {targetFamilyName}", scope: "FamilyConversion");
            _logger.Log($"Template: {Path.GetFileName(templatePath)}", scope: "FamilyConversion");
            _logger.Log($"Temporary Mode: {isTemporary}", scope: "FamilyConversion");
        }

        public void LogWarning(string message)
        {
            _logger.LogWarning($"Warning: {message}", scope: "FamilyConversion");
        }

        public void LogCriticalError(string message, string stackTrace)
        {
            _logger.LogError($"Critical Error: {message}", scope: "FamilyConversion");
            _logger.Log(stackTrace, scope: "FamilyConversion");
        }
    }
}
