using System;
using System.IO;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyTargetDocumentService : IFamilyTargetDocumentService
    {
        private readonly ILogger _logger;

        public FamilyTargetDocumentService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Document? Create(Document doc, string templatePath)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(templatePath);

            if (!File.Exists(templatePath))
            {
                _logger.LogError($"Error: Template not found at {templatePath}", scope: "FamilyTargetDocument");
                return null;
            }

            Document? targetFamilyDoc = doc.Application.NewFamilyDocument(templatePath);
            if (targetFamilyDoc == null)
            {
                _logger.LogError("Error: Could not create new family document.", scope: "FamilyTargetDocument");
                return null;
            }

            return targetFamilyDoc;
        }
    }
}
