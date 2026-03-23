using LECG.ViewModels;
using LECG.Views.Base;
using LECG.Core;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using LECG.Utils;
using System;
using System.Collections.Generic;

namespace LECG.Views
{
    public partial class CategoryChangerView : LecgWindow
    {
        private readonly CategoryChangerViewModel _viewModel;
        private readonly ISelectionCoordinator _selectionCoordinator;

        public CategoryChangerView(CategoryChangerViewModel viewModel, ISelectionCoordinator selectionCoordinator)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _selectionCoordinator = selectionCoordinator;

            InitializeComponent();
            DataContext = viewModel;

            BindClose(viewModel);

            viewModel.Selection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;
                IList<Reference> refs = _selectionCoordinator.PickObjects(
                    this,
                    UiDocument,
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new AnyFamilyInstanceFilter(),
                    "Select family instances to change category.",
                    restoreModalState: false);
                if (refs.Count > 0)
                {
                    viewModel.SetSelection(refs, UiDocument.Document);
                }
            };
        }

        public override void Initialize(UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            base.Initialize(uiDoc);
            _viewModel.LoadCategories(uiDoc.Document);
        }
    }
}
