using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels.Components;

namespace LECG.ViewModels
{
    public partial class SearchReplaceViewModel : BaseViewModel
    {
        private ISearchReplaceService? _service;
        private Document? _doc;
        private List<ElementData> _cachedElements = new List<ElementData>();
        private bool _isSettingScope;
        private CancellationTokenSource? _searchCts;

        // R5: keys of rows the USER explicitly unchecked, remembered for the life of
        // the dialog. Held across rebuilds so a filter round-trip cannot silently
        // re-arm a row that was deliberately excluded — re-arming is the dangerous
        // direction for a rename.
        private readonly HashSet<string> _uncheckedKeys = new HashSet<string>(StringComparer.Ordinal);

        // R12: CollectBaseElements is pure Revit API and cannot leave the UI thread, so
        // the only way to make a scope click cheap is to not repeat it. Keyed by the
        // nine scope flags. Not invalidated mid-dialog: the window is modal
        // (SearchReplaceCommand.cs:31 ShowDialog), so the document cannot change under it.
        private readonly Dictionary<string, List<ElementData>> _scopeCache = new Dictionary<string, List<ElementData>>(StringComparer.Ordinal);

        private string _busyMessage = string.Empty;
        public string BusyMessage { get => _busyMessage; set => SetProperty(ref _busyMessage, value); }

        public ReplaceRule ReplaceRule { get; } = new ReplaceRule();
        public RemoveRule RemoveRule { get; } = new RemoveRule();
        public AddRule AddRule { get; } = new AddRule();
        public NumberingRule NumberingRule { get; } = new NumberingRule();
        public CaseRule CaseRule { get; } = new CaseRule();

        private bool _scopeTypeName = true;
        public bool ScopeTypeName { get => _scopeTypeName; set { if (SetProperty(ref _scopeTypeName, value)) { if (value) SetExclusiveScope(() => _scopeTypeName = true); } } }

        private bool _scopeFamilyName;
        public bool ScopeFamilyName { get => _scopeFamilyName; set { if (SetProperty(ref _scopeFamilyName, value)) { if (value) SetExclusiveScope(() => _scopeFamilyName = true); } } }

        private bool _scopeViewName;
        public bool ScopeViewName { get => _scopeViewName; set { if (SetProperty(ref _scopeViewName, value)) { if (value) SetExclusiveScope(() => _scopeViewName = true); } } }

        private bool _scopeSheetName;
        public bool ScopeSheetName { get => _scopeSheetName; set { if (SetProperty(ref _scopeSheetName, value)) { if (value) SetExclusiveScope(() => _scopeSheetName = true); } } }

        private bool _scopeMaterialName;
        public bool ScopeMaterialName { get => _scopeMaterialName; set { if (SetProperty(ref _scopeMaterialName, value)) { if (value) SetExclusiveScope(() => _scopeMaterialName = true); } } }

        private bool _scopeObjectStyleName;
        public bool ScopeObjectStyleName { get => _scopeObjectStyleName; set { if (SetProperty(ref _scopeObjectStyleName, value)) { if (value) SetExclusiveScope(() => _scopeObjectStyleName = true); } } }

        private bool _scopeLineStyleName;
        public bool ScopeLineStyleName { get => _scopeLineStyleName; set { if (SetProperty(ref _scopeLineStyleName, value)) { if (value) SetExclusiveScope(() => _scopeLineStyleName = true); } } }

        private bool _scopeFillPatternName;
        public bool ScopeFillPatternName { get => _scopeFillPatternName; set { if (SetProperty(ref _scopeFillPatternName, value)) { if (value) SetExclusiveScope(() => _scopeFillPatternName = true); } } }

        private bool _scopeFamilyParameterName;
        public bool ScopeFamilyParameterName { get => _scopeFamilyParameterName; set { if (SetProperty(ref _scopeFamilyParameterName, value)) { if (value) SetExclusiveScope(() => _scopeFamilyParameterName = true); } } }

        private string _filterName = "";
        public string FilterName { get => _filterName; set { if (SetProperty(ref _filterName, value)) _ = UpdatePreviewAsync(); } }

