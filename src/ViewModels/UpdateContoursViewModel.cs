using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    public partial class UpdateContoursViewModel : BaseViewModel
    {
        // Mode selection
        [ObservableProperty] private bool _isApplyMode = true;
        
        // Types list expanded/collapsed
        [ObservableProperty] private bool _isTypesExpanded = true;
        
        // Toposolid Types
        public ObservableCollection<TypeSelectionItem> ToposolidTypes { get; } = new();
        
        // Primary Contours
        [ObservableProperty] private bool _enablePrimary = true;
        [ObservableProperty] private double _primaryInterval = 1.0; // meters
        
        // Secondary Contours
        [ObservableProperty] private bool _enableSecondary = true;
        [ObservableProperty] private double _secondaryInterval = 0.25; // meters
        
        // CloseAction inherited from BaseViewModel
        public bool ShouldRun { get; private set; }

        public UpdateContoursViewModel()
        {
            Title = "UPDATE CONTOURS";
        }

        [RelayCommand]
        private void Run()
        {
            ShouldRun = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void DoCancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void CheckAll()
        {
            foreach (var item in ToposolidTypes)
                item.IsSelected = true;
        }

        [RelayCommand]
        private void UncheckAll()
        {
            foreach (var item in ToposolidTypes)
                item.IsSelected = false;
        }

        [RelayCommand]
        private void ToggleTypesExpanded()
        {
            IsTypesExpanded = !IsTypesExpanded;
        }
    }

    public partial class TypeSelectionItem : ObservableObject
    {
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private bool _isSelected = true;
        public long ElementId { get; set; }
    }
}
