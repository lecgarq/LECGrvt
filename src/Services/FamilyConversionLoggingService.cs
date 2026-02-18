using System.IO;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyConversionLoggingService : IFamilyConversionLoggingService
    {
        public void LogStart(string sourceFamilyName, string targetFamilyName, string templatePath, bool isTemporary)
        {
            Logger.Instance.Log($"Converting Family: {sourceFamilyName} -> {targetFamilyName}");
            Logger.Instance.Log($"Template: {Path.GetFileName(templatePath)}");
            Logger.Instance.Log($"Temporary Mode: {isTemporary}");
        }

        public void LogCriticalError(string message, string stackTrace)
        {
            Logger.Instance.Log($"Critical Error: {message}");
            Logger.Instance.Log(stackTrace);
        }
    }
}
