using System.Text.Json;
using System.Text.Json.Serialization;
using LECG.Batch.Configuration;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class BatchManifestService : IBatchManifestService
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public BatchManifest? Load()
        {
            string path = BatchConstants.ManifestPath;
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BatchManifest>(json, s_jsonOptions);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"[Batch] Failed to load manifest: {ex.Message}");
                return null;
            }
        }

        public void Save(BatchManifest manifest)
        {
            string path = BatchConstants.ManifestPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string json = JsonSerializer.Serialize(manifest, s_jsonOptions);
            File.WriteAllText(path, json);
        }

        public void AddJobs(BatchManifest manifest, IEnumerable<ApsVersion> versions, bool publishAfterSync)
        {
            foreach (ApsVersion v in versions)
            {
                bool duplicate = manifest.Jobs.Any(j =>
                    j.ItemId == v.ItemId && j.VersionId == v.Id);

                if (duplicate) continue;

                manifest.Jobs.Add(new BatchJob
                {
                    JobId            = Guid.NewGuid().ToString(),
                    CorrelationId    = manifest.BatchRunId,
                    Status           = JobStatus.Queued,
                    HubId            = v.HubId,
                    ProjectId        = v.ProjectId,
                    ProjectGuid      = v.ProjectGuid,
                    FolderId         = v.FolderId,
                    ItemId           = v.ItemId,
                    VersionId        = v.Id,
                    VersionNumber    = v.VersionNumber,
                    ModelGuid        = v.ModelGuid ?? "",
                    Region           = v.Region ?? "US",
                    DisplayName      = v.DisplayName,
                    FileName         = v.FileName,
                    ModelType        = v.IsWorkshared ? ModelType.CloudWorkshared : ModelType.NonWorkshared,
                    PublishAfterSync = publishAfterSync,
                    MaxRetries       = BatchConstants.DefaultMaxRetries,
                    AddedAt          = DateTime.UtcNow,
                });
            }
        }
    }
}
