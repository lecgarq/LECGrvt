using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;
using System.IO;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class FamilyConversionExecutionService : IFamilyConversionExecutionService
    {
        private readonly IFamilyTargetDocumentService _familyTargetDocumentService;
        private readonly IFamilyGeometryCopyService _familyGeometryCopyService;
        private readonly IFamilySaveLoadService _familySaveLoadService;

        public FamilyConversionExecutionService(
            IFamilyTargetDocumentService familyTargetDocumentService,
            IFamilyGeometryCopyService familyGeometryCopyService,
            IFamilySaveLoadService familySaveLoadService)
        {
            _familyTargetDocumentService = familyTargetDocumentService;
            _familyGeometryCopyService = familyGeometryCopyService;
            _familySaveLoadService = familySaveLoadService;
        }

        public (Document? targetFamilyDoc, string tempFamilyPath) Execute(Document projectDoc, Family sourceFamily, Document sourceFamilyDoc, string templatePath, string targetFamilyName)
        {
            LECG.Services.Logging.Logger.Instance.Log($"--- Starting Template-Based Conversion for: {targetFamilyName} ---");

            Document? targetFamilyDoc = _familyTargetDocumentService.Create(projectDoc, templatePath);
            if (targetFamilyDoc != null)
            {
                try
                {
                    _familyGeometryCopyService.CopyGeometry(sourceFamilyDoc, targetFamilyDoc);
                    string path = _familySaveLoadService.SaveAndLoad(projectDoc, targetFamilyDoc, targetFamilyName);
                    return (targetFamilyDoc, path);
                }
                catch (Exception ex) when (IsExpectedFamilyConversionExecutionException(ex))
                {
                    Logger.Instance.LogWarning($"Family conversion execution failed for '{targetFamilyName}': {ex.Message}");
                    return (null, string.Empty);
                }
            }

            return (null, string.Empty);
        }

        private static bool IsExpectedFamilyConversionExecutionException(Exception ex)
        {
            return ex is ArgumentException
                or IOException
                or InvalidOperationException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
