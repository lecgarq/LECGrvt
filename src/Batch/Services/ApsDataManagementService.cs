using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
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
        private readonly ConcurrentDictionary<string, IReadOnlyList<ApsFolderItem>> _topFolderCache = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, IReadOnlyList<ApsVersion>> _projectSearchCache = new(StringComparer.Ordinal);
        // Folder name + parent cache shared across searches: key = "{projectId}|{folderId}"
        private readonly ConcurrentDictionary<string, (string Name, string ParentId)> _folderInfoCache = new(StringComparer.Ordinal);

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

        public async Task SearchRevitModelsInProjectAsync(
            string hubId,
            string projectId,
            Action<IReadOnlyList<ApsVersion>> onModelsFound,
            string? searchText = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(hubId);
            ArgumentNullException.ThrowIfNull(projectId);
            ArgumentNullException.ThrowIfNull(onModelsFound);

            string normalizedSearch = searchText?.Trim() ?? "";
            IReadOnlyList<ApsFolderItem> topFolders = await GetCachedTopFoldersAsync(hubId, projectId, cancellationToken);
            string projectGuid = projectId.StartsWith("b.", StringComparison.Ordinal) ? projectId[2..] : projectId;

            var results = new ConcurrentDictionary<string, ApsVersion>(StringComparer.Ordinal);

            // Bounded concurrency search (limit 3)
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = 3,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(topFolders, options, async (folder, ct) =>
            {
                // Per-folder timeout allowed to be up to 60s for large projects
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(60));

                try
                {
                    var folderResults = new ConcurrentDictionary<string, ApsVersion>(StringComparer.Ordinal);
                    await SearchFolderPassAsync(hubId, projectId, folder.Id, folder.Name, projectGuid, normalizedSearch, folderResults, filterByFileType: true, cts.Token);

                    if (!folderResults.IsEmpty)
                    {
                        var newModels = new List<ApsVersion>();
                        foreach (var kvp in folderResults)
                        {
                            if (results.TryAdd(kvp.Key, kvp.Value))
                            {
                                newModels.Add(kvp.Value);
                            }
                        }

                        if (newModels.Count > 0)
                        {
                            onModelsFound?.Invoke(newModels);
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    Logger.Instance.LogWarning($"[APS] Search timed out after 60s for folder: {folder.Name} ({folder.Id})");
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning($"[APS] Search failed for folder {folder.Name}: {ex.Message}");
                }
            });
        }

        private async Task<IReadOnlyList<ApsFolderItem>> GetCachedTopFoldersAsync(string hubId, string projectId, CancellationToken cancellationToken)
        {
            string cacheKey = $"{hubId}|{projectId}";
            if (_topFolderCache.TryGetValue(cacheKey, out IReadOnlyList<ApsFolderItem>? cachedFolders))
                return cachedFolders;

            IReadOnlyList<ApsFolderItem> topFolders = await GetTopFoldersAsync(hubId, projectId, cancellationToken);
            _topFolderCache[cacheKey] = topFolders;
            return topFolders;
        }

        private async Task SearchFolderPassAsync(
            string hubId,
            string projectId,
            string folderId,
            string topFolderName,
            string projectGuid,
            string searchText,
            ConcurrentDictionary<string, ApsVersion> results,
            bool filterByFileType,
            CancellationToken cancellationToken)
        {
            string? nextUrl = BuildFolderSearchUrl(projectId, folderId, searchText, filterByFileType);

            // Accumulated folder map across all pages: id → (name, parentId)
            // Seeded with the known root so path walks always terminate.
            var folderCache = new Dictionary<string, (string Name, string ParentId)>(StringComparer.Ordinal);
            folderCache[folderId] = (topFolderName, "");

            // Items are collected here during paging; paths are resolved afterwards
            // so later pages can contribute folder names used by earlier items.
            var pending = new List<(string ItemId, ApsVersion Version, string ParentFolderId)>();

            while (nextUrl is not null)
            {
                string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
                using var request = BuildGet(nextUrl, token);
                using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    break;

                JsonElement root = await ParseRootAsync(response, cancellationToken);

                // Single pass over included: index everything AND extract folder metadata.
                var includedById = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                if (root.TryGetProperty("included", out JsonElement included))
                {
                    foreach (JsonElement inc in included.EnumerateArray())
                    {
                        if (!inc.TryGetProperty("id", out JsonElement incIdEl) || incIdEl.GetString() is not string incId)
                            continue;

                        includedById[incId] = inc;

                        // Capture folder names so we can walk the full path hierarchy.
                        string incType = inc.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() ?? "" : "";
                        if (incType.Contains("folder", StringComparison.OrdinalIgnoreCase))
                        {
                            string fname = inc.TryGetProperty("attributes", out JsonElement fa)
                                && fa.TryGetProperty("name", out JsonElement fn)
                                ? fn.GetString() ?? "" : "";

                            string fparent = inc.TryGetProperty("relationships", out JsonElement fr)
                                && fr.TryGetProperty("parent", out JsonElement fp)
                                && fp.TryGetProperty("data", out JsonElement fpd)
                                && fpd.TryGetProperty("id", out JsonElement fpid)
                                ? fpid.GetString() ?? "" : "";

                            if (!string.IsNullOrEmpty(fname) && !folderCache.ContainsKey(incId))
                                folderCache[incId] = (fname, fparent);
                        }
                    }
                }

                if (root.TryGetProperty("data", out JsonElement data))
                {
                    foreach (JsonElement item in data.EnumerateArray())
                    {
                        string itemId = item.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(itemId) || results.ContainsKey(itemId))
                            continue;

                        JsonElement itemAttrs = item.TryGetProperty("attributes", out JsonElement ia) ? ia : default;
                        string rawName = itemAttrs.ValueKind != JsonValueKind.Undefined
                            && itemAttrs.TryGetProperty("displayName", out JsonElement idn) ? idn.GetString() ?? ""
                            : itemAttrs.ValueKind != JsonValueKind.Undefined
                              && itemAttrs.TryGetProperty("name", out JsonElement inm) ? inm.GetString() ?? "" : "";

                        if (!rawName.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!MatchesSearchText(rawName, searchText))
                            continue;

                        if (!item.TryGetProperty("relationships", out JsonElement rels))
                            continue;

                        string tipVersionId = "";
                        if (rels.TryGetProperty("tip", out JsonElement tip)
                            && tip.TryGetProperty("data", out JsonElement tipData)
                            && tipData.TryGetProperty("id", out JsonElement tvId))
                        {
                            tipVersionId = tvId.GetString() ?? "";
                        }

                        JsonElement version = default;
                        bool hasVersion = !string.IsNullOrEmpty(tipVersionId) && includedById.TryGetValue(tipVersionId, out version);
                        JsonElement attrs = hasVersion
                            ? (version.TryGetProperty("attributes", out JsonElement va) ? va : default)
                            : itemAttrs;

                        if (attrs.ValueKind == JsonValueKind.Undefined)
                            continue;

                        JsonElement ext = attrs.TryGetProperty("extension", out JsonElement extEl) ? extEl : default;
                        JsonElement extData = ext.ValueKind != JsonValueKind.Undefined
                            && ext.TryGetProperty("data", out JsonElement extDataEl) ? extDataEl : default;
                        string extType = ext.ValueKind != JsonValueKind.Undefined
                            && ext.TryGetProperty("type", out JsonElement etEl) ? etEl.GetString() ?? "" : "";

                        long? fileSizeBytes = null;
                        if (attrs.TryGetProperty("storageSize", out JsonElement sizeEl) && sizeEl.ValueKind == JsonValueKind.Number)
                            fileSizeBytes = sizeEl.GetInt64();

                        DateTime? lastModified = null;
                        if (attrs.TryGetProperty("lastModifiedTime", out JsonElement lmEl)
                            && lmEl.GetString() is string lmStr
                            && DateTime.TryParse(lmStr, out DateTime lmParsed))
                        {
                            lastModified = lmParsed;
                        }

                        // modelGuid is the Revit cloud model GUID needed by
                        // ModelPathUtils.ConvertCloudGUIDsToCloudPath.
                        // It lives in extension.data.modelGuid or modelId.
                        // For CNV models, it might only be present in the ITEM metadata.
                        string? modelGuid = GetModelGuid(extData);
                        if (string.IsNullOrEmpty(modelGuid))
                        {
                            // Try the Item attributes instead of the Version attributes
                            JsonElement itemExt = itemAttrs.TryGetProperty("extension", out JsonElement ie) ? ie : default;
                            JsonElement itemExtData = itemExt.ValueKind != JsonValueKind.Undefined
                                && itemExt.TryGetProperty("data", out JsonElement ied) ? ied : default;
                            modelGuid = GetModelGuid(itemExtData);
                        }

                        // Fallback to revisionId ONLY IF it is a valid GUID and we have nothing better.
                        if (string.IsNullOrEmpty(modelGuid) && extData.ValueKind != JsonValueKind.Undefined)
                        {
                            if (extData.TryGetProperty("revisionId", out JsonElement revId))
                            {
                                string? rid = revId.GetString();
                                if (Guid.TryParse(rid, out _))
                                    modelGuid = rid;
                            }
                        }
                        string region = extData.ValueKind != JsonValueKind.Undefined
                            && extData.TryGetProperty("region", out JsonElement regEl) ? regEl.GetString() ?? "US" : "US";

                        string displayName = attrs.TryGetProperty("displayName", out JsonElement dn) ? dn.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(displayName))
                            displayName = attrs.TryGetProperty("name", out JsonElement nmEl) ? nmEl.GetString() ?? "" : "";

                        string parentFolderId = rels.TryGetProperty("parent", out JsonElement par)
                            && par.TryGetProperty("data", out JsonElement pard)
                            && pard.TryGetProperty("id", out JsonElement parid)
                            ? parid.GetString() ?? "" : "";

                        int versionNumber = 0;
                        if (attrs.TryGetProperty("versionNumber", out JsonElement vn) && vn.ValueKind == JsonValueKind.Number)
                            versionNumber = vn.GetInt32();

                        pending.Add((itemId, new ApsVersion
                        {
                            Id            = tipVersionId,
                            ItemId        = itemId,
                            HubId         = hubId,
                            ProjectId     = projectId,
                            ProjectGuid   = projectGuid,
                            FolderId      = parentFolderId,
                            VersionNumber = versionNumber,
                            DisplayName   = displayName,
                            FileName      = attrs.TryGetProperty("name", out JsonElement fn) ? fn.GetString() ?? "" : "",
                            FileType      = "rvt",
                            ExtensionType = extType,
                            ModelGuid     = modelGuid,
                            Region        = region,
                            IsWorkshared  = string.Equals(extType, "versions:autodesk.bim360:C4RModel", StringComparison.Ordinal),
                            FileSizeBytes = fileSizeBytes,
                            LastModified  = lastModified,
                            FolderPath    = "", // resolved below
                        }, parentFolderId));
                    }
                }

                nextUrl = null;
                if (root.TryGetProperty("links", out JsonElement links)
                    && links.TryGetProperty("next", out JsonElement next)
                    && next.TryGetProperty("href", out JsonElement href))
                {
                    nextUrl = href.GetString();
                }
            }

            // Resolve full paths — fetch any unknown ancestor folders from the API.
            foreach (var (itemId, apsVersion, parentFolderId) in pending)
            {
                if (results.ContainsKey(itemId)) continue;

                await EnsureFolderChainAsync(projectId, parentFolderId, folderId, topFolderName, folderCache, cancellationToken);
                apsVersion.FolderPath = ResolveFolderPath(parentFolderId, folderId, topFolderName, folderCache);
                results[itemId] = apsVersion;
            }
        }

        /// <summary>
        /// Walks from <paramref name="startId"/> toward <paramref name="rootId"/>, fetching any
        /// folder nodes not already in <paramref name="folderCache"/> from the APS API.
        /// After this call the cache is guaranteed to contain the full ancestry chain.
        /// </summary>
        private async Task EnsureFolderChainAsync(
            string projectId,
            string startId,
            string rootId,
            string rootName,
            Dictionary<string, (string Name, string ParentId)> folderCache,
            CancellationToken cancellationToken)
        {
            string current = startId;
            for (int depth = 0; depth < 25 && !string.IsNullOrEmpty(current) && current != rootId; depth++)
            {
                if (folderCache.ContainsKey(current))
                {
                    current = folderCache[current].ParentId;
                    continue;
                }

                // Check the shared cross-search cache first
                string cacheKey = $"{projectId}|{current}";
                if (_folderInfoCache.TryGetValue(cacheKey, out var cached))
                {
                    folderCache[current] = cached;
                    current = cached.ParentId;
                    continue;
                }

                // Fetch from APS
                try
                {
                    string token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
                    string url = $"{BatchConstants.ApsBaseUrl}/data/v1/projects/{projectId}/folders/{Uri.EscapeDataString(current)}";
                    using var req = BuildGet(url, token);
                    using HttpResponseMessage resp = await _http.SendAsync(req, cancellationToken);
                    if (!resp.IsSuccessStatusCode) break;

                    JsonElement root = await ParseRootAsync(resp, cancellationToken);
                    if (!root.TryGetProperty("data", out JsonElement dataEl)) break;

                    string name = dataEl.TryGetProperty("attributes", out JsonElement fa)
                        && fa.TryGetProperty("name", out JsonElement fn)
                        ? fn.GetString() ?? "" : "";

                    string parentId = dataEl.TryGetProperty("relationships", out JsonElement fr)
                        && fr.TryGetProperty("parent", out JsonElement fp)
                        && fp.TryGetProperty("data", out JsonElement fpd)
                        && fpd.TryGetProperty("id", out JsonElement fpid)
                        ? fpid.GetString() ?? "" : "";

                    if (string.IsNullOrEmpty(name)) break;

                    var info = (name, parentId);
                    folderCache[current] = info;
                    _folderInfoCache[cacheKey] = info; // share across searches

                    current = parentId;
                }
                catch
                {
                    break; // Network error — stop walking, use what we have
                }
            }
        }

        /// <summary>
        /// Walks <paramref name="folderCache"/> upward from <paramref name="startId"/> to the
        /// known root, building a slash-separated path.  Always returns at least the top-level
        /// folder name so the display is never empty.
        /// </summary>
        private static string ResolveFolderPath(
            string startId,
            string rootId,
            string rootName,
            Dictionary<string, (string Name, string ParentId)> folderCache)
        {
            if (string.IsNullOrEmpty(startId))
                return rootName;

            var parts = new Stack<string>();
            string current = startId;

            for (int depth = 0; depth < 25; depth++)
            {
                if (string.IsNullOrEmpty(current))
                    break;

                if (current == rootId)
                {
                    parts.Push(rootName);
                    break;
                }

                if (folderCache.TryGetValue(current, out var info))
                {
                    parts.Push(info.Name);
                    current = info.ParentId;
                }
                else
                {
                    // Chain breaks here — stop with what we have.
                    break;
                }
            }

            return parts.Count > 0 ? string.Join(" / ", parts) : rootName;
        }

        private static string BuildFolderSearchUrl(string projectId, string folderId, string searchText, bool filterByFileType)
        {
            // include=tip,refs — tip for version metadata (C4RModel recognition),
            // refs so parent folder objects appear in included[] for path display
            // page[limit] max is 100 for APS Data Management API
            var query = new List<string> { "page[limit]=100", "include=tip,refs" };

            // Note: We deliberately removed "filter[fileType]=rvt" here to be more robust
            // against APS indexing quirks. We filter locally by extension instead.

            if (!string.IsNullOrWhiteSpace(searchText))
                query.Add($"filter[attributes.displayName]-contains={Uri.EscapeDataString(searchText)}");

            string encodedFolderId = Uri.EscapeDataString(folderId);
            return $"{BatchConstants.ApsBaseUrl}/data/v1/projects/{projectId}/folders/{encodedFolderId}/search?{string.Join("&", query)}";
        }

        private static bool MatchesSearchText(string value, string searchText) =>
            string.IsNullOrWhiteSpace(searchText)
            || value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

        public bool IsRevitCloudModel(ApsVersion version)
        {
            ArgumentNullException.ThrowIfNull(version);

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
            string extType = ext.ValueKind != JsonValueKind.Undefined
                && ext.TryGetProperty("type", out JsonElement et) ? et.GetString() ?? "" : "";
            bool isWorkshared = string.Equals(extType, "versions:autodesk.bim360:C4RModel", StringComparison.Ordinal);

            string rawProjectId = e.TryGetProperty("relationships", out JsonElement rels)
                && rels.TryGetProperty("target", out JsonElement target)
                && target.TryGetProperty("data", out JsonElement targetData)
                && targetData.TryGetProperty("id", out JsonElement tid)
                ? tid.GetString() ?? projectId : projectId;
            string projectGuid = rawProjectId.StartsWith("b.", StringComparison.Ordinal)
                ? rawProjectId[2..] : rawProjectId;

            string? modelGuid = GetModelGuid(extData);

            // Note: In ParseVersion (GET version), we don't always have the item context
            // easily accessible here without a separate fetch. For now, rely on version data.
            // But we still check revisionId as a last resort if it is a GUID.
            if (string.IsNullOrEmpty(modelGuid) && extData.ValueKind != JsonValueKind.Undefined)
            {
                if (extData.TryGetProperty("revisionId", out JsonElement revId))
                {
                    string? rid = revId.GetString();
                    if (Guid.TryParse(rid, out _))
                        modelGuid = rid;
                }
            }

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

        private static string? GetModelGuid(JsonElement extData)
        {
            if (extData.ValueKind == JsonValueKind.Undefined) return null;

            if (extData.TryGetProperty("modelGuid", out JsonElement mg)) return mg.GetString();
            if (extData.TryGetProperty("modelId", out JsonElement mi)) return mi.GetString();
            if (extData.TryGetProperty("originalModelId", out JsonElement om)) return om.GetString();

            return null;
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
