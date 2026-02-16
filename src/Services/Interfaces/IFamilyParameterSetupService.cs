using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyParameterSetupService
    {
        void ConfigureTargetFamilyParameters(Document targetFamilyDoc);
    }
}
