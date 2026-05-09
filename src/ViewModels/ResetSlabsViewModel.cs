using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core;
using System;

namespace LECG.ViewModels
{
    public partial class ResetSlabsViewModel : BaseViewModel
    {
        [ObservableProperty]
        private bool _duplicateElements;

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public IList<Reference> SelectedRefs { get; private set; } = new List<Reference>();

        public bool CanRun => Selection.HasSelection;

        public ResetSlabsViewModel()
        {
            Title = "RESET SLABS";
            Selection.ElementName = "Elements";
            Selection.Filter = new SelectionFilters.SlabFilter();

            Selection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
        }

        public void SetSelection(IList<Reference> refs, Document doc)
        {
            ArgumentNullException.ThrowIfNull(refs);
            ArgumentNullException.ThrowIfNull(doc);

            SelectedRefs = refs;
            Selection.UpdateSelection(refs.Count);
            Selection.SetSelectionRows(refs, doc);
        }

    }
}
