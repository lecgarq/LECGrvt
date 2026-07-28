using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.ViewModels
{
    public partial class PbrMaterialCreatorViewModel : BaseViewModel
    {
        private readonly IMaterialTextureLookupService? _textureLookup;

        public ObservableCollection<MaterialPageViewModel> MaterialPages { get; } = new();

        [ObservableProperty]
        private MaterialPageViewModel? _selectedPage;

        [ObservableProperty]
        private string _importSummary = string.Empty;

        public bool CanRun => MaterialPages.Count > 0 && MaterialPages.All(p => p.CanRun);

        public bool CanRemovePage => MaterialPages.Count > 1;

        public PbrMaterialCreatorViewModel() : this(null) { }

        public PbrMaterialCreatorViewModel(IMaterialTextureLookupService? textureLookup)
        {
            _textureLookup = textureLookup;
            Title = "PBR MATERIAL CREATOR";
            MaterialPages.CollectionChanged += OnPagesCollectionChanged;
            AddPage();
        }

        [RelayCommand]
        private void AddPage()
        {
            var page = new MaterialPageViewModel(MaterialPages.Count + 1, _textureLookup);
            page.CanRunNotifier = () => OnPropertyChanged(nameof(CanRun));
            MaterialPages.Add(page);
            SelectedPage = page;
        }

        [RelayCommand]
        private void ImportFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select root folder containing material subfolders"
            };

            if (dialog.ShowDialog() == true)
            {
                ImportFolder(dialog.FolderName);
            }
        }

        public void ImportFolder(string rootFolder)
        {
            if (_textureLookup == null || string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                ImportSummary = "No material folders loaded.";
                return;
            }

            IReadOnlyList<PbrMaterialFolderCandidate> candidates = _textureLookup.ScanImmediateMaterialFolders(rootFolder);
            List<PbrMaterialFolderCandidate> validCandidates = candidates.Where(candidate => candidate.IsValid).ToList();
            int skippedCount = candidates.Count - validCandidates.Count;

            if (validCandidates.Count == 0)
            {
                ImportSummary = FormatImportSummary(0, skippedCount);
                return;
            }

            if (MaterialPages.Count == 1 && IsPageEmpty(MaterialPages[0]))
            {
                MaterialPages[0].CanRunNotifier = null;
                MaterialPages.Clear();
            }

            int firstImportedIndex = MaterialPages.Count;
            foreach (PbrMaterialFolderCandidate candidate in validCandidates)
            {
                MaterialPages.Add(CreatePageFromCandidate(candidate));
            }

            RenumberPages();
            SelectedPage = MaterialPages[firstImportedIndex];
            ImportSummary = FormatImportSummary(validCandidates.Count, skippedCount);
        }

        [RelayCommand]
        private void RemovePage(MaterialPageViewModel? page)
        {
            if (page == null || MaterialPages.Count <= 1)
            {
                return;
            }

            int index = MaterialPages.IndexOf(page);
            page.CanRunNotifier = null;
            MaterialPages.Remove(page);

            for (int i = 0; i < MaterialPages.Count; i++)
            {
                MaterialPages[i].PageNumber = i + 1;
            }

            if (MaterialPages.Count > 0)
            {
                SelectedPage = MaterialPages[Math.Min(index, MaterialPages.Count - 1)];
            }
        }

        public List<PbrMaterialCreateRequest> CreateRequests()
        {
            return MaterialPages.Select(p => p.CreateRequest()).ToList();
        }

        private void OnPagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(CanRemovePage));
        }

        private MaterialPageViewModel CreatePageFromCandidate(PbrMaterialFolderCandidate candidate)
        {
            var page = new MaterialPageViewModel(MaterialPages.Count + 1, _textureLookup)
            {
                MaterialName = candidate.MaterialName,
                FolderPath = candidate.FolderPath,
                DiffusePath = candidate.DiffusePath ?? string.Empty,
                RoughnessPath = candidate.RoughnessPath ?? string.Empty,
                NormalPath = candidate.NormalPath ?? string.Empty,
                DetectedCount = candidate.DetectedCount
            };

            page.CanRunNotifier = () => OnPropertyChanged(nameof(CanRun));
            return page;
        }

        private void RenumberPages()
        {
            for (int i = 0; i < MaterialPages.Count; i++)
            {
                MaterialPages[i].PageNumber = i + 1;
            }
        }

        private static bool IsPageEmpty(MaterialPageViewModel page)
        {
            return string.IsNullOrWhiteSpace(page.MaterialName) &&
                string.IsNullOrWhiteSpace(page.Description) &&
                string.IsNullOrWhiteSpace(page.MaterialClass) &&
                string.IsNullOrWhiteSpace(page.FolderPath) &&
                string.IsNullOrWhiteSpace(page.DiffusePath) &&
                string.IsNullOrWhiteSpace(page.RoughnessPath) &&
                string.IsNullOrWhiteSpace(page.NormalPath);
        }

        private static string FormatImportSummary(int loadedCount, int skippedCount)
        {
            string loadedText = loadedCount == 1 ? "Loaded 1 material folder." : $"Loaded {loadedCount} material folders.";
            return skippedCount == 0 ? loadedText : $"{loadedText} Skipped {skippedCount}.";
        }
    }
}
