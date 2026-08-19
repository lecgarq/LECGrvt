using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
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
        public string FilterName { get => _filterName; set { if (SetProperty(ref _filterName, value)) { OnPropertyChanged(nameof(HasActiveFilters)); OnPropertyChanged(nameof(ActiveFilterSummary)); _ = UpdatePreviewAsync(); } } }

        private string _filterCategory = "All";

        /// <summary>
        /// Single-category shim kept for <see cref="ToCriteria"/> and existing callers.
        /// Writing it replaces the multi-select set; "All" (or blank) clears it.
        /// </summary>
        public string FilterCategory
        {
            get => _filterCategory;
            set
            {
                if (!SetProperty(ref _filterCategory, value)) return;

                _selectedCategories.Clear();
                if (!string.IsNullOrWhiteSpace(value) && !value.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    _selectedCategories.Add(value);
                }
                SyncCategoryOptionFlags();
                OnFiltersChanged();
            }
        }

        // Multi-select category filter. Empty means "no category constraint" — the same thing
        // "All" used to mean.
        private readonly HashSet<string> _selectedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private ObservableCollection<CategoryFilterOption> _categoryOptions = new ObservableCollection<CategoryFilterOption>();
        public ObservableCollection<CategoryFilterOption> CategoryOptions { get => _categoryOptions; set => SetProperty(ref _categoryOptions, value); }

        private string _categorySearch = "";
        /// <summary>Typeahead over <see cref="CategoryOptions"/> — reaches a category without scrolling.</summary>
        public string CategorySearch
        {
            get => _categorySearch;
            set { if (SetProperty(ref _categorySearch, value)) OnPropertyChanged(nameof(VisibleCategoryOptions)); }
        }

        public IEnumerable<CategoryFilterOption> VisibleCategoryOptions =>
            string.IsNullOrWhiteSpace(CategorySearch)
                ? CategoryOptions
                : CategoryOptions.Where(o => o.Name.Contains(CategorySearch, StringComparison.OrdinalIgnoreCase));

        /// <summary>Label for the collapsed dropdown button.</summary>
        public string CategoryFilterSummary => _selectedCategories.Count switch
        {
            0 => "All categories",
            1 => _selectedCategories.First(),
            _ => $"{_selectedCategories.Count} categories"
        };

        private bool _isCategoryDropDownOpen;
        public bool IsCategoryDropDownOpen { get => _isCategoryDropDownOpen; set => SetProperty(ref _isCategoryDropDownOpen, value); }

        /// <summary>
        /// Rebuilds the option list from the current preview rows, carrying the ticks over and
        /// attaching per-category row counts.
        /// </summary>
        internal void RebuildCategoryOptions()
        {
            foreach (CategoryFilterOption existing in CategoryOptions) existing.PropertyChanged -= OnCategoryOptionChanged;

            List<CategoryFilterOption> rebuilt = PreviewItems
                .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new CategoryFilterOption
                {
                    Name = g.Key,
                    Count = g.Count(),
                    IsSelected = _selectedCategories.Contains(g.Key)
                })
                .ToList();

            CategoryOptions = new ObservableCollection<CategoryFilterOption>(rebuilt);
            foreach (CategoryFilterOption option in CategoryOptions) option.PropertyChanged += OnCategoryOptionChanged;

            OnPropertyChanged(nameof(VisibleCategoryOptions));
            OnPropertyChanged(nameof(CategoryFilterSummary));
        }

        private void OnCategoryOptionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CategoryFilterOption.IsSelected)) return;
            if (sender is not CategoryFilterOption option) return;

            if (option.IsSelected) _selectedCategories.Add(option.Name);
            else _selectedCategories.Remove(option.Name);

            _filterCategory = _selectedCategories.Count == 1 ? _selectedCategories.First() : "All";
            OnPropertyChanged(nameof(FilterCategory));
            OnFiltersChanged();
        }

        private void SyncCategoryOptionFlags()
        {
            foreach (CategoryFilterOption option in CategoryOptions)
            {
                option.PropertyChanged -= OnCategoryOptionChanged;
                option.IsSelected = _selectedCategories.Contains(option.Name);
                option.PropertyChanged += OnCategoryOptionChanged;
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

        // R8: per-column text filters. Each routes through SetColumnFilter, which
        // AND-combines them with the category selection in MatchesAllFilters. The plumbing
        // already existed and nothing in the XAML reached it.
        private string _filterColType = "";
        public string FilterColType { get => _filterColType; set { if (SetProperty(ref _filterColType, value)) SetTextFilter("Type", value, r => r.Type); } }

        private string _filterColCategory = "";
        public string FilterColCategory { get => _filterColCategory; set { if (SetProperty(ref _filterColCategory, value)) SetTextFilter("Category", value, r => r.Category); } }

        private string _filterColOriginal = "";
        public string FilterColOriginal { get => _filterColOriginal; set { if (SetProperty(ref _filterColOriginal, value)) SetTextFilter("Original", value, r => r.OriginalValue); } }

        private string _filterColNew = "";
        public string FilterColNew { get => _filterColNew; set { if (SetProperty(ref _filterColNew, value)) SetTextFilter("New", value, r => r.NewValue); } }

        private string _filterColStatus = "";
        public string FilterColStatus { get => _filterColStatus; set { if (SetProperty(ref _filterColStatus, value)) SetTextFilter("Status", value, r => r.Status); } }

        private void SetTextFilter(string key, string value, Func<ElementRowViewModel, string> selector)
        {
            SetColumnFilter(key, string.IsNullOrWhiteSpace(value)
                ? null
                : row => (selector(row) ?? string.Empty).Contains(value, StringComparison.OrdinalIgnoreCase));
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(ActiveFilterSummary));
        }

        /// <summary>True when anything is narrowing the grid — drives the "filters active" chip.</summary>
        public bool HasActiveFilters =>
            _selectedCategories.Count > 0
            || !string.IsNullOrWhiteSpace(FilterName)
            || !string.IsNullOrWhiteSpace(FilterColType)
            || !string.IsNullOrWhiteSpace(FilterColCategory)
            || !string.IsNullOrWhiteSpace(FilterColOriginal)
            || !string.IsNullOrWhiteSpace(FilterColNew)
            || !string.IsNullOrWhiteSpace(FilterColStatus);

        public string ActiveFilterSummary => HasActiveFilters ? "Filters active" : string.Empty;

        /// <summary>Clears every filter in one action (R11).</summary>
        internal void ClearFilters()
        {
            _selectedCategories.Clear();
            SyncCategoryOptionFlags();
            _filterCategory = "All";
            OnPropertyChanged(nameof(FilterCategory));

            CategorySearch = "";
            FilterColType = "";
            FilterColCategory = "";
            FilterColOriginal = "";
            FilterColNew = "";
            FilterColStatus = "";
            FilterName = "";

            OnFiltersChanged();
        }

        /// <summary>Re-evaluates the view and every derived count/indicator after a filter change.</summary>
        private void OnFiltersChanged()
        {
            _previewView?.Refresh();
            RaiseCountsChanged();
            OnPropertyChanged(nameof(CategoryFilterSummary));
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(ActiveFilterSummary));
        }

        public ICommand ClearFiltersCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand InvertSelectionCommand { get; }
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
            RaiseCountsChanged();
        }

        private bool MatchesAllFilters(ElementRowViewModel row)
        {
            // 1. Category dropdown — multi-select. Empty set means no constraint.
            if (_selectedCategories.Count > 0 && !_selectedCategories.Contains(row.Category))
            {
                return false;
            }

            // 2. AND-combined per-column predicates
            foreach (var p in _columnFilters.Values)
            {
                if (!p(row)) return false;
            }
            return true;
        }

        public SearchReplaceViewModel()
        {
            ReplaceRule.PropertyChanged += RuleChanged;
            RemoveRule.PropertyChanged += RuleChanged;
            AddRule.PropertyChanged += RuleChanged;
            NumberingRule.PropertyChanged += RuleChanged;
            CaseRule.PropertyChanged += RuleChanged;
            Title = "Batch Rename";

            ClearFiltersCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ClearFilters);
            SelectAllCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectAll);
            InvertSelectionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(InvertSelection);
            SelectNoneCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectNone);
            ReplaceSpacesCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ReplaceSpaces);
            ReplaceSpacesInReplaceTextCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ReplaceSpacesInReplaceText);
        }

        /// <summary>
        /// Rows the user can actually act on: currently visible through the filter, and not
        /// skipped by ProcessPreview. Materialized because callers mutate while iterating.
        /// </summary>
        /// <remarks>
        /// Deliberately reads <see cref="PreviewView"/>, not <c>PreviewItems</c>. Selecting
        /// over the unfiltered collection is how "filter to 12 rows, hit Select All" used to
        /// select four thousand.
        /// </remarks>
        internal List<ElementRowViewModel> VisibleActionableRows =>
            PreviewView.Cast<ElementRowViewModel>().Where(r => r.IsRenameable).ToList();

        private void SelectAll() => BulkSetChecked(VisibleActionableRows, _ => true);
        private void SelectNone() => BulkSetChecked(VisibleActionableRows, _ => false);
        private void InvertSelection() => BulkSetChecked(VisibleActionableRows, r => !r.IsChecked);

        /// <summary>
        /// Applies a check decision across many rows, raising the derived counts once at the
        /// end rather than once per row.
        /// </summary>
        /// <remarks>
        /// Without the suppression this is O(n^2): every row's IsChecked notification would
        /// recompute CheckedCount by walking the whole view. At ~1,900 rows that is ~3.6M
        /// operations for one Select All.
        /// </remarks>
        internal void BulkSetChecked(IEnumerable<ElementRowViewModel> rows, Func<ElementRowViewModel, bool> value)
        {
            _suppressCountNotifications = true;
            try
            {
                foreach (ElementRowViewModel row in rows)
                {
                    if (!row.IsRenameable) continue;
                    row.IsChecked = value(row);
                }
            }
            finally
            {
                _suppressCountNotifications = false;
                RaiseCountsChanged();
            }
        }

        /// <summary>
        /// Sets every row between <paramref name="from"/> and <paramref name="to"/> inclusive,
        /// in the grid's current visual order, to <paramref name="value"/>. Backs shift-click
        /// on the Sel column.
        /// </summary>
        internal void ToggleRange(ElementRowViewModel from, ElementRowViewModel to, bool value)
        {
            if (from == null || to == null) return;

            List<ElementRowViewModel> visible = PreviewView.Cast<ElementRowViewModel>().ToList();
            int a = visible.IndexOf(from);
            int b = visible.IndexOf(to);
            if (a < 0 || b < 0) return;
            if (a > b) { int t = a; a = b; b = t; }

            BulkSetChecked(visible.GetRange(a, b - a + 1), _ => value);
        }
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
            // Category choices come from CategoryOptions, rebuilt from the preview rows with
            // per-category counts. The old flat AvailableCategories list had no consumer left.
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

        /// <summary>Rows currently visible through the active filters.</summary>
        public int VisibleCount => PreviewView.Cast<object>().Count();

        /// <summary>Visible rows that are ticked — what Apply will actually rename.</summary>
        public int CheckedCount => PreviewView.Cast<ElementRowViewModel>().Count(r => r.IsChecked);

        private bool _suppressCountNotifications;

        internal void RaiseCountsChanged()
        {
            if (_suppressCountNotifications) return;
            OnPropertyChanged(nameof(VisibleCount));
            OnPropertyChanged(nameof(CheckedCount));
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ElementRowViewModel.IsChecked)) RaiseCountsChanged();
        }

        /// <summary>
        /// Swaps the preview rows and rebinds per-row change notification so the derived
        /// counts follow individual checkbox clicks. Unsubscribes the outgoing rows first —
        /// they outlive the collection otherwise and leak for the life of the dialog.
        /// </summary>
        internal void SetPreviewRows(IList<ElementRowViewModel> rows)
        {
            foreach (ElementRowViewModel row in PreviewItems) row.PropertyChanged -= OnRowPropertyChanged;
            PreviewItems.ReplaceAll(rows);
            foreach (ElementRowViewModel row in PreviewItems) row.PropertyChanged += OnRowPropertyChanged;
            RebuildCategoryOptions();
            RaiseCountsChanged();
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

        /// <summary>
        /// Returns a human-readable message when the Replace rule's pattern will not compile,
        /// or null when it is fine.
        /// </summary>
        /// <remarks>
        /// Without this, a half-typed pattern surfaced as "Error loading preview: parsing …"
        /// from the catch-all — technically the regex message, buried under a generic prefix
        /// that pointed at the wrong thing.
        /// </remarks>
        internal string? ValidateReplaceRegex()
        {
            if (!ReplaceRule.IsActive || !ReplaceRule.UseRegex) return null;
            if (string.IsNullOrEmpty(ReplaceRule.FindText)) return null;

            try
            {
                _ = new Regex(ReplaceRule.FindText);
                return null;
            }
            catch (ArgumentException ex)
            {
                return $"Invalid regular expression: {ex.Message}";
            }
        }

        private async Task UpdatePreviewAsync()
        {
            if (_service == null || _cachedElements == null) return;

            string? regexError = ValidateReplaceRegex();
            if (regexError != null)
            {
                ValidationMessage = regexError;
                return;
            }
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
                    SetPreviewRows(results);
                    // Materialize/refresh the ICollectionView so sort + filter apply.
                    _ = PreviewView; // ensure created
                    _previewView?.Refresh();
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ValidationMessage = $"Error loading preview: {ex.Message}"; }
        }

        public override void Apply()
        {
            if (CheckedCount == 0)
            {
                ValidationMessage = "Select at least one item to rename.";
                return;
            }
            base.Apply();
        }
    }
}
