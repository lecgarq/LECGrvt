using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyTargetDocumentService
    {
        Document? Create(Document doc, string templatePath);
    }
}
