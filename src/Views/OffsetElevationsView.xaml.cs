using System.Globalization;
using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views.Base;
using System;

namespace LECG.Views
{
    public partial class OffsetElevationsView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public OffsetElevationsView(OffsetElevationsViewModel vm, ISelectionCoordinator selectionCoordinator)
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
                    "Select Elements to Offset");
                if (refs.Count > 0)
                {
                    vm.SetSelection(refs);
                }
            };

            TxtValue.Focus();
            TxtValue.SelectAll();
        }

        public override void Initialize(UIDocument uiDoc)
        {
            base.Initialize(uiDoc);
        }
    }
}