        private string _filterCategory = "All";
        public string FilterCategory
        {
            get => _filterCategory;
            set
            {
                if (SetProperty(ref _filterCategory, value))
                {
                    // FilterCategory now lives on the ICollectionView (Plan 03-05) — no
                    // re-run of ProcessPreview, just refresh the view's predicate.
                    _previewView?.Refresh();
                }
            }
        }

        private SearchFilterType _selectedFilterType = SearchFilterType.Contains;
        public SearchFilterType SelectedFilterType { get => _selectedFilterType; set { if (SetProperty(ref _selectedFilterType, value)) _ = UpdatePreviewAsync(); } }

        private string _filterParamGroup = "All";
        public string FilterParamGroup { get => _filterParamGroup; set { if (SetProperty(ref _filterParamGroup, value)) _ = UpdatePreviewAsync(); } }

        private ObservableCollection<string> _availableParamGroups = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableParamGroups { get => _availableParamGroups; set => SetProperty(ref _availableParamGroups, value); }

        private int _filterIsInstanceIndex = 0;
        public int FilterIsInstanceIndex { get => _filterIsInstanceIndex; set { if (SetProperty(ref _filterIsInstanceIndex, value)) _ = UpdatePreviewAsync(); } }

        private int _filterIsReadOnlyIndex = 0;
        public int FilterIsReadOnlyIndex { get => _filterIsReadOnlyIndex; set { if (SetProperty(ref _filterIsReadOnlyIndex, value)) _ = UpdatePreviewAsync(); } }

        public bool? FilterIsInstance => _filterIsInstanceIndex == 1 ? true : (_filterIsInstanceIndex == 2 ? false : (bool?)null);
        public bool? FilterIsReadOnly => _filterIsReadOnlyIndex == 1 ? true : (_filterIsReadOnlyIndex == 2 ? false : (bool?)null);

        private string _filterViewType = "All";
        public string FilterViewType { get => _filterViewType; set { if (SetProperty(ref _filterViewType, value)) _ = UpdatePreviewAsync(); } }

        private ObservableCollection<string> _availableViewTypes = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableViewTypes { get => _availableViewTypes; set => SetProperty(ref _availableViewTypes, value); }

        private string _validationMessage = string.Empty;
        public string ValidationMessage { get => _validationMessage; set { if (SetProperty(ref _validationMessage, value)) OnPropertyChanged(nameof(HasValidationMessage)); } }

        public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand ReplaceSpacesCommand { get; }
        public ICommand ReplaceSpacesInReplaceTextCommand { get; }

        private BulkObservableCollection<ElementRowViewModel> _previewItems = new BulkObservableCollection<ElementRowViewModel>();
        public BulkObservableCollection<ElementRowViewModel> PreviewItems { get => _previewItems; set => SetProperty(ref _previewItems, value); }

        // Plan 03-05: ICollectionView wraps PreviewItems with default Category-ascending
        // sort and an AND-combined filter (FilterCategory dropdown ∧ per-column filters).
        // The XAML DataGrid binds to PreviewView (not PreviewItems) so WPF honours the
        // SortDescriptions + Filter automatically.
        private ICollectionView? _previewView;
        public ICollectionView PreviewView
        {
            get
            {
                if (_previewView == null)
                {
                    _previewView = CollectionViewSource.GetDefaultView(PreviewItems);
                    _previewView.SortDescriptions.Add(new SortDescription(
                        nameof(ElementRowViewModel.Category),
                        ListSortDirection.Ascending));
                    _previewView.Filter = item => MatchesAllFilters((ElementRowViewModel)item);
                }
                return _previewView;
            }
        }

        // Per-column filter predicates (column key → predicate). AND-combined with the
        // top-of-grid FilterCategory dropdown by MatchesAllFilters.
        private readonly Dictionary<string, Predicate<ElementRowViewModel>> _columnFilters = new();

        /// <summary>
        /// Set or clear a per-column filter predicate. Pass <c>null</c> to remove.
        /// Refreshes <see cref="PreviewView"/> so the DataGrid re-evaluates.
        /// </summary>
        public void SetColumnFilter(string columnKey, Predicate<ElementRowViewModel>? predicate)
        {
            if (string.IsNullOrEmpty(columnKey)) return;
            if (predicate == null) _columnFilters.Remove(columnKey);
            else _columnFilters[columnKey] = predicate;
            _previewView?.Refresh();
        }

