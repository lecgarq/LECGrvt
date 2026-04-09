using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;

namespace LECG.Batch.ViewModels
{
    public partial class CloudModelBrowserViewModel : ObservableObject
    {
        private readonly IApsDataManagementService _dmService;

        public ObservableCollection<ApsHub> Hubs { get; } = new();
        public ObservableCollection<ApsProject> Projects { get; } = new();
        public ObservableCollection<ApsFolderItem> FolderItems { get; } = new();
        public ObservableCollection<ApsVersion> Versions { get; } = new();
        public ObservableCollection<ApsVersion> SelectedVersions { get; } = new();

        [ObservableProperty]
        private ApsHub? _selectedHub;

        [ObservableProperty]
        private ApsProject? _selectedProject;

        [ObservableProperty]
        private ApsFolderItem? _selectedFolder;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "";

        public CloudModelBrowserViewModel(IApsDataManagementService dmService)
        {
            _dmService = dmService;
        }

        [RelayCommand]
        private async Task LoadHubsAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading hubs...";
            Hubs.Clear();
            try
            {
                IReadOnlyList<ApsHub> hubs = await _dmService.GetHubsAsync();
                foreach (ApsHub h in hubs) Hubs.Add(h);
                StatusMessage = $"{hubs.Count} hub(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        partial void OnSelectedHubChanged(ApsHub? value)
        {
            if (value != null)
                _ = LoadProjectsAsync(value);
        }

        private async Task LoadProjectsAsync(ApsHub hub)
        {
            IsLoading = true;
            StatusMessage = "Loading projects...";
            Projects.Clear();
            FolderItems.Clear();
            Versions.Clear();
            try
            {
                IReadOnlyList<ApsProject> projects = await _dmService.GetProjectsAsync(hub.Id);
                foreach (ApsProject p in projects) Projects.Add(p);
                StatusMessage = $"{projects.Count} project(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        partial void OnSelectedProjectChanged(ApsProject? value)
        {
            if (value != null && SelectedHub != null)
                _ = LoadTopFoldersAsync(SelectedHub, value);
        }

        private async Task LoadTopFoldersAsync(ApsHub hub, ApsProject project)
        {
            IsLoading = true;
            StatusMessage = "Loading folders...";
            FolderItems.Clear();
            Versions.Clear();
            try
            {
                IReadOnlyList<ApsFolderItem> folders = await _dmService.GetTopFoldersAsync(hub.Id, project.Id);
                foreach (ApsFolderItem f in folders) FolderItems.Add(f);
                StatusMessage = $"{folders.Count} folder(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        partial void OnSelectedFolderChanged(ApsFolderItem? value)
        {
            if (value != null && SelectedProject != null)
                _ = LoadFolderContentsAsync(SelectedProject, value);
        }

        private async Task LoadFolderContentsAsync(ApsProject project, ApsFolderItem folder)
        {
            IsLoading = true;
            StatusMessage = "Loading items...";
            FolderItems.Clear();
            Versions.Clear();
            try
            {
                IReadOnlyList<ApsFolderItem> items = await _dmService.GetFolderContentsAsync(project.Id, folder.Id);
                foreach (ApsFolderItem item in items) FolderItems.Add(item);
                StatusMessage = $"{items.Count} item(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task LoadVersionsAsync(ApsFolderItem item)
        {
            if (SelectedProject == null) return;
            IsLoading = true;
            StatusMessage = "Loading versions...";
            Versions.Clear();
            try
            {
                IReadOnlyList<ApsVersion> versions = await _dmService.GetVersionsAsync(SelectedProject.Id, item.Id);
                foreach (ApsVersion v in versions.Where(_dmService.IsRevitCloudModel))
                    Versions.Add(v);
                StatusMessage = $"{Versions.Count} Revit version(s) found.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        public void AddSelectedToQueue(ApsVersion version)
        {
            if (!SelectedVersions.Contains(version))
                SelectedVersions.Add(version);
        }

        public void RemoveFromQueue(ApsVersion version) =>
            SelectedVersions.Remove(version);
    }
}
