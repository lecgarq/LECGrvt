using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;

namespace LECG.ViewModels
{
    public partial class AlignEdgesViewModel : BaseViewModel
    {
        public SelectionViewModel TargetsSelection { get; } = new SelectionViewModel();
        public SelectionViewModel ReferenceSelection { get; } = new SelectionViewModel();

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

        public override void Apply()
        {
            ShouldRun = true;
            CloseAction?.Invoke();
        }

        public override void Cancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }
        
        // Helper for View to inject results
        public IList<Reference> TargetRefs { get; private set; } = new List<Reference>();
        public IList<Reference> ReferenceRefs { get; private set; } = new List<Reference>();

        public void SetTargets(IList<Reference> refs)
        {
            ArgumentNullException.ThrowIfNull(refs);
            TargetRefs = refs;
            TargetsSelection.UpdateSelection(refs.Count);
        }

        public void SetReference(Reference r)
        {
            ArgumentNullException.ThrowIfNull(r);
            ReferenceRefs = new List<Reference> { r };
            ReferenceSelection.UpdateSelection(1);
        }
    }
}
