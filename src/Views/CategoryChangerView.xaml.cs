using LECG.ViewModels;
using LECG.Views.Base;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using LECG.Utils;
using System;
using System.Collections.Generic;

namespace LECG.Views
{
    public partial class CategoryChangerView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public CategoryChangerView(CategoryChangerViewModel viewModel, UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(uiDoc);

            InitializeComponent();
            DataContext = viewModel;
            _uiDoc = uiDoc;

            viewModel.LoadCategories(uiDoc.Document);

            viewModel.CloseAction = () =>
            {
                if (IsLoaded)
                {
                    try { DialogResult = viewModel.ShouldRun; } catch { Close(); }
                }
                else
                {
                    Close();
                }
            };

            viewModel.Selection.OnRequestSelect += (s, e) =>
            {
                Hide();
                try
                {
                    Autodesk.Revit.UI.Selection.ISelectionFilter filter = new AnyFamilyInstanceFilter();

                    IList<Reference> refs = _uiDoc.Selection.PickObjects(
                        Autodesk.Revit.UI.Selection.ObjectType.Element,
                        filter,
                        "Select family instances to change category.");

                    viewModel.SetSelection(refs, _uiDoc.Document);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally
                {
                    try
                    {
                        this.Visibility = System.Windows.Visibility.Visible;
                        Activate();
                    }
                    catch { }
                }
            };
        }
    }
}
