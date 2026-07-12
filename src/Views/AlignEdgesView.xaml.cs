using System.Windows;
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

            // Source slab selection
            vm.TargetsSelection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;
                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new LECG.Core.SelectionFilters.SlabFilter(),
                    "Select source floors or toposolids");
                if (refs.Count > 0)
                {
                    vm.SetTargets(refs, UiDocument.Document);
                }
            };

            // Reference selection (supports floors, toposolids, and linked model containers)
            vm.ReferenceSelection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;

                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new LECG.Core.SelectionFilters.SlabOrLinkFilter(),
                    "Select reference floors, toposolids, or links");
                if (refs.Count > 0)
                {
                    vm.SetReferences(refs, UiDocument.Document);
                }
            };
        }
    }
}
