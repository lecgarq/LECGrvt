using System;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class FixPointsView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public FixPointsView(FixPointsViewModel vm, ISelectionCoordinator selectionCoordinator)
        {
            ArgumentNullException.ThrowIfNull(vm);
            _selectionCoordinator = selectionCoordinator;

            InitializeComponent();
            DataContext = vm;

            BindDialogClose(vm, () => vm.ShouldRun);

            // Wire up Selection Request
            vm.Selection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;

                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    ObjectType.Element,
                    vm.Selection.Filter,
                    "Select Floors/Toposolids");

                if (refs.Count > 0)
                {
                    Document doc = UiDocument.Document;
                    var elements = new List<Element>();

                    foreach (var r in refs)
                    {
                        var el = doc.GetElement(r);
                        if (el != null) elements.Add(el);
                    }

                    vm.SetSelectedElements(elements);
                }
            };
        }

        public override void Initialize(UIDocument uiDoc)
        {
            base.Initialize(uiDoc);
        }

    }
}
