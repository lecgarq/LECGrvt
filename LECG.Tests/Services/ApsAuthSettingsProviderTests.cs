using System.Text.Json;
using FluentAssertions;
using LECG.Batch.Configuration;
using LECG.Batch.Services;

namespace LECG.Tests.Services;

public class ApsAuthSettingsProviderTests
{
    [Fact]
    public void TryGetValidSettings_MissingFile_WritesTemplateAndReturnsConfigurationError()
    {
        string tempDir = CreateTempDirectory();
        string settingsPath = Path.Combine(tempDir, "aps-settings.json");

        ClearEnvironmentOverrides();

        try
        {
            var provider = new ApsAuthSettingsProvider(settingsPath);

            bool isValid = provider.TryGetValidSettings(out ApsAuthSettings settings, out string errorMessage);

            isValid.Should().BeFalse();
            File.Exists(settingsPath).Should().BeTrue();
            settings.ClientId.Should().BeEmpty();
            settings.ClientSecret.Should().BeEmpty();
            settings.RedirectUri.Should().Be("http://localhost:8090/callback");
            settings.Scopes.Should().BeEquivalentTo(["openid", "user-profile:read", "data:read", "data:create", "data:write"]);
            errorMessage.Should().Contain(settingsPath);
        }
        finally
        {
            ClearEnvironmentOverrides();
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryGetValidSettings_ValidFile_ReturnsValidatedSettings()
    {
        string tempDir = CreateTempDirectory();
        string settingsPath = Path.Combine(tempDir, "aps-settings.json");

        ClearEnvironmentOverrides();

        try
        {
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new ApsAuthSettings
            {
                ClientId = "client-123",
                ClientSecret = "secret-123",
                RedirectUri = "http://localhost:8090/callback",
                Scopes = ["data:read", "data:write", "data:read"],
            }));

            var provider = new ApsAuthSettingsProvider(settingsPath);

            bool isValid = provider.TryGetValidSettings(out ApsAuthSettings settings, out string errorMessage);

            isValid.Should().BeTrue();
            errorMessage.Should().BeEmpty();
            settings.ClientId.Should().Be("client-123");
            settings.ClientSecret.Should().Be("secret-123");
            settings.RedirectUri.Should().Be("http://localhost:8090/callback");
            settings.Scopes.Should().Equal("data:read", "data:write");
        }
        finally
        {
            ClearEnvironmentOverrides();
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadSettings_EnvironmentOverridesFileValues()
    {
        string tempDir = CreateTempDirectory();
        string settingsPath = Path.Combine(tempDir, "aps-settings.json");

        ClearEnvironmentOverrides();

        try
        {
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new ApsAuthSettings
            {
                ClientId = "file-client",
                ClientSecret = "file-secret",
                RedirectUri = "http://localhost:8090/callback",
                Scopes = ["data:read"],
            }));

            Environment.SetEnvironmentVariable("LECG_APS_CLIENT_ID", "env-client");
            Environment.SetEnvironmentVariable("LECG_APS_CLIENT_SECRET", "env-secret");
            Environment.SetEnvironmentVariable("LECG_APS_REDIRECT_URI", "http://127.0.0.1:9001/auth");
            Environment.SetEnvironmentVariable("LECG_APS_SCOPES", "openid data:read data:write");

            var provider = new ApsAuthSettingsProvider(settingsPath);
            ApsAuthSettings settings = provider.LoadSettings();

            settings.ClientId.Should().Be("env-client");
            settings.ClientSecret.Should().Be("env-secret");
            settings.RedirectUri.Should().Be("http://127.0.0.1:9001/auth");
            settings.Scopes.Should().Equal("openid", "data:read", "data:write");
        }
        finally
        {
            ClearEnvironmentOverrides();
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lecg-aps-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void ClearEnvironmentOverrides()
    {
        Environment.SetEnvironmentVariable("LECG_APS_CLIENT_ID", null);
        Environment.SetEnvironmentVariable("LECG_APS_CLIENT_SECRET", null);
        Environment.SetEnvironmentVariable("LECG_APS_REDIRECT_URI", null);
        Environment.SetEnvironmentVariable("LECG_APS_SCOPES", null);
    }
}
