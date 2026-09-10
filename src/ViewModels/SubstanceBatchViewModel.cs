using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;

namespace LECG.ViewModels
{
    public partial class SubstanceBatchViewModel : BaseViewModel
    {
        private readonly IMetallicProbeService? _probe;
        private ISet<string> _existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _loadingLibrary;

        public ObservableCollection<SubstanceMaterialRowViewModel> Rows { get; } = new();
        public ObservableCollection<SubstanceCategoryViewModel> Categories { get; } = new();
        public ICollectionView RowsView { get; }

        public IReadOnlyList<int> ResolutionChoices { get; } = new[] { 2048, 4096 };

        [ObservableProperty] private string _libraryRoot = string.Empty;
        [ObservableProperty] private string _outputRoot = string.Empty;
        [ObservableProperty] private int _targetSize = 2048;
        [ObservableProperty] private double _sizeMillimeters = 2500;
        [ObservableProperty] private bool _overwriteExisting;
        [ObservableProperty] private bool _forceRebake;
        [ObservableProperty] private string _filterText = string.Empty;
        [ObservableProperty] private string _scanSummary = string.Empty;
        [ObservableProperty] private IReadOnlyList<string> _warnings = Array.Empty<string>();

        public int SelectedCount => Rows.Count(r => r.IsSelected);
        public int SelectedInDocumentCount => Rows.Count(r => r.IsSelected && r.ExistsInDocument);
        public int EstimatedBakeMegabytes => (int)Math.Round(Rows.Count(r => r.IsSelected) * 16.0 * Math.Pow(TargetSize / 2048.0, 2));
        public string Footer => $"{SelectedCount} selected · {SelectedInDocumentCount} already in document · ~{EstimatedBakeMegabytes} MB to bake";
        public bool CanRun => SelectedCount > 0 && SizeMillimeters > 0 && Directory.Exists(LibraryRoot);

        public SubstanceBatchViewModel() : this(null) { }

        public SubstanceBatchViewModel(IMetallicProbeService? probe)
        {
            _probe = probe;
            Title = "SUBSTANCE BATCH MATERIALS";
            RowsView = CollectionViewSource.GetDefaultView(Rows);
            RowsView.Filter = o => o is SubstanceMaterialRowViewModel r && SubstanceSelectionPolicy.Matches(r.ToState(), FilterText);

            _loadingLibrary = true;
            try
            {
                var s = SettingsManager.Load<SubstanceBatchSettings>(SubstanceBatchSettings.FileName);
                LibraryRoot = s.LibraryRoot;
                OutputRoot = string.IsNullOrWhiteSpace(s.OutputRoot) ? BakeOutputPaths.DefaultOutputRoot(s.LibraryRoot) : s.OutputRoot;
                TargetSize = s.TargetSize;
                SizeMillimeters = s.SizeMillimeters;
                OverwriteExisting = s.OverwriteExisting;
                ForceRebake = s.ForceRebake;
            }
            finally
            {
                _loadingLibrary = false;
            }

            Rescan();
        }

        public void SetExistingNames(ISet<string> names)
        {
            ArgumentNullException.ThrowIfNull(names);
            _existingNames = names;
            foreach (var r in Rows) r.ExistsInDocument = names.Contains(r.DisplayName);
            NotifyCounts();
        }

        public IReadOnlyList<SubstanceMaterialEntry> SelectedEntries() =>
            Rows.Where(r => r.IsSelected).Select(r => r.Entry).ToList();

        public SubstanceBatchSettings CurrentSettings() => new()
        {
            LibraryRoot = LibraryRoot,
            OutputRoot = OutputRoot,
            TargetSize = TargetSize,
            SizeMillimeters = SizeMillimeters,
            OverwriteExisting = OverwriteExisting,
            ForceRebake = ForceRebake,
        };

        public override void Apply()
        {
            if (!CanRun) return;
            SettingsManager.Save(CurrentSettings(), SubstanceBatchSettings.FileName);
            base.Apply();
        }

