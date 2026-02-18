using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;
using Autodesk.Revit.UI.Selection;
using LECG.Core;

namespace LECG.Views
{
    public partial class AssignMaterialView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public AssignMaterialView(AssignMaterialViewModel vm, UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(vm);
            ArgumentNullException.ThrowIfNull(uiDoc);

            InitializeComponent();
            DataContext = vm;
            _uiDoc = uiDoc;
            
            // VM Interaction
            vm.CloseAction = () => 
            {
                if (IsLoaded)
                {
                    try { DialogResult = vm.ShouldRun; } catch { Close(); }
                }
                else
                {
                    Close();
                }
            };

            // Selection Request
            vm.Selection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                    IList<Reference> refs = _uiDoc.Selection.PickObjects(
                        ObjectType.Element, 
                        new SelectionFilters.MaterialHostFilter(), 
                        "Select elements to assign materials");
                    
                    vm.SetSelection(refs);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally { ShowDialog(); }
            };
        }
    }
}
