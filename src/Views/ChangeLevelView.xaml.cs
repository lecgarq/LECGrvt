using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views.Base;
using System.Collections.Generic;
using System;

namespace LECG.Views
{
    public partial class ChangeLevelView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public ChangeLevelView(ChangeLevelViewModel vm, ISelectionCoordinator selectionCoordinator)
        {
            ArgumentNullException.ThrowIfNull(vm);
            _selectionCoordinator = selectionCoordinator;

            InitializeComponent();
            DataContext = vm;

            BindClose(vm);

            // Wire up Selection Request
            vm.Selection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;
                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    ObjectType.Element,
                    vm.Selection.Filter,
                    $"Select {vm.Selection.ElementName}");
                if (refs.Count > 0)
                {
                    vm.Selection.UpdateSelection(refs.Count);

                    // Convert refs to Elements
                    List<Element> elements = new List<Element>();
                    Document doc = UiDocument.Document;
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
            ArgumentNullException.ThrowIfNull(uiDoc);
            base.Initialize(uiDoc);
            (DataContext as ChangeLevelViewModel)?.Initialize(uiDoc.Document);
        }
    }
}
