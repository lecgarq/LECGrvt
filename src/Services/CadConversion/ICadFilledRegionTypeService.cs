using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadFilledRegionTypeService
    {
        FilledRegionType? GetOrCreateFilledRegionType(Document doc, Color color);
    }
}
