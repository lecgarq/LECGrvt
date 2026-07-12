using System.Windows;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views.Base;
using Autodesk.Revit.UI.Selection;
using System;

namespace LECG.Views
{
    public partial class AlignElementsView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public AlignElementsView(AlignElementsViewModel vm, ISelectionCoordinator selectionCoordinator)
        {
            ArgumentNullException.ThrowIfNull(vm);
            _selectionCoordinator = selectionCoordinator;

            InitializeComponent();
            DataContext = vm;

            BindDialogClose(vm, () => vm.ShouldRun);

            // 2. Reference Selection Request
            vm.ReferenceSelection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;
                Reference? r = _selectionCoordinator.PickObject(
                    this,
                    UiDocument,
                    ObjectType.Element,
                    null,
                    "Select Reference Element");
                if (r != null)
                {
                    vm.SetReference(r, UiDocument.Document);
                }
            };

            // 3. Target Selection Request
            vm.TargetSelection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;
                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    ObjectType.Element,
                    null,
                    "Select Target Elements");
                if (refs.Count > 0)
                {
                    vm.SetTargets(refs, UiDocument.Document);
                }
            };
        }
    }
}
