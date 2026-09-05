using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class ApsCloudModelIndexStore : IApsCloudModelIndexStore
    {
        private readonly string _cacheFilePath;
        private readonly object _lock = new object();
        private Dictionary<string, CloudModelCacheEntry> _cache = new Dictionary<string, CloudModelCacheEntry>();

        public ApsCloudModelIndexStore()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string batchDir = Path.Combine(appData, "LECG", "Batch");

            if (!Directory.Exists(batchDir))
            {
                Directory.CreateDirectory(batchDir);
            }

            _cacheFilePath = Path.Combine(batchDir, "cloud-model-index.json");
            LoadFromDisk();
        }

        public CloudModelCacheEntry? Get(string hubId, string projectId)
        {
            string key = GetKey(hubId, projectId);
            lock (_lock)
            {
                return _cache.TryGetValue(key, out CloudModelCacheEntry? entry) ? entry : null;
            }
        }

        public void Save(CloudModelCacheEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.HubId) || string.IsNullOrWhiteSpace(entry.ProjectId))
                return;

            string key = GetKey(entry.HubId, entry.ProjectId);
            lock (_lock)
            {
                _cache[key] = entry;
                SaveToDisk();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                if (File.Exists(_cacheFilePath))
                {
                    try { File.Delete(_cacheFilePath); } catch { }
                }
            }
        }

        private string GetKey(string hubId, string projectId) => $"{hubId}|{projectId}";

        private void LoadFromDisk()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    string json = File.ReadAllText(_cacheFilePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, CloudModelCacheEntry>>(json);
                    if (data != null)
                    {
                        _cache = data;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"[Batch] Could not load cloud model index cache: {ex.Message}");
            }
        }

        private void SaveToDisk()
        {
            try
            {
                string json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_cacheFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"[Batch] Could not save cloud model index cache: {ex.Message}");
            }
        }
    }
}
