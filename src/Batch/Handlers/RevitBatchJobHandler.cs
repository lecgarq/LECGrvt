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

        // Tracks the job currently being opened so the dialog handler can check its type.
        private BatchJob? _currentOpeningJob;

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
                // Open — subscribe to dialog handler before Open() so upgrade dialogs during file-open
                // are handled (the FailuresProcessing handler below fires only after open succeeds).
                _orchestrator.Transition(job, JobStatus.Opening);
                _currentOpeningJob = job;
                app.DialogBoxShowing += OnModelOpenDialogShowing;
                try
                {
                    doc = _openService.Open(app, job);
                }
                finally
                {
                    app.DialogBoxShowing -= OnModelOpenDialogShowing;
                    _currentOpeningJob = null;
                }

                if (doc == null)
                    throw new InvalidOperationException(
                        job.ModelType == ModelType.NonWorkshared
                            ? $"Failed to open '{job.DisplayName}'. Non-workshared models cannot be opened across Revit versions without upgrading. Process this model in the Revit version it was created with."
                            : $"Failed to open cloud model: {job.DisplayName}");

                isWorkshared = doc.IsWorkshared;

                // Subscribe failure handler to suppress non-critical warnings
                app.Application.FailuresProcessing += OnFailuresProcessing;

                // Process — suppress printer dialogs that may appear when configuring PrintManager
                _orchestrator.Transition(job, JobStatus.Processing);
                app.DialogBoxShowing += OnPrinterDialogShowing;
                try
                {
                    _routine.Execute(app, doc, job);
                }
                finally
                {
                    app.DialogBoxShowing -= OnPrinterDialogShowing;
                }

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
                Logger.Instance.LogError(
                    $"[{job.DisplayName}] Failed — {ex.Message}",
                    ex.Message,
                    ex.ToString());
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

        /// <summary>
        /// Suppresses model-version-upgrade dialogs to preserve cloud model versions across Revit releases.
        /// For workshared (C4R) models: chooses "Open without upgrading" (CommandLink1 = 1001).
        /// For non-workshared models: cancels (0) — they cannot be saved in an older format.
        /// Mirrors the pattern used in PurgeCommand.OnDialogShowing.
        /// </summary>
        private void OnModelOpenDialogShowing(object? sender, Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs e)
        {
            string dialogId = e.DialogId ?? "";
            bool isVersionDialog =
                dialogId.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ||
                dialogId.Contains("version", StringComparison.OrdinalIgnoreCase) ||
                dialogId.Contains("older", StringComparison.OrdinalIgnoreCase);

            if (!isVersionDialog)
                return;

            bool isWorkshared = _currentOpeningJob?.ModelType == ModelType.CloudWorkshared;
            int result = isWorkshared ? 1001 : 0; // 1001 = CommandLink1 ("Open without upgrading"), 0 = Cancel

            Logger.Instance.Log($"[Batch] Version dialog '{dialogId}' during open of '{_currentOpeningJob?.DisplayName}'. " +
                $"IsWorkshared={isWorkshared}. Overriding result to {result}.");

            e.OverrideResult(result);
        }

        /// <summary>
        /// Suppresses printer/print-settings dialogs that Revit may show when PrintManager
        /// is configured during the Publish To Cloud routine. Tries OK → custom button 1001 → YES.
        /// </summary>
        private static void OnPrinterDialogShowing(object? sender, Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs e)
        {
            string text = (e.DialogId ?? "").ToLowerInvariant();
            // Also check message if accessible
            string? message = null;
            try { message = (e as Autodesk.Revit.UI.Events.TaskDialogShowingEventArgs)?.Message?.ToLowerInvariant(); } catch { }
            string combined = text + " " + (message ?? string.Empty);

            bool isPrinterDialog =
                combined.Contains("printer") ||
                combined.Contains("print setup") ||
                combined.Contains("print settings") ||
                combined.Contains("print driver") ||
                combined.Contains("paper size") ||
                combined.Contains("paper source") ||
                combined.Contains("no printer") ||
                combined.Contains("printing") ||
                combined.Contains("print resource");

            if (!isPrinterDialog)
                return;

            // Try OK → custom button → YES
            try { e.OverrideResult(1); return; } catch { }
            try { e.OverrideResult(1001); return; } catch { }
            try { e.OverrideResult(6); } catch { }
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
