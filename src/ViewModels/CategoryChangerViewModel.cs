using LECG.ViewModels.Components;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LECG.ViewModels
{
    public partial class CategoryChangerViewModel : BaseViewModel
    {
        private List<Category> _allCategories = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Category> _filteredCategories = new();

        [ObservableProperty]
        private Category? _selectedCategory;

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public IList<Reference>? SelectedRefs { get; set; }

        public Action<string>? OnLog { get; set; }
        public Action? OnShowLog { get; set; }
        public Document? Doc { get; set; }
        public LECG.Services.Interfaces.IFamilyEditorService? FamilyService { get; set; }

        public Action? RequestRun { get; set; }

        public bool ShouldRun { get; private set; }
        public bool CanRun => Selection.HasSelection && SelectedCategory != null;

        public CategoryChangerViewModel()
        {
            Title = "CATEGORY CHANGER [V6-EVENT]";
            Selection.ElementName = "Family Instances";

            Selection.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectionViewModel.HasSelection))
                    OnPropertyChanged(nameof(CanRun));
            };
        }

        protected override void Apply()
        {
            try
            {
                if (!CanRun) return;

                ShouldRun = true;
                
                // Instead of running logic here (UI context), 
                // we tell the command to raise the External Event (API context).
                RequestRun?.Invoke();
            }
            catch (Exception ex)
            {
                LECG.Services.Logging.Logger.Instance.Log($"Error in CategoryChanger Apply: {ex.Message}");
            }
        }

        public void LoadCategories(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            _allCategories = CategoryUtils.GetValidFamilyCategories(doc);
            FilteredCategories = new ObservableCollection<Category>(_allCategories);
        }

        public void SetSelection(IList<Reference> refs, Document doc)
        {
            ArgumentNullException.ThrowIfNull(refs);
            ArgumentNullException.ThrowIfNull(doc);

            SelectedRefs = refs;
            Selection.UpdateSelection(refs.Count);
        }

        partial void OnSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                FilteredCategories = new ObservableCollection<Category>(_allCategories);
            }
            else
            {
                var filtered = _allCategories.Where(c => c.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
                FilteredCategories = new ObservableCollection<Category>(filtered);
            }
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            OnPropertyChanged(nameof(CanRun));
        }

        protected override void Cancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }
    }
}
