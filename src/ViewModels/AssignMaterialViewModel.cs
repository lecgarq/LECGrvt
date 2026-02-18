using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels.Components;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace LECG.ViewModels
{
    public partial class AssignMaterialViewModel : BaseViewModel
    {
        private readonly IMaterialService _service;
        
        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public List<Reference> SelectedRefs { get; private set; } = new List<Reference>();

        public bool ShouldRun { get; private set; }
        public bool CanRun => Selection.HasSelection;

        public AssignMaterialViewModel(IMaterialService service)
        {
            _service = service;
            Title = "ASSIGN MATERIAL";
            Selection.ElementName = "Elements";

            Selection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
        }

        public void SetSelection(IList<Reference> refs)
        {
            ArgumentNullException.ThrowIfNull(refs);

            SelectedRefs = refs.ToList();
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
