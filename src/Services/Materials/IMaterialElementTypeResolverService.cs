using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialElementTypeResolverService
    {
        ElementType? Resolve(Document doc, ElementId typeId);
    }
}
