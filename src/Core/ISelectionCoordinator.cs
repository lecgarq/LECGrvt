using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace LECG.Core
{
    public interface ISelectionCoordinator
    {
        IList<Reference> PickObjects(Window owner, UIDocument uiDoc, ObjectType objectType, ISelectionFilter? filter, string prompt);

        Reference? PickObject(Window owner, UIDocument uiDoc, ObjectType objectType, ISelectionFilter? filter, string prompt);
    }
}
