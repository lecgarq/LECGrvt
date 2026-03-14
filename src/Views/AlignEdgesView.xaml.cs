using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views.Base;
using System;
using System.Linq;

namespace LECG.Views
{
    public partial class AlignEdgesView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public AlignEdgesView(AlignEdgesViewModel vm, ISelectionCoordinator selectionCoordinator)
        {
            ArgumentNullException.ThrowIfNull(vm);
            _selectionCoordinator = selectionCoordinator;

            InitializeComponent();
            DataContext = vm;
            
            BindDialogClose(vm, () => vm.ShouldRun);
            
            // Targets Selection
            vm.TargetsSelection.OnRequestSelect += (s, e) => {
                if (UiDocument == null) return;
                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new LECG.Core.SelectionFilters.ToposolidFilter(),
                    "Select Target Toposolids");
                if (refs.Count > 0)
                {
                    vm.SetTargets(refs);
                }
            };

            // Reference Selection
            vm.ReferenceSelection.OnRequestSelect += (s, e) => {
                if (UiDocument == null) return;
                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new LECG.Core.SelectionFilters.ToposolidFilter(),
                    "Select Reference Toposolid");
                if (refs.Count > 0)
                {
                    vm.SetReference(refs.First());
                }
            };
        }

        public override void Initialize(UIDocument uiDoc)
        {
            base.Initialize(uiDoc);
        }
    }
}
