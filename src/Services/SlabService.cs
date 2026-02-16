using System.Linq;
using Autodesk.Revit.DB;

namespace LECG.Services
{
    public class SlabService : ISlabService
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
             var copiedIds = ElementTransformUtils.CopyElements(doc, new[] { element.Id }, doc, Transform.Identity, new CopyPasteOptions());
             if (copiedIds.Count > 0)
             {
                 return doc.GetElement(copiedIds.First());
             }
             return null;
        }
    }
}
