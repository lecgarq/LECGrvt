using System;
using System.IO;
using System.Text.Json;
using LECG.Core;
using LECG.Services.Logging;
using LECG.Validation;

namespace LECG.Services
{
    /// <summary>
    /// Generic service to save and load application settings to JSON files.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LECG",
            "Settings");

        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        static SettingsManager()
        {
            // Ensure settings directory exists
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
            }
        }

        /// <summary>
        /// Save a settings object to a JSON file.
        /// </summary>
        public static void Save<T>(T settings, string fileName)
        {
            try
            {
                if (!TryValidateSettings(settings, fileName))
                {
                    return;
                }

                string json = JsonSerializer.Serialize(settings, _options);
                string filePath = Path.Combine(_appDataPath, fileName);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Silently fail if settings can't be saved (not critical)
            }
        }

        /// <summary>
        /// Load a settings object from a JSON file.
        /// </summary>
        public static T Load<T>(string fileName) where T : new()
        {
            try
            {
                string filePath = Path.Combine(_appDataPath, fileName);
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    T? result = JsonSerializer.Deserialize<T>(json);
                    if (result != null && TryValidateSettings(result, fileName))
                    {
                        return result;
                    }
                }
            }
            catch
            {
                // Return default if load fails
            }
            return new T();
        }

        private static bool TryValidateSettings<T>(T settings, string fileName)
        {
            if (ValidationRules.TryValidate(settings!, out string message))
            {
                return true;
            }

            ServiceLocator.GetService<ILogger>()?.LogWarning($"Invalid settings in {fileName}: {message}", scope: nameof(SettingsManager));
            return false;
        }
    }
}
