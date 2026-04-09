namespace LECG.Batch.Models
{
    public class BatchManifest
    {
        public string BatchRunId { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<BatchJob> Jobs { get; set; } = new();
    }
}