        private bool MatchesAllFilters(ElementRowViewModel row)
        {
            // 1. Top-of-grid FilterCategory dropdown
            if (!string.IsNullOrEmpty(FilterCategory)
                && !FilterCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (!row.Category.Contains(FilterCategory, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 2. AND-combined per-column predicates
            foreach (var p in _columnFilters.Values)
            {
                if (!p(row)) return false;
            }
            return true;
        }

        private ObservableCollection<string> _availableCategories = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableCategories { get => _availableCategories; set => SetProperty(ref _availableCategories, value); }

        public SearchReplaceViewModel()
        {
            ReplaceRule.PropertyChanged += RuleChanged;
            RemoveRule.PropertyChanged += RuleChanged;
            AddRule.PropertyChanged += RuleChanged;
            NumberingRule.PropertyChanged += RuleChanged;
            CaseRule.PropertyChanged += RuleChanged;
            Title = "Batch Rename";

            SelectAllCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectAll);
            SelectNoneCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectNone);
            ReplaceSpacesCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ReplaceSpaces);
            ReplaceSpacesInReplaceTextCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ReplaceSpacesInReplaceText);
        }

        private void SelectAll() { if (PreviewItems != null) foreach (var item in PreviewItems) item.IsChecked = true; }
        private void SelectNone() { if (PreviewItems != null) foreach (var item in PreviewItems) item.IsChecked = false; }
        private void ReplaceSpaces() { if (!string.IsNullOrEmpty(FilterName)) FilterName = FilterName.Replace(" ", "_"); }
        private void ReplaceSpacesInReplaceText() { if (ReplaceRule != null && !string.IsNullOrEmpty(ReplaceRule.ReplaceText)) ReplaceRule.ReplaceText = ReplaceRule.ReplaceText.Replace(" ", "_"); }

        public void Initialize(ISearchReplaceService service, Document doc)
        {
            _service = service;
            _doc = doc;
            _scopeCache.Clear();
            RefreshScope();
        }

        public RenameRuleContext ToContext() => new RenameRuleContext(ReplaceRule, RemoveRule, AddRule, NumberingRule, CaseRule, ScopeTypeName, ScopeFamilyName, ScopeViewName, ScopeSheetName, ScopeMaterialName, ScopeObjectStyleName, ScopeLineStyleName, ScopeFillPatternName, ScopeFamilyParameterName, FilterName, FilterCategory, SelectedFilterType, FilterParamGroup, FilterIsInstance, FilterIsReadOnly, FilterViewType);
        public SearchCriteria ToCriteria() => new SearchCriteria { FilterName = FilterName, FilterCategory = FilterCategory, SelectedFilterType = SelectedFilterType, FilterParamGroup = FilterParamGroup, FilterIsInstance = FilterIsInstance, FilterIsReadOnly = FilterIsReadOnly, FilterViewType = FilterViewType, ScopeTypeName = ScopeTypeName, ScopeFamilyName = ScopeFamilyName, ScopeViewName = ScopeViewName, ScopeSheetName = ScopeSheetName, ScopeMaterialName = ScopeMaterialName, ScopeObjectStyleName = ScopeObjectStyleName, ScopeLineStyleName = ScopeLineStyleName, ScopeFillPatternName = ScopeFillPatternName, ScopeFamilyParameterName = ScopeFamilyParameterName };

        private void RuleChanged(object? sender, PropertyChangedEventArgs e) => _ = UpdatePreviewAsync();

        private void SetExclusiveScope(Action activator)
        {
            if (_isSettingScope) return;
            _isSettingScope = true;
            _scopeTypeName = _scopeFamilyName = _scopeViewName = _scopeSheetName = _scopeMaterialName = _scopeObjectStyleName = _scopeLineStyleName = _scopeFillPatternName = _scopeFamilyParameterName = false;
            activator();
            _isSettingScope = false;
            OnPropertyChanged(nameof(ScopeTypeName)); OnPropertyChanged(nameof(ScopeFamilyName)); OnPropertyChanged(nameof(ScopeViewName)); OnPropertyChanged(nameof(ScopeSheetName)); OnPropertyChanged(nameof(ScopeMaterialName)); OnPropertyChanged(nameof(ScopeObjectStyleName)); OnPropertyChanged(nameof(ScopeLineStyleName)); OnPropertyChanged(nameof(ScopeFillPatternName)); OnPropertyChanged(nameof(ScopeFamilyParameterName));
            OnPropertyChanged(nameof(IsParameterScope)); OnPropertyChanged(nameof(IsViewScope));
            RefreshScope();
        }

