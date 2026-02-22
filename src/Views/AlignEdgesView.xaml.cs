using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;
using System;
using System.Linq;

namespace LECG.Views
{
    public partial class AlignEdgesView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public AlignEdgesView(AlignEdgesViewModel vm, UIDocument uiDoc)
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

            // Reference Selection
            vm.ReferenceSelection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                    IList<Reference> refs = _uiDoc.Selection.PickObjects(
                        Autodesk.Revit.UI.Selection.ObjectType.Element, 
                        new LECG.Core.SelectionFilters.ToposolidFilter(), 
                        "Select Reference Toposolid");
                    
                    if (refs != null && refs.Count > 0)
                    {
                        vm.SetReference(refs.First());
                    }
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
