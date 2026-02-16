using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Services;
using Autodesk.Revit.DB;
using System.Linq;

namespace LECG.ViewModels
{
    public partial class ReplaceItem : ObservableObject
    {
        [ObservableProperty] private bool _isChecked = true;
        public string ElementName { get; set; } = "";
        public string OriginalValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public long ElementId { get; set; }
    }

    public partial class SearchReplaceViewModel : BaseViewModel
    {
        private ISearchReplaceService _service = null!;
        private Document _doc = null!;
        private List<ElementData> _cachedElements = new List<ElementData>();

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

        // Selection Filters
        [ObservableProperty] private string _filterName = "";
        [ObservableProperty] private string _filterCategory = "All"; // Default to All

        [ObservableProperty] private ObservableCollection<ReplaceItem> _previewItems = new ObservableCollection<ReplaceItem>();
        [ObservableProperty] private ObservableCollection<string> _availableCategories = new ObservableCollection<string>();

        public bool ShouldRun { get; private set; }

        public SearchReplaceViewModel() // Default constructor for design time? Or usually we inject service in code.
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
        
        // Scope Change Handlers
        partial void OnScopeTypeNameChanged(bool value) => RefreshScope();
        partial void OnScopeFamilyNameChanged(bool value) => RefreshScope();
        partial void OnScopeViewNameChanged(bool value) => RefreshScope();
        partial void OnScopeSheetNameChanged(bool value) => RefreshScope();

        // Filter Change Handlers
        partial void OnFilterNameChanged(string value) => UpdatePreview();
        partial void OnFilterCategoryChanged(string value) => UpdatePreview();

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
            _cachedElements = _service.CollectBaseElements(_doc, ScopeTypeName, ScopeFamilyName, ScopeViewName, ScopeSheetName);
            
            // 2. Update Categories
            var cats = _service.GetUniqueCategories(_cachedElements);
            AvailableCategories.Clear();
            AvailableCategories.Add("All"); 
            foreach (var c in cats) AvailableCategories.Add(c);
            
            FilterCategory = "All";

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
                 // We can use a service or event for simple message, or just return.
                 // Ideally TaskDialog or MessageBox. For VM purity, we might use a dialog service, 
                 // but System.Windows.MessageBox is often used in Revit Addins for simple stuff.
                 System.Windows.MessageBox.Show("No items selected to rename.", "Batch Rename");
                 return;
             }
             
             ShouldRun = true;
             CloseAction?.Invoke();
        }
    }
}
