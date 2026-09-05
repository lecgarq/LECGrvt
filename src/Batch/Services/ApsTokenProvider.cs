using System.Net;
using System.Net.Http;
using System.Text.Json;
using LECG.Batch.Configuration;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class ApsTokenProvider : IApsTokenProvider
    {
        private readonly IApsSessionStore _sessionStore;
        private readonly IApsAuthSettingsProvider _settingsProvider;
        private readonly HttpClient _http;

        public ApsTokenProvider(IApsSessionStore sessionStore, HttpClient http, IApsAuthSettingsProvider settingsProvider)
        {
            _sessionStore = sessionStore;
            _http = http;
            _settingsProvider = settingsProvider;
        }

        public async Task<string> GetValidTokenAsync(CancellationToken cancellationToken = default)
        {
            ApsSession? session = _sessionStore.Load();
            if (session == null)
                throw new InvalidOperationException("No APS session found. Please sign in.");

            if (!session.IsExpired(BatchConstants.TokenRefreshBufferMin))
                return session.AccessToken;

            if (!_settingsProvider.TryGetValidSettings(out ApsAuthSettings settings, out string errorMessage))
                throw new ApsAuthConfigurationException(errorMessage);

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = session.RefreshToken,
                ["client_id"] = settings.ClientId,
                ["redirect_uri"] = settings.RedirectUri,
            };
            if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
                form["client_secret"] = settings.ClientSecret;

            HttpResponseMessage response = await _http.PostAsync(
                $"{BatchConstants.ApsBaseUrl}/authentication/v2/token",
                new FormUrlEncodedContent(form),
                cancellationToken);

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Instance.LogWarning($"[APS] Token refresh failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    _sessionStore.Delete();

                throw new InvalidOperationException(
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? "APS session refresh failed with 401 Unauthorized. Please sign in again."
                        : $"APS session refresh failed ({(int)response.StatusCode} {response.ReasonPhrase}).");
            }

            JsonElement doc = JsonDocument.Parse(body).RootElement;
            ApsSession refreshed = ApsSession.FromTokenResponse(
                doc.GetProperty("access_token").GetString()!,
                doc.TryGetProperty("refresh_token", out JsonElement rt) ? rt.GetString()! : session.RefreshToken,
                doc.GetProperty("expires_in").GetInt32(),
                doc.TryGetProperty("scope", out JsonElement sc) ? sc.GetString()! : session.Scope,
                session.UserId);

            _sessionStore.Save(refreshed);
            Logger.Instance.Log($"[APS] Token refreshed, expires at {refreshed.ExpiresAt:u}");
            return refreshed.AccessToken;
        }
    }
}
