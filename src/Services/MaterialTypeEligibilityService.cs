using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialTypeEligibilityService : IMaterialTypeEligibilityService
    {
        public bool TryGetSkipReason(ElementType elemType, out string reason)
        {
            reason = string.Empty;

            if (elemType is not HostObjAttributes hostType)
            {
                return false;
            }

            CompoundStructure? compoundStructure = hostType.GetCompoundStructure();
            if (compoundStructure == null || compoundStructure.GetLayers().Count <= 1)
            {
                return false;
            }

            reason = $"{elemType.Name} has {compoundStructure.GetLayers().Count} layers. Cannot assign single material.";
            return true;
        }
    }
}
