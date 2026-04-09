using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LECG.Batch.Configuration;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class ApsPublishService : IApsPublishService
    {
        private readonly IApsTokenProvider _tokenProvider;
        private readonly HttpClient _http;

        public ApsPublishService(IApsTokenProvider tokenProvider, HttpClient http)
        {
            _tokenProvider = tokenProvider;
            _http = http;
        }

        public async Task PublishAsync(BatchJob job, CancellationToken cancellationToken = default)
        {
            string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);

            // URN = base64url-encoded versionId
            string urn = Convert.ToBase64String(Encoding.UTF8.GetBytes(job.VersionId))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            string region = string.Equals(job.Region, "EMEA", StringComparison.OrdinalIgnoreCase)
                ? "EU" : "US";

            string url = $"{BatchConstants.ApsBaseUrl}/modelderivative/v2/regions/{region}/designdata/{urn}/publish";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            Logger.Instance.Log($"[Batch] Publishing version: {job.DisplayName}");
            HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"APS publish failed ({(int)response.StatusCode}): {body}");
            }

            Logger.Instance.Log($"[Batch] Publish triggered: {job.DisplayName}");
        }
    }
}
