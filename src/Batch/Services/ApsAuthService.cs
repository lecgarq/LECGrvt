using System.Diagnostics;
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
        private readonly HttpClient _http;
        private readonly string _clientId;
        private readonly string _redirectUri;
        private readonly string[] _scopes;

        public ApsAuthService(
            IApsSessionStore sessionStore,
            HttpClient http,
            string clientId,
            string redirectUri,
            string[] scopes)
        {
            _sessionStore = sessionStore;
            _http = http;
            _clientId = clientId;
            _redirectUri = redirectUri;
            _scopes = scopes;
        }

        public async Task<ApsSession> SignInAsync(CancellationToken cancellationToken = default)
        {
            var (verifier, challenge) = PkceHelper.Generate();
            string state = Guid.NewGuid().ToString("N");
            string scopeStr = string.Join(" ", _scopes);

            string url = $"{BatchConstants.ApsBaseUrl}/authentication/v2/authorize"
                       + $"?client_id={Uri.EscapeDataString(_clientId)}"
                       + $"&response_type=code"
                       + $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}"
                       + $"&scope={Uri.EscapeDataString(scopeStr)}"
                       + $"&state={state}"
                       + $"&code_challenge={challenge}"
                       + $"&code_challenge_method=S256";

            using var listener = new LoopbackCallbackListener(_redirectUri);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            string code = await listener.WaitForCodeAsync(TimeSpan.FromMinutes(5), cancellationToken);

            ApsSession session = await ExchangeCodeAsync(code, verifier, cancellationToken);
            _sessionStore.Save(session);
            Logger.Instance.Log($"[APS] Signed in. Token expires at {session.ExpiresAt:u}");
            return session;
        }

        public async Task<ApsSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"]    = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"]     = _clientId,
                ["redirect_uri"]  = _redirectUri,
            };

            HttpResponseMessage response = await _http.PostAsync(
                $"{BatchConstants.ApsBaseUrl}/authentication/v2/token",
                new FormUrlEncodedContent(form),
                cancellationToken);

            response.EnsureSuccessStatusCode();
            return await ParseTokenResponseAsync(response, refreshToken, cancellationToken);
        }

        public void SignOut()
        {
            _sessionStore.Delete();
            Logger.Instance.Log("[APS] Signed out.");
        }

        private async Task<ApsSession> ExchangeCodeAsync(string code, string verifier, CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"]    = "authorization_code",
                ["code"]          = code,
                ["redirect_uri"]  = _redirectUri,
                ["code_verifier"] = verifier,
                ["client_id"]     = _clientId,
            };

            HttpResponseMessage response = await _http.PostAsync(
                $"{BatchConstants.ApsBaseUrl}/authentication/v2/token",
                new FormUrlEncodedContent(form),
                ct);

            response.EnsureSuccessStatusCode();
            return await ParseTokenResponseAsync(response, null, ct);
        }

        private static async Task<ApsSession> ParseTokenResponseAsync(
            HttpResponseMessage response, string? fallbackRefreshToken, CancellationToken ct)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            JsonElement doc = JsonDocument.Parse(body).RootElement;

            string accessToken  = doc.GetProperty("access_token").GetString()!;
            string refreshToken = doc.TryGetProperty("refresh_token", out JsonElement rt) ? rt.GetString()! : (fallbackRefreshToken ?? "");
            int    expiresIn    = doc.GetProperty("expires_in").GetInt32();
            string scope        = doc.TryGetProperty("scope", out JsonElement sc) ? sc.GetString()! : "";
            string userId       = doc.TryGetProperty("userId", out JsonElement uid) ? uid.GetString()! : "";

            return ApsSession.FromTokenResponse(accessToken, refreshToken, expiresIn, scope, userId);
        }
    }
}
