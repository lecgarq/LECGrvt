using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;

namespace LECG.Batch.ViewModels
{
    public enum ScanStatus { Pending, Scanning, Done, Error }

    // ── Hub accordion card ────────────────────────────────────────────────
    public partial class HubNodeViewModel : ObservableObject
    {
        public ApsHub Hub { get; }
        public string HubId  => Hub.Id;
        public string HubName => Hub.Name;

        [ObservableProperty] private bool _isExpanded;

        public ObservableCollection<ProjectNodeViewModel> Projects { get; } = new();

        public HubNodeViewModel(ApsHub hub) { Hub = hub; }

        [RelayCommand]
        private void Toggle() => IsExpanded = !IsExpanded;
    }

    // ── Project row inside a hub ──────────────────────────────────────────
    public partial class ProjectNodeViewModel : ObservableObject
    {
        public ApsProject Project    { get; }
        public string ProjectId      => Project.Id;
        public string ProjectName    => Project.Name;

        [ObservableProperty] private ScanStatus _scanStatus = ScanStatus.Pending;
        [ObservableProperty] private int _modelCount;
        [ObservableProperty] private System.Windows.Visibility _visibility = System.Windows.Visibility.Visible;

        public bool HasModels => ModelCount > 0;

        public string StatusLabel => ScanStatus switch
        {
            ScanStatus.Pending => "Open to load",
            ScanStatus.Scanning => "Loading...",
            ScanStatus.Done when ModelCount == 0 => "No Revit models",
            ScanStatus.Done => $"{ModelCount} model{(ModelCount == 1 ? "" : "s")}",
            ScanStatus.Error => "Load error",
            _ => ""
        };

        public List<ApsVersion> Versions { get; } = new();

        public ProjectNodeViewModel(ApsProject project) { Project = project; }

        partial void OnScanStatusChanged(ScanStatus value)
        {
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(HasModels));
        }

