using LECG.Batch.Configuration;

namespace LECG.Batch.Services.Interfaces
{
    public interface IApsAuthSettingsProvider
    {
        string SettingsPath { get; }

        ApsAuthSettings LoadSettings();
        bool TryGetValidSettings(out ApsAuthSettings settings, out string errorMessage);
    }
}
