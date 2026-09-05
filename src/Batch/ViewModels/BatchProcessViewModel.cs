using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Batch.Configuration;
using LECG.Batch.Models;
using LECG.Batch.Services;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.ViewModels
{
    public partial class BatchProcessViewModel : ObservableObject, IDisposable
    {
        private readonly IBatchOrchestrationService _orchestrator;
        private readonly IApsAuthService _authService;
        private readonly IApsSessionStore _sessionStore;
        private readonly IApsAuthSettingsProvider _authSettingsProvider;
        private readonly IBatchManifestService _manifestService;
        private readonly IBatchReportService _reportService;
        private readonly BatchRoutineSelector _routineSelector;
        private bool _isDisposed;

        public ObservableCollection<BatchJobRowViewModel> Jobs { get; } = new();

        // ── Routine Selector ──────────────────────────────────────────────────
        public IReadOnlyList<IBatchJobRoutine> AvailableRoutines => _routineSelector.Available;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStartBatch))]
        [NotifyPropertyChangedFor(nameof(SelectedRoutineDescription))]
        private IBatchJobRoutine? _selectedRoutine;

        public string SelectedRoutineDescription => SelectedRoutine?.Description ?? string.Empty;
        public bool HasSelectedRoutine => SelectedRoutine != null;

        partial void OnSelectedRoutineChanged(IBatchJobRoutine? value)
        {
            _routineSelector.Selected = value;
            OnPropertyChanged(nameof(HasSelectedRoutine));
        }

        // ── Log Entries ───────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isSignedIn;

        [ObservableProperty]
        private ApsAuthState _authState = ApsAuthState.SignedOut;

        [ObservableProperty]
        private string _authStatusMessage = "Sign in to Autodesk Docs to browse cloud models.";

        [ObservableProperty]
        private bool _publishAfterSync = true;

        [ObservableProperty]
        private bool _isBatchActive;

        [ObservableProperty]
        private string _queueSummary = "No models in queue yet.";

        [ObservableProperty]
        private string _hostCompatibilityMessage = string.Empty;

        public bool HasJobs => Jobs.Count > 0;
        public bool HasHostCompatibilityMessage => !string.IsNullOrWhiteSpace(HostCompatibilityMessage);
        public int QueuedJobsCount => _orchestrator.Manifest.Jobs.Count(j => j.Status == JobStatus.Queued);
        public int CompletedJobsCount => _orchestrator.Manifest.Jobs.Count(j => j.Status == JobStatus.Completed);
        public int AttentionJobsCount => _orchestrator.Manifest.Jobs.Count(j =>
            j.Status is JobStatus.Failed or JobStatus.Poisoned or JobStatus.Cancelled);
        public bool AuthHasError => AuthState is ApsAuthState.SignInFailed or ApsAuthState.ConfigurationError;
        public bool IsSigningIn => AuthState == ApsAuthState.SigningIn;
        public bool CanSignIn => !IsBusy && !IsSignedIn && !IsSigningIn;
        public bool CanSignOut => !IsBusy && IsSignedIn && !IsBatchActive;
        public string SignInButtonText => IsSigningIn ? "Signing In..." : "Sign In";

        public bool CanBrowseModels => IsSignedIn && !IsBusy;
        public bool CanStartBatch => IsSignedIn && SelectedRoutine != null && _orchestrator.HasPending && !IsBusy && !IsBatchActive && !_orchestrator.IsPaused && !_orchestrator.IsCancelled;
        public bool CanPauseBatch => IsSignedIn && _orchestrator.HasPending && IsBatchActive && !_orchestrator.IsPaused && !_orchestrator.IsCancelled;
        public bool CanResumeBatch => IsSignedIn && _orchestrator.HasPending && _orchestrator.IsPaused && !_orchestrator.IsCancelled;
        public bool CanCancelBatch => _orchestrator.HasPending && !_orchestrator.IsCancelled && (IsBatchActive || _orchestrator.IsPaused);

        // Injected by BatchProcessCommand after ExternalEvent creation
        public Action? RaiseExternalEvent { get; set; }

        public BatchProcessViewModel(
            IBatchOrchestrationService orchestrator,
            IApsAuthService authService,
            IApsSessionStore sessionStore,
            IApsAuthSettingsProvider authSettingsProvider,
            IBatchManifestService manifestService,
            IBatchReportService reportService,
            BatchRoutineSelector routineSelector)
        {
            _orchestrator = orchestrator;
            _authService = authService;
            _sessionStore = sessionStore;
            _authSettingsProvider = authSettingsProvider;
            _manifestService = manifestService;
            _reportService = reportService;
            _routineSelector = routineSelector;

            _orchestrator.JobStatusChanged += OnJobStatusChanged;
            _orchestrator.BatchCompleted += OnBatchCompleted;
            _orchestrator.ReAuthRequired += OnReAuthRequired;
            Jobs.CollectionChanged += OnJobsCollectionChanged;

            // Mirror Logger entries into LogEntries so the log panel can bind to this VM
            foreach (LogEntry entry in Logger.Instance.Entries)
                LogEntries.Add(entry);

            Logger.Instance.Entries.CollectionChanged += OnLogEntriesChanged;
            SelectedRoutine = _routineSelector.Selected ?? _routineSelector.Available.FirstOrDefault();
        }

        private void OnJobsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshUiState();

        private void OnLogEntriesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null) return;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (LogEntry entry in e.NewItems)
                    LogEntries.Add(entry);
            });
        }

        public void Initialize()
        {
            IsBatchActive = false;
            _orchestrator.Initialize();
            RefreshJobsList();
            RefreshAuthState();
            RefreshUiState();

            if (_orchestrator.Manifest.Jobs.Any(j => j.Status == JobStatus.Queued))
                StatusMessage = $"{_orchestrator.Manifest.Jobs.Count(j => j.Status == JobStatus.Queued)} job(s) pending from previous session. Ready to resume.";
        }

        partial void OnIsBusyChanged(bool value) => RefreshUiState();
        partial void OnIsSignedInChanged(bool value) => RefreshUiState();
        partial void OnAuthStateChanged(ApsAuthState value) => RefreshUiState();
        partial void OnIsBatchActiveChanged(bool value) => RefreshUiState();
        partial void OnHostCompatibilityMessageChanged(string value) => RefreshUiState();

        public void SetHostRevitVersion(string hostRevitVersion)
        {
            string trimmedVersion = hostRevitVersion?.Trim() ?? string.Empty;
            HostCompatibilityMessage = string.IsNullOrWhiteSpace(trimmedVersion)
                ? string.Empty
                : $"Running in Revit {trimmedVersion}. Browsing Autodesk Docs models works here; opening and processing still depend on what this Revit version can open.";
        }

        [RelayCommand]
        private async Task SignInAsync()
        {
            if (!CanSignIn)
                return;

            SetAuthState(ApsAuthState.SigningIn, "Complete Autodesk sign-in in your browser.");
            IsBusy = true;
            StatusMessage = "Opening Autodesk sign-in in your browser...";
            try
            {
                await _authService.SignInAsync();
                SetAuthState(ApsAuthState.SignedIn, "Connected to Autodesk Docs.");
                StatusMessage = "Signed in to Autodesk Docs.";
            }
            catch (ApsAuthConfigurationException ex)
            {
                SetAuthState(ApsAuthState.ConfigurationError, ex.Message);
                StatusMessage = ex.Message;
                Logger.Instance.LogWarning($"[Batch] APS auth configuration error: {ex.Message}");
            }
            catch (Exception ex)
            {
                SetAuthState(ApsAuthState.SignInFailed, ex.Message);
                StatusMessage = $"Sign-in failed: {ex.Message}";
                Logger.Instance.LogWarning($"[Batch] Sign-in error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void SignOut()
        {
            _authService.SignOut();
            RefreshAuthState();
            StatusMessage = "Signed out.";
        }

        public void AddJobsToQueue(IEnumerable<ApsVersion> versions)
        {
            BatchManifest? manifest = _manifestService.Load() ?? new BatchManifest();
            HashSet<string> existingJobIds = manifest.Jobs
                .Select(job => job.JobId)
                .ToHashSet(StringComparer.Ordinal);

            _manifestService.AddJobs(manifest, versions, PublishAfterSync);
            IReadOnlyList<BatchJob> newJobs = manifest.Jobs
                .Where(job => !existingJobIds.Contains(job.JobId))
                .ToList();

            if (newJobs.Count == 0)
            {
                StatusMessage = "Selected models are already queued.";
                RefreshUiState();
                return;
            }

            _manifestService.Save(manifest);
            _orchestrator.EnqueueJobs(newJobs);
            RefreshJobsList();
            StatusMessage = $"{newJobs.Count} job(s) added. Queue has {Jobs.Count} job(s).";
            RefreshUiState();
        }

        [RelayCommand]
        private void StartBatch()
        {
            if (!IsSignedIn)
            {
                StatusMessage = "Sign in to Autodesk Docs before starting the batch.";
                return;
            }

            if (!_orchestrator.HasPending)
            {
                StatusMessage = "No jobs in queue.";
                return;
            }

            IsBatchActive = true;
            StatusMessage = "Batch running...";
            RefreshUiState();
            RaiseExternalEvent?.Invoke();
        }

        [RelayCommand]
        private void PauseBatch()
        {
            _orchestrator.Pause();
            IsBatchActive = false;
            StatusMessage = "Paused. Will stop after current job.";
            RefreshUiState();
        }

        [RelayCommand]
        private void ResumeBatch()
        {
            _orchestrator.Resume();
            IsBatchActive = true;
            StatusMessage = "Resuming...";
            RefreshUiState();
            RaiseExternalEvent?.Invoke();
        }

        [RelayCommand]
        private void CancelBatch()
        {
            _orchestrator.Cancel();
            IsBatchActive = false;
            StatusMessage = "Cancelling after current job...";
            RefreshUiState();
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
                if (!_orchestrator.HasPending)
                    IsBatchActive = false;

                RefreshUiState();
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
                IsBatchActive = false;
                RefreshUiState();
            });
        }

        private void OnReAuthRequired(object? sender, EventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusMessage = "Session expired. Please sign in again.";
                SetAuthState(ApsAuthState.SignInFailed, "Session expired. Please sign in again.");
                IsBatchActive = false;
                _orchestrator.Pause();
                RefreshUiState();
            });
        }

        private void RefreshJobsList()
        {
            Jobs.Clear();
            foreach (BatchJob job in _orchestrator.Manifest.Jobs)
                Jobs.Add(new BatchJobRowViewModel(job));

            RefreshUiState();
        }

        private void RefreshUiState()
        {
            QueueSummary = !HasJobs
                ? "No models in queue yet."
                : $"{QueuedJobsCount} queued, {CompletedJobsCount} completed, {AttentionJobsCount} with attention.";

            OnPropertyChanged(nameof(HasJobs));
            OnPropertyChanged(nameof(HasHostCompatibilityMessage));
            OnPropertyChanged(nameof(QueuedJobsCount));
            OnPropertyChanged(nameof(CompletedJobsCount));
            OnPropertyChanged(nameof(AttentionJobsCount));
            OnPropertyChanged(nameof(AuthHasError));
            OnPropertyChanged(nameof(IsSigningIn));
            OnPropertyChanged(nameof(CanSignIn));
            OnPropertyChanged(nameof(CanSignOut));
            OnPropertyChanged(nameof(SignInButtonText));
            OnPropertyChanged(nameof(CanBrowseModels));
            OnPropertyChanged(nameof(CanStartBatch));
            OnPropertyChanged(nameof(CanPauseBatch));
            OnPropertyChanged(nameof(CanResumeBatch));
            OnPropertyChanged(nameof(CanCancelBatch));
            OnPropertyChanged(nameof(HasSelectedRoutine));
            OnPropertyChanged(nameof(SelectedRoutineDescription));
        }

        private void RefreshAuthState()
        {
            ApsSession? session = _sessionStore.Load();
            if (HasUsableSession(session))
            {
                string authMessage = session!.IsExpired(BatchConstants.TokenRefreshBufferMin)
                    ? "Connected to Autodesk Docs. Your session will refresh automatically if needed."
                    : "Connected to Autodesk Docs.";
                SetAuthState(ApsAuthState.SignedIn, authMessage);
                return;
            }

            if (session != null)
                _sessionStore.Delete();

            if (_authSettingsProvider.TryGetValidSettings(out _, out string errorMessage))
            {
                SetAuthState(ApsAuthState.SignedOut, "Sign in to Autodesk Docs to browse cloud models.");
                return;
            }

            SetAuthState(ApsAuthState.ConfigurationError, errorMessage);
        }

        private void SetAuthState(ApsAuthState authState, string authStatusMessage)
        {
            AuthState = authState;
            AuthStatusMessage = authStatusMessage;
            IsSignedIn = authState == ApsAuthState.SignedIn;
        }

        private static bool HasUsableSession(ApsSession? session)
        {
            if (session == null)
                return false;

            return !session.IsExpired(BatchConstants.TokenRefreshBufferMin)
                || !string.IsNullOrWhiteSpace(session.RefreshToken);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _orchestrator.JobStatusChanged -= OnJobStatusChanged;
            _orchestrator.BatchCompleted -= OnBatchCompleted;
            _orchestrator.ReAuthRequired -= OnReAuthRequired;
            Jobs.CollectionChanged -= OnJobsCollectionChanged;
            Logger.Instance.Entries.CollectionChanged -= OnLogEntriesChanged;
        }
    }
}
