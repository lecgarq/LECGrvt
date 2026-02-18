using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core;

namespace LECG.ViewModels
{
    public partial class OffsetElevationsVM : BaseViewModel
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        private double _offsetValue = 0.0;
        
        [ObservableProperty]
        private bool _isAddition = true;

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public IList<Reference> SelectedRefs { get; private set; } = new List<Reference>();
        
        public bool ShouldRun { get; private set; }
        public bool CanRun => Selection.HasSelection && OffsetValue >= 0;

        public OffsetElevationsVM()
        {
            Title = "OFFSET ELEVATIONS";
            Selection.ElementName = "Elements";
            Selection.Filter = new SelectionFilters.SlabFilter();
            
            Selection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
        }

        public void SetSelection(IList<Reference> refs)
        {
            ArgumentNullException.ThrowIfNull(refs);

            SelectedRefs = refs;
            Selection.UpdateSelection(refs.Count);
        }

        [RelayCommand]
        private void ExecuteAdd()
        {
            IsAddition = true;
            Run();
        }

        [RelayCommand]
        private void ExecuteSubtract()
        {
            IsAddition = false;
            Run();
        }
        
        [RelayCommand]
        private void ExecuteCancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }

        private void Run()
        {
            ShouldRun = true;
            CloseAction?.Invoke();
        }
    }
}
