using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;
using LECG.Core;

namespace LECG.ViewModels
{
    public partial class SimplifyPointsViewModel : BaseViewModel
    {
        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        
        public bool ShouldRun { get; private set; }

        // Helper for View to inject results
        public IList<Reference> SelectedRefs { get; private set; } = new List<Reference>();

        public bool CanRun => Selection.HasSelection;

        public SimplifyPointsViewModel()
        {
            Title = "SIMPLIFY POINTS";
            Selection.ElementName = "Toposolids";
            Selection.Filter = new SelectionFilters.ToposolidFilter(); 
            
            Selection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
        }

        public void SetSelection(IList<Reference> refs)
        {
            SelectedRefs = refs;
            Selection.UpdateSelection(refs.Count);
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
    }
}