        public bool IsParameterScope => ScopeFamilyParameterName;
        public bool IsViewScope => ScopeViewName;

        /// <summary>
        /// Identity of the current scope selection, used as the cache key.
        /// Built from all nine flags rather than assuming exactly one is set, so it stays
        /// correct if the scope control ever becomes genuinely multi-select.
        /// </summary>
        /// <remarks>
        /// The key is built by a pure static so it can be unit-tested. Reading it through
        /// the scope properties cannot be: setting one runs SetExclusiveScope -> RefreshScope,
        /// whose body references ISearchReplaceService, and resolving that interface loads
        /// RevitAPI. The JIT does that before the null guard runs, so the test runner throws
        /// FileNotFoundException no matter what the guard says.
        /// </remarks>
        internal static string BuildScopeKey(
            bool types, bool families, bool views, bool sheets, bool materials,
            bool objectStyles, bool lineStyles, bool fillPatterns, bool familyParameters)
            => string.Concat(
                types ? "T" : "-",
                families ? "F" : "-",
                views ? "V" : "-",
                sheets ? "S" : "-",
                materials ? "M" : "-",
                objectStyles ? "O" : "-",
                lineStyles ? "L" : "-",
                fillPatterns ? "P" : "-",
                familyParameters ? "R" : "-");

        private string ScopeKey => BuildScopeKey(
            ScopeTypeName, ScopeFamilyName, ScopeViewName, ScopeSheetName, ScopeMaterialName,
            ScopeObjectStyleName, ScopeLineStyleName, ScopeFillPatternName, ScopeFamilyParameterName);

        /// <summary>
        /// Returns the element set for the current scope, collecting it only on a cache miss.
        /// </summary>
        /// <remarks>
        /// The collect runs synchronously on the UI thread and must: CollectBaseElements is
        /// FilteredElementCollector plus a per-element label read
        /// (<c>BaseElementCollectionService.cs:37-59</c>), and the Revit API is main-thread
        /// only. Wrapping it in <c>Task.Run</c> is the crash that does not reproduce until it
        /// does. So the wait is made visible instead of hidden, and paid once per scope.
        /// </remarks>
        private List<ElementData> CollectForCurrentScope()
        {
            string key = ScopeKey;
            if (_scopeCache.TryGetValue(key, out List<ElementData>? cached)) return cached;

            IsBusy = true;
            BusyMessage = "Collecting elements…";

            // Force a paint before the collector blocks the thread, otherwise the busy panel
            // never appears. Render (7) outranks Input (5), so the overlay draws and no click
            // is processed meanwhile — a repaint without the reentrancy Dispatcher.Yield()
            // would open. Null-guarded: the test runner has no WPF Application.
            System.Windows.Application.Current?.Dispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Render);

