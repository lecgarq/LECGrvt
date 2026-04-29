namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionLoggingService
    {
        void LogStart(string sourceFamilyName, string targetFamilyName, string templatePath, bool isTemporary);
        void LogWarning(string message);
        void LogCriticalError(string message, string stackTrace);
    }
}
