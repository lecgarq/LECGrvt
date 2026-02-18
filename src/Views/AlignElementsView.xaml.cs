using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;
using Autodesk.Revit.UI.Selection;

namespace LECG.Views
{
    public partial class AlignElementsView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public AlignElementsView(AlignElementsViewModel vm, UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(vm);
            ArgumentNullException.ThrowIfNull(uiDoc);

            InitializeComponent();
            DataContext = vm;
            _uiDoc = uiDoc;
            
            // 1. VM Close Interaction
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

            // 2. Reference Selection Request
            vm.ReferenceSelection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                    Reference r = _uiDoc.Selection.PickObject(ObjectType.Element, "Select Reference Element");
                    vm.SetReference(r, _uiDoc.Document);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally { ShowDialog(); }
            };

            // 3. Target Selection Request
            vm.TargetSelection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                    IList<Reference> refs = _uiDoc.Selection.PickObjects(ObjectType.Element, "Select Target Elements");
                    vm.SetTargets(refs, _uiDoc.Document);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally { ShowDialog(); }
            };
        }
    }
}
