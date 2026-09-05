using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using LECG.Batch.Configuration;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class ApsAuthService : IApsAuthService
    {
        private readonly IApsSessionStore _sessionStore;
        private readonly IApsAuthSettingsProvider _settingsProvider;
        private readonly HttpClient _http;

        public ApsAuthService(
            IApsSessionStore sessionStore,
            HttpClient http,
            IApsAuthSettingsProvider settingsProvider)
        {
            _sessionStore = sessionStore;
            _http = http;
            _settingsProvider = settingsProvider;
        }

        public async Task<ApsSession> SignInAsync(CancellationToken cancellationToken = default)
        {
            ApsAuthSettings settings = GetValidatedSettings();
            var (verifier, challenge) = PkceHelper.Generate();
            string state = Guid.NewGuid().ToString("N");
            string scopeStr = string.Join(" ", settings.Scopes);

            string url = $"{BatchConstants.ApsBaseUrl}/authentication/v2/authorize"
                       + $"?client_id={Uri.EscapeDataString(settings.ClientId)}"
                       + $"&response_type=code"
                       + $"&redirect_uri={Uri.EscapeDataString(settings.RedirectUri)}"
                       + $"&scope={Uri.EscapeDataString(scopeStr)}"
                       + $"&state={state}"
                       + $"&code_challenge={challenge}"
                       + $"&code_challenge_method=S256";

            try
            {
                using var listener = new LoopbackCallbackListener(settings.RedirectUri);
                Process? browserProcess = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                if (browserProcess == null)
                    throw new InvalidOperationException("APS sign-in could not launch your system browser.");

                string code = await listener.WaitForCodeAsync(TimeSpan.FromMinutes(5), cancellationToken);

                ApsSession session = await ExchangeCodeAsync(code, verifier, settings, cancellationToken);
                _sessionStore.Save(session);
                Logger.Instance.Log($"[APS] Signed in. Token expires at {session.ExpiresAt:u}");
                return session;
            }
            catch (ApsAuthConfigurationException)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                Logger.Instance.LogWarning($"[APS] Sign-in timed out waiting for callback: {ex.Message}");
                throw new InvalidOperationException("APS sign-in timed out waiting for the browser callback.", ex);
            }
            catch (HttpListenerException ex)
            {
                Logger.Instance.LogWarning($"[APS] Sign-in callback listener failed for {settings.RedirectUri}: {ex.Message}");
                throw new InvalidOperationException($"APS sign-in could not start the local callback listener for {settings.RedirectUri}. Check the redirect URI and that the port is available.", ex);
            }
            catch (Win32Exception ex)
            {
                Logger.Instance.LogWarning($"[APS] Sign-in browser launch failed: {ex.Message}");
                throw new InvalidOperationException("APS sign-in could not launch your system browser.", ex);
            }
        }

        public async Task<ApsSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            ApsAuthSettings settings = GetValidatedSettings();
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = settings.ClientId,
                ["redirect_uri"] = settings.RedirectUri,
            };
            AddClientSecretIfConfigured(form, settings);

            HttpResponseMessage response = await _http.PostAsync(
                $"{BatchConstants.ApsBaseUrl}/authentication/v2/token",
                new FormUrlEncodedContent(form),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw await CreateAuthFailureAsync("Token refresh", response, cancellationToken);

            return await ParseTokenResponseAsync(response, refreshToken, cancellationToken);
        }

        public void SignOut()
        {
            _sessionStore.Delete();
            Logger.Instance.Log("[APS] Signed out.");
        }

        private async Task<ApsSession> ExchangeCodeAsync(string code, string verifier, ApsAuthSettings settings, CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = settings.RedirectUri,
                ["code_verifier"] = verifier,
                ["client_id"] = settings.ClientId,
            };
            AddClientSecretIfConfigured(form, settings);

            HttpResponseMessage response = await _http.PostAsync(
                $"{BatchConstants.ApsBaseUrl}/authentication/v2/token",
                new FormUrlEncodedContent(form),
                ct);

            if (!response.IsSuccessStatusCode)
                throw await CreateAuthFailureAsync("Token exchange", response, ct);

            return await ParseTokenResponseAsync(response, null, ct);
        }

        private ApsAuthSettings GetValidatedSettings()
        {
            if (_settingsProvider.TryGetValidSettings(out ApsAuthSettings settings, out string errorMessage))
                return settings;

            Logger.Instance.LogWarning($"[APS] {errorMessage}");
            throw new ApsAuthConfigurationException(errorMessage);
        }

        private async Task<Exception> CreateAuthFailureAsync(string stage, HttpResponseMessage response, CancellationToken ct)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            Logger.Instance.LogWarning($"[APS] {stage} failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new InvalidOperationException(
                    $"APS {stage.ToLowerInvariant()} failed with 401 Unauthorized. Verify the ClientId and RedirectUri in {_settingsProvider.SettingsPath}.");
            }

            return new InvalidOperationException(
                $"APS {stage.ToLowerInvariant()} failed ({(int)response.StatusCode} {response.ReasonPhrase}).");
        }

        private static async Task<ApsSession> ParseTokenResponseAsync(
            HttpResponseMessage response, string? fallbackRefreshToken, CancellationToken ct)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            JsonElement doc = JsonDocument.Parse(body).RootElement;

            string accessToken = doc.GetProperty("access_token").GetString()!;
            string refreshToken = doc.TryGetProperty("refresh_token", out JsonElement rt)
                ? rt.GetString()!
                : (fallbackRefreshToken ?? "");
            int expiresIn = doc.GetProperty("expires_in").GetInt32();
            string scope = doc.TryGetProperty("scope", out JsonElement sc) ? sc.GetString()! : "";
            string userId = doc.TryGetProperty("userId", out JsonElement uid) ? uid.GetString()! : "";

            return ApsSession.FromTokenResponse(accessToken, refreshToken, expiresIn, scope, userId);
        }

        private static void AddClientSecretIfConfigured(Dictionary<string, string> form, ApsAuthSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
                form["client_secret"] = settings.ClientSecret;
        }
    }
}
