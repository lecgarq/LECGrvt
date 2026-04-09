using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LECG.Batch.Configuration;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class BatchReportService : IBatchReportService
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public void WriteReport(BatchManifest manifest)
        {
            var report = new BatchReport
            {
                BatchRunId  = manifest.BatchRunId,
                StartedAt   = manifest.Jobs.Min(j => j.StartedAt) ?? DateTime.UtcNow,
                CompletedAt = manifest.Jobs.All(j => j.CompletedAt.HasValue || j.Status is JobStatus.Poisoned or JobStatus.Cancelled)
                    ? DateTime.UtcNow : null,
                TotalJobs   = manifest.Jobs.Count,
                Completed   = manifest.Jobs.Count(j => j.Status == JobStatus.Completed),
                Failed      = manifest.Jobs.Count(j => j.Status == JobStatus.Failed),
                Poisoned    = manifest.Jobs.Count(j => j.Status == JobStatus.Poisoned),
                Cancelled   = manifest.Jobs.Count(j => j.Status == JobStatus.Cancelled),
            };

            foreach (BatchJob job in manifest.Jobs)
            {
                report.Jobs.Add(new BatchJobReportEntry
                {
                    JobId        = job.JobId,
                    DisplayName  = job.DisplayName,
                    Status       = job.Status.ToString(),
                    ModelType    = job.ModelType.ToString(),
                    StartedAt    = job.StartedAt,
                    CompletedAt  = job.CompletedAt,
                    Published    = job.Status is JobStatus.Published or JobStatus.Completed && job.PublishAfterSync,
                    RetryCount   = job.RetryCount,
                    ErrorMessage = job.ErrorMessage,
                });
            }

            string dir = BatchConstants.ReportsDir;
            Directory.CreateDirectory(dir);

            string date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string path = Path.Combine(dir, $"batch-{date}-{manifest.BatchRunId[..8]}.json");
            string json = JsonSerializer.Serialize(report, s_jsonOptions);
            File.WriteAllText(path, json);
            Logger.Instance.Log($"[Batch] Report written: {path}");
        }
    }
}
