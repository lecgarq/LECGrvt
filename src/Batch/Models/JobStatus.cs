namespace LECG.Batch.Models
{
    public enum JobStatus
    {
        Queued,
        Opening,
        Processing,
        Saving,
        Synced,
        PublishQueued,
        Published,
        Completed,
        Failed,
        Poisoned,
        Cancelled
    }
}