        [RelayCommand]
        private void BrowseLibrary()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Substance bake library root" };
            if (dialog.ShowDialog() == true)
            {
                LoadLibrary(dialog.FolderName);
            }
        }

        public void LoadLibrary(string root)
        {
            ArgumentNullException.ThrowIfNull(root);
            _loadingLibrary = true;
            try
            {
                LibraryRoot = root;
                OutputRoot = BakeOutputPaths.DefaultOutputRoot(root);
                SizeMillimeters = 2500;
            }
            finally
            {
                _loadingLibrary = false;
            }
            Rescan();
        }

        [RelayCommand]
        private void BrowseOutput()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select baked texture output folder" };
            if (dialog.ShowDialog() == true) OutputRoot = dialog.FolderName;
        }

        [RelayCommand]
        private void SelectAll() => ApplyStates(SubstanceSelectionPolicy.SetAll(States(), true));

        [RelayCommand]
        private void SelectNone() => ApplyStates(SubstanceSelectionPolicy.SetAll(States(), false));

        [RelayCommand]
        private void Rescan()
        {
            Rows.Clear();
            Categories.Clear();

            SubstanceScanResult scan = SubstanceLibraryScanner.Scan(LibraryRoot);
            foreach (SubstanceMaterialEntry e in scan.Entries)
            {
                var row = new SubstanceMaterialRowViewModel(e, _probe)
                {
                    ExistsInDocument = _existingNames.Contains(e.DisplayName),
                };
                row.SelectionChanged = OnRowSelectionChanged;
                Rows.Add(row);
            }

            foreach (var (category, count) in SubstanceSelectionPolicy.CategoryCounts(States()))
            {
                var cat = new SubstanceCategoryViewModel(category, count, SubstanceSelectionPolicy.CategoryState(States(), category));
                cat.CheckedByUser = (name, selected) => ApplyStates(SubstanceSelectionPolicy.SetCategory(States(), name, selected));
                Categories.Add(cat);
            }

            Warnings = scan.Warnings;
            ScanSummary = scan.Warnings.Count == 0
                ? $"{scan.Entries.Count} materials in {Categories.Count} categories"
                : $"{scan.Entries.Count} materials in {Categories.Count} categories · {scan.Warnings.Count} skipped (see log)";
            NotifyCounts();
        }

        partial void OnFilterTextChanged(string value) => RowsView.Refresh();
        partial void OnSizeMillimetersChanged(double value) => OnPropertyChanged(nameof(CanRun));
        partial void OnTargetSizeChanged(int value) => OnPropertyChanged(nameof(Footer));

        partial void OnLibraryRootChanged(string value)
        {
            OnPropertyChanged(nameof(CanRun));
            if (string.IsNullOrWhiteSpace(OutputRoot))
            {
                OutputRoot = BakeOutputPaths.DefaultOutputRoot(value);
            }
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value) && !_loadingLibrary)
            {
                Rescan();
            }
        }

        private List<SubstanceRowState> States() => Rows.Select(r => r.ToState()).ToList();

        private void ApplyStates(IReadOnlyList<SubstanceRowState> states)
        {
            var bySlug = states.ToDictionary(s => s.Category + "/" + s.Slug, s => s.IsSelected, StringComparer.OrdinalIgnoreCase);
            foreach (var r in Rows)
            {
                if (bySlug.TryGetValue(r.Category + "/" + r.Entry.Slug, out bool sel) && r.IsSelected != sel)
                {
                    r.SelectionChanged = null;
                    r.IsSelected = sel;
                    r.SelectionChanged = OnRowSelectionChanged;
                }
            }
            RefreshCategoryStates();
            NotifyCounts();
        }

        private void OnRowSelectionChanged()
        {
            RefreshCategoryStates();
            NotifyCounts();
        }

        private void RefreshCategoryStates()
        {
            var states = States();
            foreach (var c in Categories) c.SetFromRows(SubstanceSelectionPolicy.CategoryState(states, c.Name));
        }

        private void NotifyCounts()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedInDocumentCount));
            OnPropertyChanged(nameof(Footer));
            OnPropertyChanged(nameof(CanRun));
        }
    }
}
