using System.Globalization;
using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class OffsetElevationsView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public OffsetElevationsView(OffsetElevationsVM vm, UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(vm);
            ArgumentNullException.ThrowIfNull(uiDoc);

            InitializeComponent();
            DataContext = vm;
            _uiDoc = uiDoc;
            
             // Listen to VM events to handle window behavior
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

            // Selection request
            vm.Selection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                    IList<Reference> refs = _uiDoc.Selection.PickObjects(
                        Autodesk.Revit.UI.Selection.ObjectType.Element, 
                        vm.Selection.Filter,
                        "Select Elements to Offset");
                    
                    vm.SetSelection(refs);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally { ShowDialog(); }
            };

            TxtValue.Focus();
            TxtValue.SelectAll();
        }
    }
}
