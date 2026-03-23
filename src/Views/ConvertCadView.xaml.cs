using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views.Base;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System;

namespace LECG.Views
{
    public partial class ConvertCadView : LecgWindow
    {
        private readonly ISelectionCoordinator _selectionCoordinator;

        public ConvertCadView(ConvertCadViewModel vm, ISelectionCoordinator selectionCoordinator)
        {
            ArgumentNullException.ThrowIfNull(vm);
            _selectionCoordinator = selectionCoordinator;

            InitializeComponent();
            DataContext = vm;

            BindClose(vm);

            // Hook up selection request
            vm.Selection.OnRequestSelect += (s, e) =>
            {
                if (UiDocument == null) return;
                Reference? r = _selectionCoordinator.PickObject(
                    this,
                    UiDocument,
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    null,
                    "Select an Imported CAD or Link",
                    restoreModalState: false);
                if (r != null)
                {
                    Element el = UiDocument.Document.GetElement(r);
                    vm.SetSelection(el);
                }
            };
        }

        public override void Initialize(UIDocument uiDoc)
        {
            base.Initialize(uiDoc);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
