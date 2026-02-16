using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FamilyParameterSetupService : IFamilyParameterSetupService
    {
        public void ConfigureTargetFamilyParameters(Document targetFamilyDoc)
        {
            Family targetFamilyRoot = targetFamilyDoc.OwnerFamily;

            Parameter? pWorkPlane = targetFamilyRoot.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED);
            if (pWorkPlane != null && !pWorkPlane.IsReadOnly) pWorkPlane.Set(1);

            Parameter? pAlwaysVertical = targetFamilyRoot.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL);
            if (pAlwaysVertical != null && !pAlwaysVertical.IsReadOnly) pAlwaysVertical.Set(0);
        }
    }
}
