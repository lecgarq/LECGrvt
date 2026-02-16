using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB;
using LECG.ViewModels;
using LECG.Views.Base;
using System.Collections.Generic;

namespace LECG.Views
{
    public partial class ChangeLevelView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public ChangeLevelView(ChangeLevelViewModel vm, UIDocument uiDoc)
        {
            InitializeComponent();
            DataContext = vm;
            _uiDoc = uiDoc;
            
            // Wire up CloseAction from BaseViewModel
            vm.CloseAction = () => Close();

            // Wire up Selection Request
            vm.Selection.OnRequestSelect += (s, e) => 
            {
                Hide();
                try
                {
                    IList<Reference> refs = _uiDoc.Selection.PickObjects(
                        ObjectType.Element, 
                        vm.Selection.Filter, 
                        $"Select {vm.Selection.ElementName}");
                    
                    vm.Selection.UpdateSelection(refs.Count);
                    
                    // TODO: The VM logic needs the actual elements for Run(), not just the count.
                    // Ideally, we pass the elements back to the VM here, or the VM manages logic differently.
                    // For now, let's inject them into the VM context if possible, or assume VM needs a method SetElements(List<ElementId>).
                    // BUT our ChangeLevelViewModel logic needs `List<Element>` to run.
                    // Use a new method on VM: `SetSelectedElements(List<Element>)`.
                    
                    // Convert refs to Elements
                    List<Element> elements = new List<Element>();
                    Document doc = _uiDoc.Document;
                    foreach(var r in refs)
                    {
                        var el = doc.GetElement(r);
                        if(el != null) elements.Add(el);
                    }
                    
                    vm.SetSelectedElements(elements);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User cancelled, do nothing
                }
                finally
                {
                    ShowDialog(); // Re-show as dialog
                }
            };
        }
    }
}
