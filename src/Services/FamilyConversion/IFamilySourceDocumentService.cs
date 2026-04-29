using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilySourceDocumentService
    {
        Document? Open(Document doc, Family sourceFamily);
    }
}
