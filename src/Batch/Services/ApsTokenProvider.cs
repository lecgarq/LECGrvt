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
        private readonly HttpClient _http;
        private readonly string _clientId;
        private readonly string _redirectUri;

        public ApsTokenProvider(IApsSessionStore sessionStore, HttpClient http, string clientId, string redirectUri)
        {
            _sessionStore = sessionStore;
            _http = http;
            _clientId = clientId;
            _redirectUri = redirectUri;
        }

        public async Task<string> GetValidTokenAsync(CancellationToken cancellationToken = default)
        {
            ApsSession? session = _sessionStore.Load();
            if (session == null)
                throw new InvalidOperationException("No APS session found. Please sign in.");

            if (!session.IsExpired(BatchConstants.TokenRefreshBufferMin))
                return session.AccessToken;

            var form = new Dictionary<string, string>
            {
                ["grant_type"]    = "refresh_token",
                ["refresh_token"] = session.RefreshToken,
                ["client_id"]     = _clientId,
                ["redirect_uri"]  = _redirectUri,
            };

            HttpResponseMessage response = await _http.PostAsync(
                $"{BatchConstants.ApsBaseUrl}/authentication/v2/token",
                new FormUrlEncodedContent(form),
                cancellationToken);

            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
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
