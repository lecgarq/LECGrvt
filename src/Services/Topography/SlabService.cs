using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SlabService
    {
        public bool TryResetSlabShape(Element element, out string statusMessage)
        {
            SlabShapeEditor? editor = null;
            if (element is Floor f) editor = f.GetSlabShapeEditor();
            else if (element is Toposolid t) editor = t.GetSlabShapeEditor();

            if (editor == null)
            {
                statusMessage = "Could not get SlabShapeEditor.";
                return false;
            }

            if (editor.IsEnabled)
            {
                editor.ResetSlabShape();
                statusMessage = "Slab Shape Reset Success.";
                return true;
            }

            statusMessage = "SlabShapeEditor is already flat or not enabled.";
            return false;
        }

        public Element? DuplicateElement(Document doc, Element element)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(element);

            var copiedIds = ElementTransformUtils.CopyElements(doc, new[] { element.Id }, doc, Transform.Identity, new CopyPasteOptions());
            foreach (ElementId copiedId in copiedIds)
            {
                return doc.GetElement(copiedId);
            }

            return null;
        }

        public List<XYZ> GetInteriorVertexPositions(Element element)
        {
            return GetVertexPositions(element, interiorOnly: true);
        }

        private List<XYZ> GetVertexPositions(Element element, bool interiorOnly)
        {
            ArgumentNullException.ThrowIfNull(element);

            var editor = GetEditor(element);
            if (editor == null || !editor.IsEnabled)
                return new List<XYZ>();

            var positions = new List<XYZ>();
            foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
            {
                if (interiorOnly && vertex.VertexType != SlabShapeVertexType.Interior)
                {
                    continue;
                }

                positions.Add(vertex.Position);
            }

            return positions;
        }

        public SlabShapeEditor? GetEditor(Element element)
        {
            if (element is Floor f)
                return f.GetSlabShapeEditor();

            if (element is Toposolid t)
                return t.GetSlabShapeEditor();

            return null;
        }
    }
}
