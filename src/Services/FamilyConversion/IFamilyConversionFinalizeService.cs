using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionFinalizeService
    {
        void Finalize(Document sourceFamilyDoc, Document? targetFamilyDoc, string tempFamilyPath, bool isTemporary);
    }
}
