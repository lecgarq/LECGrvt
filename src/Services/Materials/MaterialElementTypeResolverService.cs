using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialElementTypeResolverService : IMaterialElementTypeResolverService
    {
        public ElementType? Resolve(Document doc, ElementId typeId)
        {
            ArgumentNullException.ThrowIfNull(doc);

            return doc.GetElement(typeId) as ElementType;
        }
    }
}
