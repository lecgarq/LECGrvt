using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ISlabService
    {
        bool TryResetSlabShape(Element element, out string statusMessage);
        Element? DuplicateElement(Document doc, Element element);
    }
}
