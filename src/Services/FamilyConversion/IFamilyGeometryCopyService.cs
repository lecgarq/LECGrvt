using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyGeometryCopyService
    {
        int CopyGeometry(Document sourceFamilyDoc, Document targetFamilyDoc);
    }
}
