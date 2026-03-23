using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace LECG.Core
{
    internal static class SelectionSeedHelper
    {
        public static List<Element> GetSelectedElements(UIDocument uiDoc, ISelectionFilter? filter, int maxCount = int.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);

            var elements = new List<Element>();
            foreach (ElementId elementId in uiDoc.Selection.GetElementIds())
            {
                if (elements.Count >= maxCount)
                {
                    break;
                }

                Element? element = uiDoc.Document.GetElement(elementId);
                if (element == null)
                {
                    continue;
                }

                if (filter != null && !filter.AllowElement(element))
                {
                    continue;
                }

                elements.Add(element);
            }

            return elements;
        }

        public static List<Reference> GetSelectedReferences(UIDocument uiDoc, ISelectionFilter? filter, int maxCount = int.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);

            var references = new List<Reference>();
            foreach (ElementId elementId in uiDoc.Selection.GetElementIds())
            {
                if (references.Count >= maxCount)
                {
                    break;
                }

                Element? element = uiDoc.Document.GetElement(elementId);
                if (element == null)
                {
                    continue;
                }

                if (filter != null && !filter.AllowElement(element))
                {
                    continue;
                }

                references.Add(new Reference(element));
            }

            return references;
        }
    }
}
