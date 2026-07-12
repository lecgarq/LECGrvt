using System.Collections.Generic;
using System;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class DivideToposolidView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public DivideToposolidView(DivideToposolidViewModel vm, ISelectionCoordinator selectionCoordinator)
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
                    "Select Toposolids to Divide");
                if (refs.Count > 0)
                {
                    Document doc = UiDocument.Document;
                    var elements = new List<Element>();

                    foreach (Reference reference in refs)
                    {
                        Element? element = doc.GetElement(reference);
                        if (element != null)
                        {
                            elements.Add(element);
                        }
                    }

                    vm.SetSelectedElements(elements);
                }
            };
        }
    }
}
