using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class BatchOrchestrationService : IBatchOrchestrationService
    {
        private readonly IBatchManifestService _manifestService;
        private readonly Queue<BatchJob> _jobQueue = new();
        private BatchJob? _currentJob;
        private BatchManifest _manifest = new();

        public BatchManifest Manifest => _manifest;
        public bool IsCancelled { get; private set; }
        public bool IsPaused { get; private set; }
        public bool HasPending => _jobQueue.Count > 0 || _currentJob != null;

        public event EventHandler<BatchJob>? JobStatusChanged;
        public event EventHandler? BatchCompleted;
        public event EventHandler? ReAuthRequired;

        public BatchOrchestrationService(IBatchManifestService manifestService)
        {
            _manifestService = manifestService;
        }

        public void Initialize()
        {
            BatchManifest? loaded = _manifestService.Load();
            if (loaded == null) return;

            _manifest = loaded;
            IsCancelled = false;
            IsPaused = false;

            // Crash recovery: reset in-flight jobs back to Queued
            int resetCount = 0;
            foreach (BatchJob job in _manifest.Jobs)
            {
                if (job.Status is JobStatus.Opening or JobStatus.Processing or JobStatus.Saving)
                {
                    job.Status = JobStatus.Queued;
                    job.StartedAt = null;
                    resetCount++;
                }
            }

            if (resetCount > 0)
            {
                Logger.Instance.Log($"[Batch] Crash recovery: {resetCount} job(s) reset to Queued.");
                _manifestService.Save(_manifest);
            }

            // Re-populate queue from manifest
            _jobQueue.Clear();
            _currentJob = null;
            foreach (BatchJob job in _manifest.Jobs.Where(j => j.Status == JobStatus.Queued))
                _jobQueue.Enqueue(job);
        }

        public void EnqueueJobs(IEnumerable<BatchJob> jobs)
        {
            foreach (BatchJob job in jobs)
            {
                _manifest.Jobs.Add(job);
                _jobQueue.Enqueue(job);
            }
            _manifestService.Save(_manifest);
        }

        public BatchJob? DequeueNext()
        {
            if (IsCancelled || IsPaused) return null;
            if (_jobQueue.Count == 0) return null;

            _currentJob = _jobQueue.Dequeue();
            return _currentJob;
        }

        public void Transition(BatchJob job, JobStatus newStatus)
        {
            job.Status = newStatus;

            if (newStatus is JobStatus.Opening)
                job.StartedAt = DateTime.UtcNow;
            else if (newStatus is JobStatus.Completed or JobStatus.Poisoned or JobStatus.Cancelled)
            {
                job.CompletedAt = DateTime.UtcNow;
                _currentJob = null;
            }

            _manifestService.Save(_manifest);
            JobStatusChanged?.Invoke(this, job);
        }

        public void Pause()
        {
            IsPaused = true;
            Logger.Instance.Log("[Batch] Paused.");
        }

        public void Resume()
        {
            IsPaused = false;
            Logger.Instance.Log("[Batch] Resumed.");
        }

        public void Cancel()
        {
            IsCancelled = true;
            Logger.Instance.Log("[Batch] Cancelled.");
        }

        public void NotifyBatchComplete()
        {
            Logger.Instance.Log("[Batch] All jobs processed.");
            BatchCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseReAuthRequired()
        {
            ReAuthRequired?.Invoke(this, EventArgs.Empty);
        }
    }
}
