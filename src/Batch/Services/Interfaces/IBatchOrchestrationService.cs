using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IBatchOrchestrationService
    {
        BatchManifest Manifest { get; }
        bool IsCancelled { get; }
        bool IsPaused { get; }
        bool HasPending { get; }

        void Initialize();
        void EnqueueJobs(IEnumerable<BatchJob> jobs);
        BatchJob? DequeueNext();
        void Transition(BatchJob job, JobStatus newStatus);
        void Pause();
        void Resume();
        void Cancel();
        void NotifyBatchComplete();

        event EventHandler<BatchJob>? JobStatusChanged;
        event EventHandler? BatchCompleted;
        event EventHandler? ReAuthRequired;
    }
}
