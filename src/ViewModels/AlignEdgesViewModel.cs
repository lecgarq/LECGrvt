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

        [ObservableProperty]
        private bool _findMyEdge;

        public bool CanRun => TargetsSelection.HasSelection && (FindMyEdge || ReferenceSelection.HasSelection);
        public bool ShowReferenceSection => !FindMyEdge;

        public AlignEdgesViewModel()
        {
            Title = "ALIGN EDGES";

            TargetsSelection.ElementName = "Source slabs";
            ReferenceSelection.ElementName = "Reference slabs";

            // Subscribe to children changes to update CanRun
            TargetsSelection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
            ReferenceSelection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
        }

        partial void OnFindMyEdgeChanged(bool value)
        {
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(ShowReferenceSection));
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

        public void SetReferences(IList<Reference> refs)
        {
            ArgumentNullException.ThrowIfNull(refs);
            ReferenceRefs = refs;
            ReferenceSelection.UpdateSelection(refs.Count);
        }
    }
}
