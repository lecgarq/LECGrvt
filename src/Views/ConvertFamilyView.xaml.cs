using LECG.ViewModels;
using LECG.Views.Base;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using LECG.Utils;

namespace LECG.Views
{
    public partial class ConvertFamilyView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public ConvertFamilyView(ConvertFamilyViewModel viewModel, UIDocument uiDoc)
        {
            InitializeComponent();
            DataContext = viewModel;
            _uiDoc = uiDoc;
            
             // Listen to VM events to handle window behavior
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

            // Selection request
            viewModel.Selection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                    Autodesk.Revit.UI.Selection.ISelectionFilter filter = new LECG.Utils.FamilyInstanceFilter();
                    
                    Reference r = _uiDoc.Selection.PickObject(
                        Autodesk.Revit.UI.Selection.ObjectType.Element, 
                        filter, 
                        "Select a hosted family instance to convert.");
                    
                    viewModel.SetSelection(r, _uiDoc.Document);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally { ShowDialog(); }
            };
        }
    }
}
