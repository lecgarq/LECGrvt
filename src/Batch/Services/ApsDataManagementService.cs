using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LECG.Batch.Configuration;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class ApsDataManagementService : IApsDataManagementService
    {
        private readonly IApsTokenProvider _tokenProvider;
        private readonly HttpClient _http;

        public ApsDataManagementService(IApsTokenProvider tokenProvider, HttpClient http)
        {
            _tokenProvider = tokenProvider;
            _http = http;
        }

        public async Task<IReadOnlyList<ApsHub>> GetHubsAsync(CancellationToken cancellationToken = default)
        {
            string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
            using var request = BuildGet($"{BatchConstants.ApsBaseUrl}/project/v1/hubs", token);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            JsonElement root = await ParseRootAsync(response, cancellationToken);
            return root.GetProperty("data").EnumerateArray()
                .Select(e => new ApsHub
                {
                    Id     = e.GetProperty("id").GetString() ?? "",
                    Name   = e.GetProperty("attributes").GetProperty("name").GetString() ?? "",
                    Region = e.GetProperty("attributes").TryGetProperty("region", out JsonElement rg) ? rg.GetString() ?? "US" : "US",
                })
                .ToList();
        }

        public async Task<IReadOnlyList<ApsProject>> GetProjectsAsync(string hubId, CancellationToken cancellationToken = default)
        {
            string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
            using var request = BuildGet($"{BatchConstants.ApsBaseUrl}/project/v1/hubs/{hubId}/projects", token);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            JsonElement root = await ParseRootAsync(response, cancellationToken);
            return root.GetProperty("data").EnumerateArray()
                .Select(e => new ApsProject
                {
                    Id    = e.GetProperty("id").GetString() ?? "",
                    Name  = e.GetProperty("attributes").GetProperty("name").GetString() ?? "",
                    HubId = hubId,
                })
                .ToList();
        }

        public async Task<IReadOnlyList<ApsFolderItem>> GetTopFoldersAsync(string hubId, string projectId, CancellationToken cancellationToken = default)
        {
            string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
            string url = $"{BatchConstants.ApsBaseUrl}/project/v1/hubs/{hubId}/projects/{projectId}/topFolders";
            using var request = BuildGet(url, token);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            JsonElement root = await ParseRootAsync(response, cancellationToken);
            return root.GetProperty("data").EnumerateArray()
                .Select(e => new ApsFolderItem
                {
                    Id        = e.GetProperty("id").GetString() ?? "",
                    Name      = e.GetProperty("attributes").GetProperty("name").GetString() ?? "",
                    ItemType  = FolderItemType.Folder,
                    ProjectId = projectId,
                })
                .ToList();
        }

        public async Task<IReadOnlyList<ApsFolderItem>> GetFolderContentsAsync(string projectId, string folderId, CancellationToken cancellationToken = default)
        {
            string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
            string url = $"{BatchConstants.ApsBaseUrl}/data/v1/projects/{projectId}/folders/{folderId}/contents";
            using var request = BuildGet(url, token);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            JsonElement root = await ParseRootAsync(response, cancellationToken);
            return root.GetProperty("data").EnumerateArray()
                .Select(e =>
                {
                    string type = e.GetProperty("type").GetString() ?? "";
                    bool isFolder = type == "folders";
                    JsonElement attrs = e.GetProperty("attributes");

                    return new ApsFolderItem
                    {
                        Id           = e.GetProperty("id").GetString() ?? "",
                        Name         = attrs.GetProperty("name").GetString() ?? "",
                        ItemType     = isFolder ? FolderItemType.Folder : FolderItemType.Item,
                        ProjectId    = projectId,
                        IsRevitModel = !isFolder && IsRvtItem(attrs),
                    };
                })
                .ToList();
        }

        public async Task<IReadOnlyList<ApsVersion>> GetVersionsAsync(string projectId, string itemId, CancellationToken cancellationToken = default)
        {
            string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
            string url = $"{BatchConstants.ApsBaseUrl}/data/v1/projects/{projectId}/items/{itemId}/versions";
            using var request = BuildGet(url, token);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            JsonElement root = await ParseRootAsync(response, cancellationToken);
            return root.GetProperty("data").EnumerateArray()
                .Select(e => ParseVersion(e, projectId, itemId))
                .ToList();
        }

        public async Task<ApsVersion?> GetVersionAsync(string projectId, string versionId, CancellationToken cancellationToken = default)
        {
            string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
            string encodedId = Uri.EscapeDataString(versionId);
            string url = $"{BatchConstants.ApsBaseUrl}/data/v1/projects/{projectId}/versions/{encodedId}";
            using var request = BuildGet(url, token);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            JsonElement root = await ParseRootAsync(response, cancellationToken);
            return ParseVersion(root.GetProperty("data"), projectId, null);
        }

        public bool IsRevitCloudModel(ApsVersion version)
        {
            if (!string.Equals(version.FileType, "rvt", StringComparison.OrdinalIgnoreCase))
                return false;

            return version.ExtensionType is "versions:autodesk.bim360:C4RModel"
                                          or "versions:autodesk.core:File";
        }

        private static ApsVersion ParseVersion(JsonElement e, string projectId, string? itemId)
        {
            JsonElement attrs = e.GetProperty("attributes");
            JsonElement ext = attrs.TryGetProperty("extension", out JsonElement extEl) ? extEl : default;
            JsonElement extData = ext.ValueKind != JsonValueKind.Undefined
                && ext.TryGetProperty("data", out JsonElement extDataEl) ? extDataEl : default;

            string fileType = attrs.TryGetProperty("fileType", out JsonElement ft) ? ft.GetString() ?? "" : "";
            string extType  = ext.ValueKind != JsonValueKind.Undefined
                && ext.TryGetProperty("type", out JsonElement et) ? et.GetString() ?? "" : "";
            bool isWorkshared = string.Equals(extType, "versions:autodesk.bim360:C4RModel", StringComparison.Ordinal);

            // Project GUID: strip "b." prefix from projectId
            string rawProjectId = e.TryGetProperty("relationships", out JsonElement rels)
                && rels.TryGetProperty("target", out JsonElement target)
                && target.TryGetProperty("data", out JsonElement targetData)
                && targetData.TryGetProperty("id", out JsonElement tid)
                ? tid.GetString() ?? projectId : projectId;
            string projectGuid = rawProjectId.StartsWith("b.", StringComparison.Ordinal)
                ? rawProjectId[2..] : rawProjectId;

            string? modelGuid = extData.ValueKind != JsonValueKind.Undefined
                && extData.TryGetProperty("revisionId", out JsonElement revId)
                ? revId.GetString() : null;

            string? region = extData.ValueKind != JsonValueKind.Undefined
                && extData.TryGetProperty("region", out JsonElement regEl)
                ? regEl.GetString() : "US";

            return new ApsVersion
            {
                Id            = e.GetProperty("id").GetString() ?? "",
                ItemId        = itemId ?? (e.TryGetProperty("relationships", out JsonElement r2)
                    && r2.TryGetProperty("item", out JsonElement itemRel)
                    && itemRel.TryGetProperty("data", out JsonElement itemData)
                    && itemData.TryGetProperty("id", out JsonElement iid)
                    ? iid.GetString() ?? "" : ""),
                ProjectId     = projectId,
                VersionNumber = attrs.TryGetProperty("versionNumber", out JsonElement vn) ? vn.GetInt32() : 0,
                DisplayName   = attrs.TryGetProperty("displayName", out JsonElement dn) ? dn.GetString() ?? "" : "",
                FileName      = attrs.TryGetProperty("name", out JsonElement nm) ? nm.GetString() ?? "" : "",
                FileType      = fileType,
                ExtensionType = extType,
                ModelGuid     = modelGuid,
                Region        = region,
                ProjectGuid   = projectGuid,
                IsWorkshared  = isWorkshared,
            };
        }

        private static bool IsRvtItem(JsonElement attrs) =>
            attrs.TryGetProperty("fileType", out JsonElement ft) &&
            string.Equals(ft.GetString(), "rvt", StringComparison.OrdinalIgnoreCase);

        private static HttpRequestMessage BuildGet(string url, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        private static async Task<JsonElement> ParseRootAsync(HttpResponseMessage response, CancellationToken ct)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(body).RootElement;
        }
    }
}
