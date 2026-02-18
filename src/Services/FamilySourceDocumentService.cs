using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilySourceDocumentService : IFamilySourceDocumentService
    {
        public Document? Open(Document doc, Family sourceFamily)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(sourceFamily);

            Document sourceFamilyDoc = doc.EditFamily(sourceFamily);
            if (sourceFamilyDoc == null)
            {
                Logger.Instance.Log("Error: Could not open source family for editing.");
                return null;
            }

            return sourceFamilyDoc;
        }
    }
}
