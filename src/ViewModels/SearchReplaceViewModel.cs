using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Services;
using LECG.Services.Interfaces;
using Autodesk.Revit.DB;
using System.Linq;
using System;

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

        // Advanced Filters
        [ObservableProperty] private string _filterParamGroup = "All";
        [ObservableProperty] private ObservableCollection<string> _availableParamGroups = new ObservableCollection<string>();
        
        [ObservableProperty] private int _filterIsInstanceIndex = 0; // 0=All, 1=Instance, 2=Type
        [ObservableProperty] private int _filterIsReadOnlyIndex = 0; // 0=All, 1=Yes, 2=No

        public bool? FilterIsInstance => _filterIsInstanceIndex == 1 ? true : (_filterIsInstanceIndex == 2 ? false : (bool?)null);
        public bool? FilterIsReadOnly => _filterIsReadOnlyIndex == 1 ? true : (_filterIsReadOnlyIndex == 2 ? false : (bool?)null);

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

        public bool ShouldRun { get; private set; }

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

        private void RuleChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdatePreview();
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

        partial void OnSelectedFilterTypeChanged(SearchFilterType value) => UpdatePreview();

        // Filter Change Handlers
        partial void OnFilterNameChanged(string value) => UpdatePreview();
        partial void OnFilterCategoryChanged(string value) => UpdatePreview();
        partial void OnFilterParamGroupChanged(string value) => UpdatePreview();
        partial void OnFilterIsInstanceIndexChanged(int value) => UpdatePreview();
        partial void OnFilterIsReadOnlyIndexChanged(int value) => UpdatePreview();

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

            // 3. Update Available Parameter Groups (from cached elements)
            // Extract distinct ParamGroups from elements of type "FamilyParameter"
            var groups = _cachedElements
                .Where(e => e.Type == "FamilyParameter" && !string.IsNullOrEmpty(e.ParamGroup))
                .Select(e => e.ParamGroup)
                .Distinct()
                .OrderBy(g => g);

            AvailableParamGroups.Clear();
            AvailableParamGroups.Add("All");
            foreach (var g in groups) AvailableParamGroups.Add(g);
            
            FilterParamGroup = "All";

            // 3. Update Preview
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (_service == null || _cachedElements == null) return;

            var results = _service.ProcessPreview(_cachedElements, this);
            
            PreviewItems.Clear();
            foreach (var r in results) PreviewItems.Add(r);
        }

        protected override void Apply()
        {
             if (PreviewItems == null || !PreviewItems.Any(i => i.IsChecked))
             {
                 System.Windows.MessageBox.Show("No items selected to rename.", "Batch Rename");
                 return;
             }
             
             ShouldRun = true;
             CloseAction?.Invoke();
        }
    }
}
