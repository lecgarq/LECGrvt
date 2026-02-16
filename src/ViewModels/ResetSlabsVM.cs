using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core;

namespace LECG.ViewModels
{
    public partial class ResetSlabsVM : BaseViewModel
    {
        [ObservableProperty]
        private bool _duplicateElements;

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public IList<Reference> SelectedRefs { get; private set; } = new List<Reference>();
        
        public bool ShouldRun { get; private set; }
        public bool CanRun => Selection.HasSelection;

        public ResetSlabsVM()
        {
            Title = "RESET SLABS";
            Selection.ElementName = "Elements";
            Selection.Filter = new SelectionFilters.SlabFilter();
            
            Selection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
        }

        public void SetSelection(IList<Reference> refs)
        {
            SelectedRefs = refs;
            Selection.UpdateSelection(refs.Count);
        }

        [RelayCommand]
        private void ExecuteRun()
        {
            ShouldRun = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void ExecuteCancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }
    }
}