            try
            {
                List<ElementData> collected = _service!.CollectBaseElements(
                    _doc!, ScopeTypeName, ScopeFamilyName, ScopeViewName, ScopeSheetName,
                    ScopeMaterialName, ScopeObjectStyleName, ScopeLineStyleName,
                    ScopeFillPatternName, ScopeFamilyParameterName);
                _scopeCache[key] = collected;
                return collected;
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private void RefreshScope()
        {
            if (_service == null || _doc == null) return;
            _cachedElements = CollectForCurrentScope();
            var cats = _service.GetUniqueCategories(_cachedElements);
            AvailableCategories.Clear(); AvailableCategories.Add("All"); foreach (var c in cats) AvailableCategories.Add(c);
            FilterCategory = "All";

            if (ScopeFamilyParameterName)
            {
                var groups = _cachedElements.Where(e => e.Type == "FamilyParameter" && !string.IsNullOrEmpty(e.ParamGroup)).Select(e => e.ParamGroup).Distinct().OrderBy(g => g);
                AvailableParamGroups.Clear(); AvailableParamGroups.Add("All"); foreach (var g in groups) AvailableParamGroups.Add(g);
                FilterParamGroup = "All";
            }

            if (ScopeViewName)
            {
                var viewTypes = _cachedElements.Where(e => e.Type == "View" && !string.IsNullOrEmpty(e.Category)).Select(e => e.Category).Distinct().OrderBy(t => t);
                AvailableViewTypes.Clear(); AvailableViewTypes.Add("All"); foreach (var t in viewTypes) AvailableViewTypes.Add(t);
                FilterViewType = "All";
            }

            _ = UpdatePreviewAsync();
        }

        /// <summary>
        /// Identity of a preview row, stable across rebuilds.
        /// </summary>
        /// <remarks>
        /// <c>Id</c> alone is not unique: FamilyParameter rows carry the parent family's
        /// id, so every parameter of one family shares it
        /// (<c>BaseElementCollectionService.cs:264</c>). <c>OriginalValue</c> is the source
        /// name and does not change when a rule rewrites <c>NewValue</c>.
        /// </remarks>
        internal static string CheckKey(ElementRowViewModel row)
            => $"{row.Type}|{row.Id}|{row.OriginalValue}";

        /// <summary>
        /// Records the current rows' check state into <see cref="_uncheckedKeys"/>.
        /// Call immediately before replacing the rows.
        /// </summary>
        /// <remarks>
        /// Non-renameable rows are skipped: ProcessPreview unchecks those itself
        /// (<c>SearchReplacePreviewService.cs:157,230</c>), so their state is the service's
        /// decision, not the user's, and must not be remembered as one.
        /// </remarks>
        internal void HarvestCheckState()
        {
            foreach (ElementRowViewModel row in PreviewItems)
            {
                if (!row.IsRenameable) continue;

                string key = CheckKey(row);
                if (row.IsChecked) _uncheckedKeys.Remove(key);
                else _uncheckedKeys.Add(key);
            }
        }

        /// <summary>
        /// Re-applies remembered deselections to freshly built rows.
        /// Call immediately after replacing the rows.
        /// </summary>
        /// <remarks>
        /// Only ever clears a check — never sets one. ProcessPreview unchecks rows it marks
        /// non-renameable, and an <c>else</c> branch here would resurrect them.
        /// </remarks>
        internal void ApplyCheckState(IEnumerable<ElementRowViewModel> rows)
        {
            if (rows == null) return;

            foreach (ElementRowViewModel row in rows)
            {
                if (_uncheckedKeys.Contains(CheckKey(row))) row.IsChecked = false;
            }
        }

        private async Task UpdatePreviewAsync()
        {
            if (_service == null || _cachedElements == null) return;
            _searchCts?.Cancel(); _searchCts = new CancellationTokenSource(); var ct = _searchCts.Token;
            try
            {
                await Task.Delay(150, ct);
                var results = await Task.Run(() => _service.ProcessPreview(_cachedElements, ToCriteria(), ToContext(), ct), ct);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ct.IsCancellationRequested) return;
                    ValidationMessage = string.Empty;
                    // Remember what the user had unchecked before the rows are thrown away.
                    HarvestCheckState();
                    // ponytail: ProcessPreview computes cross-batch name collisions from
                    // row.IsChecked (SearchReplacePreviewService.cs:212-213) BEFORE these
                    // deselections are restored, so an unchecked row still claims its name
                    // and can mark another row as colliding. Display-only — execution
                    // renames checked rows only. Proper fix is to pass the memory into
                    // ProcessPreview, which is out of scope for phase 1.
                    ApplyCheckState(results);
                    // One Reset instead of one notification per row: the bound
                    // ICollectionView re-filters and re-sorts once, not N times.
                    PreviewItems.ReplaceAll(results);
                    // Materialize/refresh the ICollectionView so sort + filter apply.
                    _ = PreviewView; // ensure created
                    _previewView?.Refresh();
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ValidationMessage = $"Error loading preview: {ex.Message}"; }
        }

        public override void Apply() { if (PreviewItems == null || !PreviewItems.Any(i => i.IsChecked)) { ValidationMessage = "Select at least one item to rename."; return; } base.Apply(); }
    }
}
