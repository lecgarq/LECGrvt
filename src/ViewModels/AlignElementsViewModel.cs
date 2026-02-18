using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels.Components;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System;
using System.Linq;

namespace LECG.ViewModels
{
    public partial class AlignElementsViewModel : BaseViewModel
    {
        private readonly IAlignElementsService _service;
        
        [ObservableProperty]
        private AlignMode _mode;

        public SelectionViewModel ReferenceSelection { get; } = new SelectionViewModel();
        public SelectionViewModel TargetSelection { get; } = new SelectionViewModel();
        
        // State
        public Reference? SelectedReference { get; private set; }
        public List<Reference> SelectedTargets { get; private set; } = new List<Reference>();

        public bool IsDistributeMode => Mode == AlignMode.DistributeHorizontally || Mode == AlignMode.DistributeVertically;
        public bool IsAlignMode => !IsDistributeMode;

        public bool ShouldRun { get; private set; }
        public bool CanRun 
        { 
            get 
            {
                if (IsDistributeMode) return TargetSelection.HasSelection && TargetSelection.SelectionCount >= 3;
                return ReferenceSelection.HasSelection && TargetSelection.HasSelection;
            }
        }

        public AlignElementsViewModel(IAlignElementsService service, AlignMode mode)
        {
            _service = service;
            Mode = mode;
            
            // Set Title based on Mode
            Title = GetTitle(mode);

            // Init Selection properties
            ReferenceSelection.ElementName = "Reference Element";
            TargetSelection.ElementName = "Target Elements";

            ReferenceSelection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
            TargetSelection.PropertyChanged += (s, e) => { 
                if (e.PropertyName == nameof(SelectionViewModel.HasSelection) || e.PropertyName == nameof(SelectionViewModel.SelectionCount)) 
                    OnPropertyChanged(nameof(CanRun)); 
            };
        }

        private string GetTitle(AlignMode mode)
        {
            switch (mode)
            {
                case AlignMode.Left: return "ALIGN LEFT";
                case AlignMode.Center: return "ALIGN CENTER";
                case AlignMode.Right: return "ALIGN RIGHT";
                case AlignMode.Top: return "ALIGN TOP";
                case AlignMode.Middle: return "ALIGN MIDDLE";
                case AlignMode.Bottom: return "ALIGN BOTTOM";
                case AlignMode.DistributeHorizontally: return "DISTRIBUTE HORIZONTALLY";
                case AlignMode.DistributeVertically: return "DISTRIBUTE VERTICALLY";
                default: return "ALIGN";
            }
        }

        public void SetReference(Reference r, Document doc)
        {
            ArgumentNullException.ThrowIfNull(r);
            ArgumentNullException.ThrowIfNull(doc);

            SelectedReference = r;
            ReferenceSelection.UpdateSelection(1);
            
            // Auto-exclude if already in targets? Or just warn? 
            // Logic requirement: "If user uses crossing window that includes Reference, exclude it."
            // We handle this when setting Targets.
        }

        public void SetTargets(IList<Reference> refs, Document doc)
        {
            ArgumentNullException.ThrowIfNull(refs);
            ArgumentNullException.ThrowIfNull(doc);

            var validRefs = new List<Reference>();
            
            foreach (var r in refs)
            {
                // If in Align Mode, exclude the Reference Element
                if (IsAlignMode && SelectedReference != null && r.ElementId == SelectedReference.ElementId)
                {
                    continue; // Skip the reference element
                }
                validRefs.Add(r);
            }

            SelectedTargets = validRefs;
            TargetSelection.UpdateSelection(validRefs.Count);
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
