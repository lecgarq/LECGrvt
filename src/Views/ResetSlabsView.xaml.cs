using System.Windows;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views.Base;
using System;

namespace LECG.Views
{
    public partial class ResetSlabsView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public ResetSlabsView(ResetSlabsViewModel vm, ISelectionCoordinator selectionCoordinator)
        {
            ArgumentNullException.ThrowIfNull(vm);
            _selectionCoordinator = selectionCoordinator;

            InitializeComponent();
            DataContext = vm;

            BindDialogClose(vm, () => vm.ShouldRun);

            // Selection request
            vm.Selection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;
                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    vm.Selection.Filter,
                    "Select Elements to Reset");
                if (refs.Count > 0)
                {
                    vm.SetSelection(refs, UiDocument.Document);
                }
            };
        }
    }
}
