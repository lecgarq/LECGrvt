using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.UI.Selection;

namespace LECG.ViewModels.Components
{
    public partial class SelectionViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _selectionCount;

        [ObservableProperty]
        private string _selectionStatus = "No items selected";

        [ObservableProperty]
        private bool _hasSelection;
        
        [ObservableProperty]
        private string _elementName = "Elements"; // e.g. "Walls", "Toposolids"

        public ISelectionFilter? Filter { get; set; }
        
        public event EventHandler? OnRequestSelect;

        public SelectionViewModel()
        {
        }

        public void UpdateSelection(int count)
        {
            SelectionCount = count;
            HasSelection = count > 0;
            SelectionStatus = count > 0 
                ? $"{count} {ElementName} selected" 
                : $"No {ElementName.ToLower()} selected";
        }

        [RelayCommand]
        private void Select()
        {
            OnRequestSelect?.Invoke(this, EventArgs.Empty);
        }
    }
}
