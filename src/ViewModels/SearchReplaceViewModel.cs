using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using Autodesk.Revit.DB;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LECG.ViewModels
{
    public enum SearchFilterType
    {
        Contains,
        BeginsWith,
        EndsWith,
        DoesNotContain
    }

    public partial class ReplaceItem : ObservableObject
    {
        [ObservableProperty] private bool _isChecked = true;
        public string ElementName { get; set; } = "";
        public string OriginalValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public long ElementId { get; set; }
        public string Type { get; set; } = ""; // Added for disambiguation
    }

    public partial class SearchReplaceViewModel : BaseViewModel
    {
        private ISearchReplaceService _service = null!;
        private Document _doc = null!;
        private List<ElementData> _cachedElements = new List<ElementData>();
        private bool _isSettingScope; // Guard for radio-group exclusivity
        private CancellationTokenSource? _searchCts;

        // Rules
        public ReplaceRule ReplaceRule { get; } = new ReplaceRule();
        public RemoveRule RemoveRule { get; } = new RemoveRule();
        public AddRule AddRule { get; } = new AddRule();
        public NumberingRule NumberingRule { get; } = new NumberingRule();
        public CaseRule CaseRule { get; } = new CaseRule();

        // Scope settings
        [ObservableProperty] private bool _scopeTypeName = true; 
        [ObservableProperty] private bool _scopeFamilyName;
        [ObservableProperty] private bool _scopeViewName;
        [ObservableProperty] private bool _scopeSheetName;
        [ObservableProperty] private bool _scopeMaterialName;
        [ObservableProperty] private bool _scopeObjectStyleName;
        [ObservableProperty] private bool _scopeLineStyleName;
        [ObservableProperty] private bool _scopeFillPatternName;

        // Selection Filters
        [ObservableProperty] private string _filterName = "";
        [ObservableProperty] private string _filterCategory = "All"; // Default to All
        [ObservableProperty] private SearchFilterType _selectedFilterType = SearchFilterType.Contains;

        // Advanced Filters (Parameters)
        [ObservableProperty] private string _filterParamGroup = "All";
        [ObservableProperty] private ObservableCollection<string> _availableParamGroups = new ObservableCollection<string>();
        
        [ObservableProperty] private int _filterIsInstanceIndex = 0; // 0=All, 1=Instance, 2=Type
        [ObservableProperty] private int _filterIsReadOnlyIndex = 0; // 0=All, 1=Yes, 2=No

        public bool? FilterIsInstance => _filterIsInstanceIndex == 1 ? true : (_filterIsInstanceIndex == 2 ? false : (bool?)null);
        public bool? FilterIsReadOnly => _filterIsReadOnlyIndex == 1 ? true : (_filterIsReadOnlyIndex == 2 ? false : (bool?)null);

        // Advanced Filters (Views)
        [ObservableProperty] private string _filterViewType = "All";
        [ObservableProperty] private ObservableCollection<string> _availableViewTypes = new ObservableCollection<string>();
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
        private string _validationMessage = string.Empty;

        public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

        [RelayCommand]
        private void SelectAll()
        {
            if (PreviewItems == null) return;
            foreach (var item in PreviewItems) item.IsChecked = true;
        }

        [RelayCommand]
        private void SelectNone()
        {
            if (PreviewItems == null) return;
            foreach (var item in PreviewItems) item.IsChecked = false;
        }


        [ObservableProperty] private bool _scopeFamilyParameterName; // New Scope

        [RelayCommand]
        private void ReplaceSpaces()
        {
             if (!string.IsNullOrEmpty(FilterName))
             {
                 FilterName = FilterName.Replace(" ", "_");
             }
        }

        [RelayCommand]
        private void ReplaceSpacesInReplaceText()
        {
             if (ReplaceRule != null && !string.IsNullOrEmpty(ReplaceRule.ReplaceText))
             {
                 ReplaceRule.ReplaceText = ReplaceRule.ReplaceText.Replace(" ", "_");
             }
        }

        [ObservableProperty] private ObservableCollection<ReplaceItem> _previewItems = new ObservableCollection<ReplaceItem>();
        [ObservableProperty] private ObservableCollection<string> _availableCategories = new ObservableCollection<string>();

        public SearchReplaceViewModel()
        {
             // Hook up rule changes
             ReplaceRule.PropertyChanged += RuleChanged;
             RemoveRule.PropertyChanged += RuleChanged;
             AddRule.PropertyChanged += RuleChanged;
             NumberingRule.PropertyChanged += RuleChanged;
             CaseRule.PropertyChanged += RuleChanged;
             
             Title = "Batch Rename";
        }

        public RenameRuleContext ToContext()
        {
            return new RenameRuleContext(
                ReplaceRule,
                RemoveRule,
                AddRule,
                NumberingRule,
                CaseRule,
                ScopeTypeName,
                ScopeFamilyName,
                ScopeViewName,
                ScopeSheetName,
                ScopeMaterialName,
                ScopeObjectStyleName,
                ScopeLineStyleName,
                ScopeFillPatternName,
                ScopeFamilyParameterName,
                FilterName,
                FilterCategory,
                SelectedFilterType,
                FilterParamGroup,
                FilterIsInstance,
                FilterIsReadOnly,
                FilterViewType);
        }

        public SearchCriteria ToCriteria()
        {
            return new SearchCriteria
            {
                FilterName = FilterName,
                FilterCategory = FilterCategory,
                SelectedFilterType = SelectedFilterType,
                FilterParamGroup = FilterParamGroup,
                FilterIsInstance = FilterIsInstance,
                FilterIsReadOnly = FilterIsReadOnly,
                FilterViewType = FilterViewType,
                ScopeTypeName = ScopeTypeName,
                ScopeFamilyName = ScopeFamilyName,
                ScopeViewName = ScopeViewName,
                ScopeSheetName = ScopeSheetName,
                ScopeMaterialName = ScopeMaterialName,
                ScopeObjectStyleName = ScopeObjectStyleName,
                ScopeLineStyleName = ScopeLineStyleName,
                ScopeFillPatternName = ScopeFillPatternName,
                ScopeFamilyParameterName = ScopeFamilyParameterName
            };
        }

        private void RuleChanged(object? sender, PropertyChangedEventArgs e)
        {
            _ = UpdatePreviewAsync();
        }
        
        // Scope Change Handlers — radio-group exclusivity
        // When one scope is checked, all others are unchecked.
        private void SetExclusiveScope(Action activator)
        {
            if (_isSettingScope) return;
            _isSettingScope = true;

            // Turn all off
            ScopeTypeName = false;
            ScopeFamilyName = false;
            ScopeViewName = false;
            ScopeSheetName = false;
            ScopeMaterialName = false;
            ScopeObjectStyleName = false;
            ScopeLineStyleName = false;
            ScopeFillPatternName = false;
            ScopeFamilyParameterName = false;

            // Turn on the selected one
            activator();

            _isSettingScope = false;

            // Notify scope-dependent properties
            OnPropertyChanged(nameof(IsParameterScope));
            OnPropertyChanged(nameof(IsViewScope));

            RefreshScope();
        }

        partial void OnScopeTypeNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeTypeName = true); }
        partial void OnScopeFamilyNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeFamilyName = true); }
        partial void OnScopeViewNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeViewName = true); }
        partial void OnScopeSheetNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeSheetName = true); }
        partial void OnScopeMaterialNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeMaterialName = true); }
        partial void OnScopeObjectStyleNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeObjectStyleName = true); }
        partial void OnScopeLineStyleNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeLineStyleName = true); }
        partial void OnScopeFillPatternNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeFillPatternName = true); }
        partial void OnScopeFamilyParameterNameChanged(bool value) { if (value) SetExclusiveScope(() => ScopeFamilyParameterName = true); }

        // Computed scope flags for UI visibility
        public bool IsParameterScope => ScopeFamilyParameterName;
        public bool IsViewScope => ScopeViewName;

        partial void OnSelectedFilterTypeChanged(SearchFilterType value) => _ = UpdatePreviewAsync();

        // Filter Change Handlers
        partial void OnFilterNameChanged(string value) => _ = UpdatePreviewAsync();
        partial void OnFilterCategoryChanged(string value) => _ = UpdatePreviewAsync();
        partial void OnFilterParamGroupChanged(string value) => _ = UpdatePreviewAsync();
        partial void OnFilterIsInstanceIndexChanged(int value) => _ = UpdatePreviewAsync();
        partial void OnFilterIsReadOnlyIndexChanged(int value) => _ = UpdatePreviewAsync();
        partial void OnFilterViewTypeChanged(string value) => _ = UpdatePreviewAsync();

        public void Initialize(ISearchReplaceService service, Document doc)
        {
            _service = service;
            _doc = doc;
            RefreshScope(); // Initial Load
        }

        private void RefreshScope()
        {
            if (_service == null || _doc == null) return;
            
            // 1. Fetch Data
            _cachedElements = _service.CollectBaseElements(_doc, 
                ScopeTypeName, ScopeFamilyName, ScopeViewName, ScopeSheetName,
                ScopeMaterialName, ScopeObjectStyleName, ScopeLineStyleName, ScopeFillPatternName, ScopeFamilyParameterName);
            
            // 2. Update Categories
            var cats = _service.GetUniqueCategories(_cachedElements);
            AvailableCategories.Clear();
            AvailableCategories.Add("All"); 
            foreach (var c in cats) AvailableCategories.Add(c);
            
            FilterCategory = "All";

            // 3. Update scope-specific filters
            if (ScopeFamilyParameterName)
            {
                // Parameter Groups
                var groups = _cachedElements
                    .Where(e => e.Type == "FamilyParameter" && !string.IsNullOrEmpty(e.ParamGroup))
                    .Select(e => e.ParamGroup)
                    .Distinct()
                    .OrderBy(g => g);

                AvailableParamGroups.Clear();
                AvailableParamGroups.Add("All");
                foreach (var g in groups) AvailableParamGroups.Add(g);
                FilterParamGroup = "All";
            }

            if (ScopeViewName)
            {
                // View Types (Category field holds ViewType.ToString() for views)
                var viewTypes = _cachedElements
                    .Where(e => e.Type == "View" && !string.IsNullOrEmpty(e.Category))
                    .Select(e => e.Category)
                    .Distinct()
                    .OrderBy(v => v);

                AvailableViewTypes.Clear();
                AvailableViewTypes.Add("All");
                foreach (var vt in viewTypes) AvailableViewTypes.Add(vt);
                FilterViewType = "All";
            }

            // 4. Update Preview
            _ = UpdatePreviewAsync();
        }

        private async Task UpdatePreviewAsync()
        {
            if (_service == null || _cachedElements == null) return;

            // 1. Cancel previous search
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            try
            {
                // 2. Debounce (150ms)
                await Task.Delay(150, ct);

                // 3. Prepare DTOs (Safe to capture state here before Task.Run)
                var criteria = ToCriteria();
                var context = ToContext();

                // 4. Execute heavy filtering on background thread
                var results = await Task.Run(() => 
                    _service.ProcessPreview(_cachedElements, criteria, context, ct), ct);

                // 5. Update UI (ObservableCollection must be updated on UI thread)
                // CommunityToolkit.Mvvm usually handles this if current thread is UI, 
                // but we await Tas.Run so we are on a ThreadPool thread here.
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    if (ct.IsCancellationRequested) return;
                    ValidationMessage = string.Empty;
                    PreviewItems.Clear();
                    foreach (var r in results) PreviewItems.Add(r);
                });
            }
            catch (OperationCanceledException)
            {
                // Expected when user types fast
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Error loading preview: {ex.Message}";
            }
        }

        public override void Apply()
        {
            if (PreviewItems == null || !PreviewItems.Any(i => i.IsChecked))
            {
                ValidationMessage = "Select at least one item to rename.";
                return;
            }

            ValidationMessage = string.Empty;
            ShouldRun = true;
            CloseAction?.Invoke();
        }
    }
}
