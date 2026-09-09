using CommunityToolkit.Mvvm.ComponentModel;
using LECG.ViewModels.Components;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace LECG.ViewModels
{
    public partial class AssignMaterialViewModel : BaseViewModel
    {
        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public List<Reference> SelectedRefs { get; private set; } = new List<Reference>();

        public bool CanRun => Selection.HasSelection;

        public AssignMaterialViewModel()
        {
            Title = "ASSIGN MATERIAL";
            Selection.ElementName = "Elements";

            Selection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
        }

        public void SetSelection(IList<Reference> refs, Document doc)
        {
            ArgumentNullException.ThrowIfNull(refs);
            ArgumentNullException.ThrowIfNull(doc);

            SelectedRefs = refs.ToList();
            Selection.UpdateSelection(refs.Count);
            Selection.SetSelectionRows(refs, doc);
        }
    }
}
