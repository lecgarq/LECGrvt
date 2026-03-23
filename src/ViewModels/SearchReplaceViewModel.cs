using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;

namespace LECG.ViewModels
{
    public partial class ReplaceItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private bool _isChecked = true;
        public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
        public string ElementName { get; set; } = "";
        public string OriginalValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public long ElementId { get; set; }
        public string Type { get; set; } = "";
    }

    public partial class SearchReplaceViewModel : BaseViewModel
    {
        private ISearchReplaceService? _service;
        private Document? _doc;
        private List<ElementData> _cachedElements = new List<ElementData>();
        private bool _isSettingScope;
        private CancellationTokenSource? _searchCts;

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
        public string FilterCategory { get => _filterCategory; set { if (SetProperty(ref _filterCategory, value)) _ = UpdatePreviewAsync(); } }

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

        private ObservableCollection<ReplaceItem> _previewItems = new ObservableCollection<ReplaceItem>();
        public ObservableCollection<ReplaceItem> PreviewItems { get => _previewItems; set => SetProperty(ref _previewItems, value); }

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

        private void RefreshScope()
        {
            if (_service == null || _doc == null) return;
            _cachedElements = _service.CollectBaseElements(_doc, ScopeTypeName, ScopeFamilyName, ScopeViewName, ScopeSheetName, ScopeMaterialName, ScopeObjectStyleName, ScopeLineStyleName, ScopeFillPatternName, ScopeFamilyParameterName);
            var cats = _service.GetUniqueCategories(_cachedElements);
            AvailableCategories.Clear(); AvailableCategories.Add("All"); foreach (var c in cats) AvailableCategories.Add(c);
            FilterCategory = "All";

            if (ScopeFamilyParameterName)
            {
                var groups = _cachedElements.Where(e => e.Type == "FamilyParameter" && !string.IsNullOrEmpty(e.ParamGroup)).Select(e => e.ParamGroup).Distinct().OrderBy(g => g);
                AvailableParamGroups.Clear(); AvailableParamGroups.Add("All"); foreach (var g in groups) AvailableParamGroups.Add(g);
                FilterParamGroup = "All";
            }
            _ = UpdatePreviewAsync();
        }

        private async Task UpdatePreviewAsync()
        {
            if (_service == null || _cachedElements == null) return;
            _searchCts?.Cancel(); _searchCts = new CancellationTokenSource(); var ct = _searchCts.Token;
            try
            {
                await Task.Delay(150, ct);
                var results = await Task.Run(() => _service.ProcessPreview(_cachedElements, ToCriteria(), ToContext(), ct), ct);
                System.Windows.Application.Current.Dispatcher.Invoke(() => { if (ct.IsCancellationRequested) return; ValidationMessage = string.Empty; PreviewItems.Clear(); foreach (var r in results) PreviewItems.Add(r); });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ValidationMessage = $"Error loading preview: {ex.Message}"; }
        }

        public override void Apply() { if (PreviewItems == null || !PreviewItems.Any(i => i.IsChecked)) { ValidationMessage = "Select at least one item to rename."; return; } base.Apply(); }
    }
}
