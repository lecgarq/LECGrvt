using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using System;

namespace LECG.Views
{
    public partial class AssignMaterialView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public AssignMaterialView(AssignMaterialViewModel vm, ISelectionCoordinator selectionCoordinator)
        {
            ArgumentNullException.ThrowIfNull(vm);
            _selectionCoordinator = selectionCoordinator;

            InitializeComponent();
            DataContext = vm;
            
            BindDialogClose(vm, () => vm.ShouldRun);

            // Selection Request
            vm.Selection.OnRequestSelect += (s, e) => {
                if (UiDocument == null) return;
                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    ObjectType.Element,
                    new SelectionFilters.MaterialHostFilter(),
                    "Select elements to assign materials");
                if (refs.Count > 0)
                {
                    vm.SetSelection(refs);
                }
            };
        }

        public override void Initialize(UIDocument uiDoc)
        {
            base.Initialize(uiDoc);
        }
    }
}
