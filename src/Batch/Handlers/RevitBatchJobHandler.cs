using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Batch.Models;
using LECG.Batch.Services;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Handlers
{
    public class RevitBatchJobHandler : IExternalEventHandler
    {
        private readonly IBatchOrchestrationService _orchestrator;
        private readonly ICloudModelOpenService _openService;
        private readonly ISynchronizeWithCentralService _syncService;
        private readonly ICloudSaveService _saveService;
        private readonly IApsPublishService _publishService;
        private readonly IBatchManifestService _manifestService;
        private readonly IBatchJobRoutine _routine;

        // Set by BatchProcessCommand after ExternalEvent is created
        public ExternalEvent? ExternalEvent { get; set; }

        public RevitBatchJobHandler(
            IBatchOrchestrationService orchestrator,
            ICloudModelOpenService openService,
            ISynchronizeWithCentralService syncService,
            ICloudSaveService saveService,
            IApsPublishService publishService,
            IBatchManifestService manifestService,
            IBatchJobRoutine routine)
        {
            _orchestrator = orchestrator;
            _openService = openService;
            _syncService = syncService;
            _saveService = saveService;
            _publishService = publishService;
            _manifestService = manifestService;
            _routine = routine;
        }

        public string GetName() => "LECG Batch Job Handler";

        public void Execute(UIApplication app)
        {
            BatchJob? job = _orchestrator.DequeueNext();
            if (job == null) return;

            Logger.Instance.Log($"[Batch] Starting job: {job.DisplayName}");

            Document? doc = null;
            bool isWorkshared = false;

            try
            {
                // Open
                _orchestrator.Transition(job, JobStatus.Opening);
                doc = _openService.Open(app, job);
                if (doc == null)
                    throw new InvalidOperationException($"Failed to open cloud model: {job.DisplayName}");

                isWorkshared = doc.IsWorkshared;

                // Subscribe failure handler to suppress non-critical warnings
                app.Application.FailuresProcessing += OnFailuresProcessing;

                // Process
                _orchestrator.Transition(job, JobStatus.Processing);
                _routine.Execute(app, doc, job);

                // Save / Sync
                _orchestrator.Transition(job, JobStatus.Saving);
                if (isWorkshared)
                    _syncService.Sync(doc, job);
                else
                    _saveService.Save(doc);

                _orchestrator.Transition(job, JobStatus.Synced);

                // Optional publish
                if (job.PublishAfterSync)
                {
                    _orchestrator.Transition(job, JobStatus.PublishQueued);
                    _publishService.PublishAsync(job).GetAwaiter().GetResult();
                    _orchestrator.Transition(job, JobStatus.Published);
                }

                // Close
                CloseDocument(doc, isWorkshared);
                doc = null;

                _orchestrator.Transition(job, JobStatus.Completed);
                Logger.Instance.Log($"[Batch] Job completed: {job.DisplayName}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"[Batch] Job failed ({job.DisplayName}): {ex.Message}");
                job.ErrorMessage = ex.Message;

                if (job.RetryCount < job.MaxRetries)
                {
                    job.RetryCount++;
                    _orchestrator.Transition(job, JobStatus.Queued);
                    // Re-enqueue for retry
                    _orchestrator.EnqueueJobs(new[] { job });
                }
                else
                {
                    _orchestrator.Transition(job, JobStatus.Poisoned);
                }

                // Attempt to close document on failure
                if (doc != null)
                {
                    try { CloseDocument(doc, isWorkshared); } catch { }
                }
            }
            finally
            {
                app.Application.FailuresProcessing -= OnFailuresProcessing;
                _manifestService.Save(_orchestrator.Manifest);

                if (_orchestrator.HasPending && !_orchestrator.IsCancelled && !_orchestrator.IsPaused)
                    ExternalEvent?.Raise();
                else
                    _orchestrator.NotifyBatchComplete();
            }
        }

        private static void CloseDocument(Document doc, bool isWorkshared)
        {
            if (isWorkshared)
            {
                try
                {
                    WorksharingUtils.RelinquishOwnership(
                        doc,
                        new RelinquishOptions(true),
                        new TransactWithCentralOptions());
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning($"[Batch] Relinquish failed: {ex.Message}");
                }
            }

            doc.Close(false);
        }

        private static void OnFailuresProcessing(object? sender, Autodesk.Revit.DB.Events.FailuresProcessingEventArgs e)
        {
            FailuresAccessor accessor = e.GetFailuresAccessor();
            IList<FailureMessageAccessor> messages = accessor.GetFailureMessages();

            foreach (FailureMessageAccessor msg in messages)
            {
                if (msg.GetSeverity() == FailureSeverity.Warning)
                    accessor.DeleteWarning(msg);
            }
        }
    }
}
