using System.IO;
using System.Text.Json;
using LECG.Batch.Configuration;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public sealed class ApsAuthSettingsProvider : IApsAuthSettingsProvider
    {
        private const string ClientIdEnvVar = "LECG_APS_CLIENT_ID";
        private const string ClientSecretEnvVar = "LECG_APS_CLIENT_SECRET";
        private const string RedirectUriEnvVar = "LECG_APS_REDIRECT_URI";
        private const string ScopesEnvVar = "LECG_APS_SCOPES";
        private const string DefaultRedirectUri = "http://localhost:8090/callback";

        private static readonly string[] s_defaultScopes =
        [
            "openid",
            "user-profile:read",
            "data:read",
            "data:create",
            "data:write",
        ];

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
        };

        public string SettingsPath { get; }

        public ApsAuthSettingsProvider(string? settingsPath = null)
        {
            SettingsPath = settingsPath ?? BatchConstants.ApsSettingsPath;
        }

        public ApsAuthSettings LoadSettings()
        {
            EnsureSettingsDirectory();

            ApsAuthSettings settings = LoadFileSettings();
            return MergeEnvironmentOverrides(settings);
        }

        public bool TryGetValidSettings(out ApsAuthSettings settings, out string errorMessage)
        {
            settings = LoadSettings();

            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                errorMessage = $"APS auth is not configured. Update {SettingsPath} with your Autodesk APS ClientId and registered RedirectUri.";
                return false;
            }

            if (!Uri.TryCreate(settings.RedirectUri, UriKind.Absolute, out Uri? redirectUri)
                || !string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || !redirectUri.IsLoopback
                || string.IsNullOrWhiteSpace(redirectUri.AbsolutePath.Trim('/')))
            {
                errorMessage = $"APS RedirectUri must be a loopback HTTP URL such as {DefaultRedirectUri}. Current value: {settings.RedirectUri}";
                return false;
            }

            if (NormalizeScopes(settings.Scopes).Length == 0)
            {
                errorMessage = $"APS auth scopes are missing. Update {SettingsPath} with at least one scope.";
                return false;
            }

            errorMessage = string.Empty;
            settings.Scopes = NormalizeScopes(settings.Scopes);
            return true;
        }

        private ApsAuthSettings LoadFileSettings()
        {
            if (!File.Exists(SettingsPath))
            {
                ApsAuthSettings template = CreateTemplate();
                PersistTemplate(template);
                return template;
            }

            try
            {
                string json = File.ReadAllText(SettingsPath);
                ApsAuthSettings? settings = JsonSerializer.Deserialize<ApsAuthSettings>(json, s_jsonOptions);
                return settings ?? CreateTemplate();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"[APS] Failed to read auth settings from {SettingsPath}: {ex.Message}");
                return CreateTemplate();
            }
        }

        private ApsAuthSettings MergeEnvironmentOverrides(ApsAuthSettings settings)
        {
            string clientId = FirstNonEmpty(
                Environment.GetEnvironmentVariable(ClientIdEnvVar),
                settings.ClientId);

            string clientSecret = FirstNonEmpty(
                Environment.GetEnvironmentVariable(ClientSecretEnvVar),
                settings.ClientSecret);

            string redirectUri = FirstNonEmpty(
                Environment.GetEnvironmentVariable(RedirectUriEnvVar),
                settings.RedirectUri,
                DefaultRedirectUri);

            string[] scopes = NormalizeScopes(ParseScopes(Environment.GetEnvironmentVariable(ScopesEnvVar)));
            if (scopes.Length == 0)
                scopes = NormalizeScopes(settings.Scopes);
            if (scopes.Length == 0)
                scopes = s_defaultScopes;

            return new ApsAuthSettings
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                RedirectUri = redirectUri,
                Scopes = scopes,
            };
        }

        private void EnsureSettingsDirectory()
        {
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }

        private void PersistTemplate(ApsAuthSettings template)
        {
            try
            {
                string json = JsonSerializer.Serialize(template, s_jsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"[APS] Failed to write auth settings template to {SettingsPath}: {ex.Message}");
            }
        }

        private static ApsAuthSettings CreateTemplate() =>
            new()
            {
                ClientId = string.Empty,
                ClientSecret = string.Empty,
                RedirectUri = DefaultRedirectUri,
                Scopes = s_defaultScopes,
            };

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        private static string[] ParseScopes(string? scopes)
        {
            if (string.IsNullOrWhiteSpace(scopes))
                return [];

            return scopes
                .Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string[] NormalizeScopes(IEnumerable<string>? scopes)
        {
            if (scopes == null)
                return [];

            return scopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
