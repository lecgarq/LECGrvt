using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class AlignEdgesView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public AlignEdgesView(AlignEdgesViewModel vm, UIDocument uiDoc)
        {
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
            
            // Targets Selection
            vm.TargetsSelection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                    IList<Reference> refs = _uiDoc.Selection.PickObjects(
                        Autodesk.Revit.UI.Selection.ObjectType.Element, 
                        new LECG.Core.SelectionFilters.ToposolidFilter(), 
                        "Select Target Toposolids");
                    
                    vm.SetTargets(refs);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally { ShowDialog(); }
            };

            // Reference Selection
            vm.ReferenceSelection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                     // Assuming we pick multiple references or single? Original code allowed multiple references?
                     // Loop in original code said "Select Reference Toposolids (can be multiple)"
                    IList<Reference> refs = _uiDoc.Selection.PickObjects(
                        Autodesk.Revit.UI.Selection.ObjectType.Element, 
                        new LECG.Core.SelectionFilters.ToposolidFilter(), 
                        "Select Reference Toposolids");
                    
                    vm.SetReference(refs);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally { ShowDialog(); }
            };
        }
    }
}
