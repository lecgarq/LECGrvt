using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilySourceDocumentService : IFamilySourceDocumentService
    {
        private readonly ILogger _logger;

        public FamilySourceDocumentService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Document? Open(Document doc, Family sourceFamily)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(sourceFamily);

            Document sourceFamilyDoc = doc.EditFamily(sourceFamily);
            if (sourceFamilyDoc == null)
            {
                _logger.LogError("Error: Could not open source family for editing.", scope: "FamilySourceDocument");
                return null;
            }

            return sourceFamilyDoc;
        }
    }
}
