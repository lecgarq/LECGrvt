using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyConversionExecutionService : IFamilyConversionExecutionService
    {
        private readonly IFamilyTargetDocumentService _familyTargetDocumentService;
        private readonly IFamilyGeometryCopyService _familyGeometryCopyService;
        private readonly IFamilySaveLoadService _familySaveLoadService;

        public FamilyConversionExecutionService(IFamilyTargetDocumentService familyTargetDocumentService, IFamilyGeometryCopyService familyGeometryCopyService, IFamilySaveLoadService familySaveLoadService)
        {
            _familyTargetDocumentService = familyTargetDocumentService;
            _familyGeometryCopyService = familyGeometryCopyService;
            _familySaveLoadService = familySaveLoadService;
        }

        public (Document? targetFamilyDoc, string tempFamilyPath) Execute(Document doc, Document sourceFamilyDoc, string templatePath, string targetFamilyName)
        {
            Document? targetFamilyDoc = _familyTargetDocumentService.Create(doc, templatePath);
            if (targetFamilyDoc == null)
            {
                return (null, string.Empty);
            }

            int copiedCount = _familyGeometryCopyService.CopyGeometry(sourceFamilyDoc, targetFamilyDoc);
            Logger.Instance.Log($"Found {copiedCount} geometry elements to copy.");

            string tempFamilyPath = _familySaveLoadService.SaveAndLoad(doc, targetFamilyDoc, targetFamilyName);
            return (targetFamilyDoc, tempFamilyPath);
        }
    }
}
