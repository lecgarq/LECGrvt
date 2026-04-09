namespace LECG.Batch.Models
{
    public class BatchJobReportEntry
    {
        public string JobId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Status { get; set; } = "";
        public string ModelType { get; set; } = "";
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool Published { get; set; }
        public int RetryCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BatchReport
    {
        public string BatchRunId { get; set; } = "";
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalJobs { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
        public int Poisoned { get; set; }
        public int Cancelled { get; set; }
        public List<BatchJobReportEntry> Jobs { get; private set; } = new();
    }
}
