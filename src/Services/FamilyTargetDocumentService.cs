using System.IO;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyTargetDocumentService : IFamilyTargetDocumentService
    {
        public Document? Create(Document doc, string templatePath)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(templatePath);

            if (!File.Exists(templatePath))
            {
                Logger.Instance.Log($"Error: Template not found at {templatePath}");
                return null;
            }

            Document? targetFamilyDoc = doc.Application.NewFamilyDocument(templatePath);
            if (targetFamilyDoc == null)
            {
                Logger.Instance.Log("Error: Could not create new family document.");
                return null;
            }

            return targetFamilyDoc;
        }
    }
}