        partial void OnModelCountChanged(int value)
        {
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(HasModels));
        }
    }

    // ── Model card with checkbox ──────────────────────────────────────────
    public partial class ModelCardViewModel : ObservableObject
    {
        public ApsVersion Version { get; }

        [ObservableProperty] private bool _isSelected;

        public string DisplayName      => Version.DisplayName;
        public string FolderPathDisplay => Version.FolderPathDisplay;
        public string FileSizeDisplay   => Version.FileSizeDisplay;
        public string LastModifiedDisplay => Version.LastModifiedDisplay;
        public string ModelTypeDisplay  => Version.ModelTypeDisplay;

        private readonly Action _onSelectionChanged;

        public ModelCardViewModel(ApsVersion version, Action onSelectionChanged)
        {
            Version = version;
            _onSelectionChanged = onSelectionChanged;
        }

        partial void OnIsSelectedChanged(bool value) => _onSelectionChanged();
    }

    // ── Main browser ViewModel ────────────────────────────────────────────
    public partial class CloudModelBrowserViewModel : ObservableObject
    {
        private readonly IApsDataManagementService _dmService;
        private readonly IApsCloudModelIndexStore _cacheStore;
        private CancellationTokenSource? _projectLoadCts;
        private readonly List<ModelCardViewModel> _allModelCards = new();
        private CancellationTokenSource? _searchDebounceCts;

        public ObservableCollection<HubNodeViewModel>    HubNodes       { get; } = new();
        public ObservableCollection<ModelCardViewModel>  FilteredModels { get; } = new();
        public ObservableCollection<ApsVersion>          SelectedVersions { get; } = new();

        [ObservableProperty] private bool   _isHubsView   = true;
        [ObservableProperty] private bool   _isModelsView;
        [ObservableProperty] private ProjectNodeViewModel? _activeProject;
        [ObservableProperty] private bool   _filterEmptyProjects;
        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage   = "Loading hubs...";
        [ObservableProperty] private int    _scannedProjectCount;
        [ObservableProperty] private int    _totalProjectCount;
        [ObservableProperty] private string _searchText      = "";
        [ObservableProperty] private string _sortColumn      = "Name";
        [ObservableProperty] private bool   _sortDescending;

        public bool   IsScanning          => ScannedProjectCount < TotalProjectCount && TotalProjectCount > 0;
        public string ScanProgressText    => $"Scanning {ScannedProjectCount}/{TotalProjectCount}";
        public int    SelectedCount        => _allModelCards.Count(c => c.IsSelected);
        public string AddToQueueButtonText => SelectedCount > 0
            ? $"Add {SelectedCount} to Queue" : "Select models to add";
        public bool   CanAddToQueue        => SelectedCount > 0;

        public CloudModelBrowserViewModel(IApsDataManagementService dmService, IApsCloudModelIndexStore cacheStore)
        {
            _dmService = dmService;
            _cacheStore = cacheStore;
        }

        // ── Initialise ────────────────────────────────────────────────────

        public async Task EnsureInitializedAsync()
        {
            if (HubNodes.Count == 0 && !IsLoading)
                await LoadHubsAsync();
        }

        [RelayCommand]
        private async Task LoadHubsAsync()
        {
            _projectLoadCts?.Cancel();
            IsLoading = true;
            StatusMessage = "Loading hubs...";
            HubNodes.Clear();
            ScannedProjectCount = 0;
            TotalProjectCount   = 0;
            RaiseProgressState();

            try
            {
                IReadOnlyList<ApsHub> hubs = await _dmService.GetHubsAsync();
                foreach (ApsHub hub in hubs.OrderBy(h => h.Name))
                    HubNodes.Add(new HubNodeViewModel(hub));

                if (hubs.Count == 0)
                {
                    StatusMessage = "No hubs found for the signed-in Autodesk account.";
                    return;
                }

                StatusMessage = "Loading projects...";
                await LoadAllProjectsAsync();
                StatusMessage = "Expand a hub and open a project to load Revit models.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading hubs: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAllProjectsAsync()
        {
            Task<(HubNodeViewModel HubNode, IReadOnlyList<ApsProject> Projects)>[] loadTasks = HubNodes
                .Select(async hubNode =>
                {
                    try
                    {
                        IReadOnlyList<ApsProject> projects = await _dmService.GetProjectsAsync(hubNode.HubId);
                        return (hubNode, projects);
                    }
                    catch
                    {
                        return (hubNode, Array.Empty<ApsProject>());
                    }
                })
                .ToArray();

            (HubNodeViewModel HubNode, IReadOnlyList<ApsProject> Projects)[] projectSets = await Task.WhenAll(loadTasks);
            foreach ((HubNodeViewModel hubNode, IReadOnlyList<ApsProject> projects) in projectSets)
            {
                foreach (ApsProject project in projects.OrderBy(p => p.Name))
                {
                    var projectNode = new ProjectNodeViewModel(project);

                    // Hydrate from cache so 'Hide Empty' works immediately
                    var cached = _cacheStore.Get(project.HubId, project.Id);
                    if (cached != null)
                    {
                        projectNode.ModelCount = cached.Models.Count;
                        projectNode.ScanStatus = ScanStatus.Done;
                        projectNode.Versions.AddRange(cached.Models);
                    }

                    hubNode.Projects.Add(projectNode);
                }
            }

            TotalProjectCount   = HubNodes.Sum(h => h.Projects.Count);
            ScannedProjectCount = TotalProjectCount;
            ApplyHubFilter();
            RaiseProgressState();
        }

        // ── Navigation ────────────────────────────────────────────────────

        [RelayCommand]
        private async Task BrowseProject(ProjectNodeViewModel project)
        {
            _projectLoadCts?.Cancel();
            _projectLoadCts = new CancellationTokenSource();

            ActiveProject = project;
            SearchText    = "";
            IsHubsView   = false;
            IsModelsView = true;

            await EnsureProjectModelsLoadedAsync(project, _projectLoadCts.Token);
        }

        [RelayCommand]
        private void BackToHubs()
        {
            _projectLoadCts?.Cancel();
            IsHubsView   = true;
            IsModelsView = false;
            ActiveProject = null;
            SearchText    = "";
        }

        // ── Model cards ───────────────────────────────────────────────────

        private void BuildModelCards(ProjectNodeViewModel project)
        {
            _allModelCards.Clear();
            foreach (ApsVersion v in project.Versions)
                _allModelCards.Add(new ModelCardViewModel(v, RaiseSelectionState));

            OnPropertyChanged(nameof(AllSelected));
            ApplyModelFilter();
        }

        private void RaiseSelectionState()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(AddToQueueButtonText));
            OnPropertyChanged(nameof(CanAddToQueue));
            OnPropertyChanged(nameof(AllSelected));
        }

        /// Called by the view code-behind when the user clicks "Add to Queue".
        public void ConfirmSelection()
        {
            SelectedVersions.Clear();
            foreach (ModelCardViewModel card in _allModelCards.Where(c => c.IsSelected))
                SelectedVersions.Add(card.Version);
        }

        public bool? AllSelected
        {
            get
            {
                if (FilteredModels.Count == 0) return false;
                bool all = FilteredModels.All(m => m.IsSelected);
                bool any = FilteredModels.Any(m => m.IsSelected);
                if (all) return true;
                if (any) return null; // Indeterminate
                return false;
            }
            set
            {
                bool target = value ?? false;
                foreach (var model in FilteredModels)
                    model.IsSelected = target;
                RaiseSelectionState();
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var model in FilteredModels)
                model.IsSelected = true;
            RaiseSelectionState();
        }

        [RelayCommand]
        private void UnselectAll()
        {
            foreach (var model in _allModelCards)
                model.IsSelected = false;
            RaiseSelectionState();
        }

        // ── Sorting ───────────────────────────────────────────────────────

        [RelayCommand]
        private void SortBy(string column)
        {
            if (SortColumn == column) SortDescending = !SortDescending;
            else { SortColumn = column; SortDescending = column is "Date" or "Size"; }
            if (IsModelsView) ApplyModelFilter();
        }

        // ── Filtering ─────────────────────────────────────────────────────

        partial void OnSearchTextChanged(string value)
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            // Debounce for 150ms to keep typing fluid
            Task.Delay(150, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (IsModelsView) ApplyModelFilter();
                    else ApplyHubFilter();
                });
            }, token);
        }

        partial void OnFilterEmptyProjectsChanged(bool value) => ApplyHubFilter();

        private void ApplyHubFilter()
        {
            string filter = SearchText.Trim();

            foreach (HubNodeViewModel hub in HubNodes)
            {
                bool hubMatch = string.IsNullOrWhiteSpace(filter)
                    || hub.HubName.Contains(filter, StringComparison.OrdinalIgnoreCase);

                bool anyVisible = false;
                foreach (ProjectNodeViewModel proj in hub.Projects)
                {
                    bool nameMatch = string.IsNullOrWhiteSpace(filter)
                        || proj.ProjectName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || hubMatch;
                    bool passesEmpty = !FilterEmptyProjects
                        || proj.ScanStatus != ScanStatus.Done
                        || proj.ModelCount > 0;

                    proj.Visibility = nameMatch && passesEmpty
                        ? System.Windows.Visibility.Visible
                        : System.Windows.Visibility.Collapsed;

                    if (proj.Visibility == System.Windows.Visibility.Visible) anyVisible = true;
                }

                // Auto-expand when search reveals projects inside this hub
                if (!string.IsNullOrWhiteSpace(filter) && anyVisible)
                    hub.IsExpanded = true;
            }
        }

        private void ApplyModelFilter()
        {
            string filter = SearchText.Trim();
            IEnumerable<ModelCardViewModel> filtered = _allModelCards;

            if (!string.IsNullOrWhiteSpace(filter))
                filtered = filtered.Where(m =>
                    m.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || m.FolderPathDisplay.Contains(filter, StringComparison.OrdinalIgnoreCase));

            IEnumerable<ModelCardViewModel> sorted = SortColumn switch
            {
                "Date" => SortDescending
                    ? filtered.OrderByDescending(m => m.Version.LastModified ?? DateTime.MinValue)
                    : filtered.OrderBy(m => m.Version.LastModified ?? DateTime.MinValue),
                "Size" => SortDescending
                    ? filtered.OrderByDescending(m => m.Version.FileSizeBytes ?? 0)
                    : filtered.OrderBy(m => m.Version.FileSizeBytes ?? 0),
                _ => SortDescending
                    ? filtered.OrderByDescending(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                    : filtered.OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase),
            };

            FilteredModels.Clear();
            foreach (ModelCardViewModel card in sorted)
                FilteredModels.Add(card);

            RaiseSelectionState();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void RaiseProgressState()
        {
            OnPropertyChanged(nameof(IsScanning));
            OnPropertyChanged(nameof(ScanProgressText));
        }

        private async Task EnsureProjectModelsLoadedAsync(ProjectNodeViewModel project, CancellationToken cancellationToken)
        {
            // Always start completely fresh — never mix cache data with live results
            project.ScanStatus = ScanStatus.Scanning;
            project.Versions.Clear();
            project.ModelCount = 0;
            _allModelCards.Clear();
            FilteredModels.Clear();

            StatusMessage = $"Searching {project.ProjectName} for Revit models...";

            try
            {
                await _dmService.SearchRevitModelsInProjectAsync(
                    project.Project.HubId,
                    project.ProjectId,
                    newModels =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            project.Versions.AddRange(newModels);
                            project.ModelCount = project.Versions.Count;

                            foreach (var v in newModels)
                            {
                                var card = new ModelCardViewModel(v, RaiseSelectionState);
                                _allModelCards.Add(card);

                                if (MatchesFilter(card))
                                    FilteredModels.Add(card);
                            }

                            RaiseSelectionState();
                        });
                    },
                    cancellationToken: cancellationToken);

                project.ScanStatus = ScanStatus.Done;
                StatusMessage = project.ModelCount == 0
                    ? $"No Revit models found in {project.ProjectName}."
                    : $"Found {project.ModelCount} models. Type to filter instantly.";

                // Persist findings to cache (used by hub view for model counts)
                _cacheStore.Save(new CloudModelCacheEntry
                {
                    HubId = project.Project.HubId,
                    ProjectId = project.ProjectId,
                    ProjectName = project.ProjectName,
                    FetchedAtUtc = DateTime.UtcNow,
                    Models = project.Versions.ToList()
                });

                if (FilterEmptyProjects)
                    ApplyHubFilter();
            }
            catch (OperationCanceledException)
            {
                if (project.ScanStatus == ScanStatus.Scanning)
                    project.ScanStatus = ScanStatus.Pending;
            }
            catch (Exception ex)
            {
                project.ScanStatus = ScanStatus.Error;
                StatusMessage = $"Error loading Revit models: {ex.Message}";
            }
        }

        private bool MatchesFilter(ModelCardViewModel m)
        {
            string filter = SearchText.Trim();
            if (string.IsNullOrWhiteSpace(filter)) return true;

            return m.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || m.FolderPathDisplay.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
