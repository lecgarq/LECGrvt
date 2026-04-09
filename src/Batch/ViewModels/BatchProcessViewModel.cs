using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.ViewModels
{
    public partial class BatchProcessViewModel : ObservableObject
    {
        private readonly IBatchOrchestrationService _orchestrator;
        private readonly IApsAuthService _authService;
        private readonly IApsSessionStore _sessionStore;
        private readonly IBatchManifestService _manifestService;
        private readonly IBatchReportService _reportService;

        public ObservableCollection<BatchJobRowViewModel> Jobs { get; } = new();

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isSignedIn;

        [ObservableProperty]
        private bool _publishAfterSync = true;

        // Injected by BatchProcessCommand after ExternalEvent creation
        public Action? RaiseExternalEvent { get; set; }

        public BatchProcessViewModel(
            IBatchOrchestrationService orchestrator,
            IApsAuthService authService,
            IApsSessionStore sessionStore,
            IBatchManifestService manifestService,
            IBatchReportService reportService)
        {
            _orchestrator = orchestrator;
            _authService = authService;
            _sessionStore = sessionStore;
            _manifestService = manifestService;
            _reportService = reportService;

            _orchestrator.JobStatusChanged += OnJobStatusChanged;
            _orchestrator.BatchCompleted += OnBatchCompleted;
            _orchestrator.ReAuthRequired += OnReAuthRequired;
        }

        public void Initialize()
        {
            IsSignedIn = _sessionStore.Load() != null;
            _orchestrator.Initialize();
            RefreshJobsList();

            if (_orchestrator.Manifest.Jobs.Any(j => j.Status == JobStatus.Queued))
                StatusMessage = $"{_orchestrator.Manifest.Jobs.Count(j => j.Status == JobStatus.Queued)} job(s) pending from previous session. Ready to resume.";
        }

        [RelayCommand]
        private async Task SignInAsync()
        {
            IsBusy = true;
            StatusMessage = "Signing in to Autodesk...";
            try
            {
                await _authService.SignInAsync();
                IsSignedIn = true;
                StatusMessage = "Signed in.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sign-in failed: {ex.Message}";
                Logger.Instance.Log($"[Batch] Sign-in error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void SignOut()
        {
            _authService.SignOut();
            IsSignedIn = false;
            StatusMessage = "Signed out.";
        }

        public void AddJobsToQueue(IEnumerable<ApsVersion> versions)
        {
            BatchManifest? manifest = _manifestService.Load() ?? new BatchManifest();
            _manifestService.AddJobs(manifest, versions, PublishAfterSync);
            _manifestService.Save(manifest);
            _orchestrator.EnqueueJobs(manifest.Jobs.Where(j => j.Status == JobStatus.Queued));
            RefreshJobsList();
            StatusMessage = $"Queue has {Jobs.Count} job(s).";
        }

        [RelayCommand]
        private void StartBatch()
        {
            if (!_orchestrator.HasPending)
            {
                StatusMessage = "No jobs in queue.";
                return;
            }

            StatusMessage = "Batch running...";
            RaiseExternalEvent?.Invoke();
        }

        [RelayCommand]
        private void PauseBatch()
        {
            _orchestrator.Pause();
            StatusMessage = "Paused. Will stop after current job.";
        }

        [RelayCommand]
        private void ResumeBatch()
        {
            _orchestrator.Resume();
            StatusMessage = "Resuming...";
            RaiseExternalEvent?.Invoke();
        }

        [RelayCommand]
        private void CancelBatch()
        {
            _orchestrator.Cancel();
            StatusMessage = "Cancelling after current job...";
        }

        private void OnJobStatusChanged(object? sender, BatchJob job)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                BatchJobRowViewModel? row = Jobs.FirstOrDefault(r => r.JobId == job.JobId);
                if (row != null)
                    row.Update(job);
                else
                    Jobs.Add(new BatchJobRowViewModel(job));

                StatusMessage = $"[{job.Status}] {job.DisplayName}";
            });
        }

        private void OnBatchCompleted(object? sender, EventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _reportService.WriteReport(_orchestrator.Manifest);
                int completed = _orchestrator.Manifest.Jobs.Count(j => j.Status == JobStatus.Completed);
                int total = _orchestrator.Manifest.Jobs.Count;
                StatusMessage = $"Batch complete: {completed}/{total} succeeded.";
                IsBusy = false;
            });
        }

        private void OnReAuthRequired(object? sender, EventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusMessage = "Session expired. Please sign in again.";
                IsSignedIn = false;
                _orchestrator.Pause();
            });
        }

        private void RefreshJobsList()
        {
            Jobs.Clear();
            foreach (BatchJob job in _orchestrator.Manifest.Jobs)
                Jobs.Add(new BatchJobRowViewModel(job));
        }
    }
}
