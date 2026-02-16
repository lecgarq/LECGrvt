using System;
using System.Windows.Input;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;
using System.ComponentModel;

namespace LECG.ViewModels
{
    public partial class AlignEdgesViewModel : BaseViewModel
    {
        public SelectionViewModel TargetsSelection { get; } = new SelectionViewModel();
        public SelectionViewModel ReferenceSelection { get; } = new SelectionViewModel();

        public bool ShouldRun { get; private set; }

        public bool CanRun => TargetsSelection.HasSelection && ReferenceSelection.HasSelection;

        public AlignEdgesViewModel()
        {
            Title = "ALIGN EDGES";
            
            TargetsSelection.ElementName = "Toposolids";
            ReferenceSelection.ElementName = "Reference Surface";

            // Subscribe to children changes to update CanRun
            TargetsSelection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
            ReferenceSelection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
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
        
        // Helper for View to inject results
        public IList<Reference> TargetRefs { get; private set; } = new List<Reference>();
        public IList<Reference> ReferenceRefs { get; private set; } = new List<Reference>();

        public void SetTargets(IList<Reference> refs)
        {
            TargetRefs = refs;
            TargetsSelection.UpdateSelection(refs.Count);
        }

        public void SetReference(IList<Reference> refs) 
        {
            ReferenceRefs = refs;
            ReferenceSelection.UpdateSelection(refs.Count);
        }


    }
}
