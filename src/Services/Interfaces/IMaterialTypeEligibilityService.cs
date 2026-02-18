using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialTypeEligibilityService
    {
        bool TryGetSkipReason(ElementType elemType, out string reason);
    }
}
