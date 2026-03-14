using System;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace LECG.Core
{
    public class SelectionCoordinator : ISelectionCoordinator
    {
        public IList<Reference> PickObjects(Window owner, UIDocument uiDoc, ObjectType objectType, ISelectionFilter? filter, string prompt)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

            owner.Hide();
            try
            {
                return filter == null
                    ? uiDoc.Selection.PickObjects(objectType, prompt)
                    : uiDoc.Selection.PickObjects(objectType, filter, prompt);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new List<Reference>();
            }
            finally
            {
                Restore(owner);
            }
        }

        public Reference? PickObject(Window owner, UIDocument uiDoc, ObjectType objectType, ISelectionFilter? filter, string prompt)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

            owner.Hide();
            try
            {
                return filter == null
                    ? uiDoc.Selection.PickObject(objectType, prompt)
                    : uiDoc.Selection.PickObject(objectType, filter, prompt);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
            finally
            {
                Restore(owner);
            }
        }

        private static void Restore(Window owner)
        {
            try
            {
                owner.Visibility = System.Windows.Visibility.Visible;
                owner.Activate();
            }
            catch (Exception)
            {
            }
        }
    }
}
