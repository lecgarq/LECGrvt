namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionLoggingService
    {
        void LogStart(string sourceFamilyName, string targetFamilyName, string templatePath, bool isTemporary);
        void LogCriticalError(string message, string stackTrace);
    }
}
