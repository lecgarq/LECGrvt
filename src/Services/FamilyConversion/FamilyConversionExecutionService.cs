using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;
using System.IO;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class FamilyConversionExecutionService
    {
        private readonly FamilyTargetDocumentService _familyTargetDocumentService;
        private readonly FamilyGeometryCopyService _familyGeometryCopyService;
        private readonly FamilySaveLoadService _familySaveLoadService;
        private readonly ILogger _logger;

        public FamilyConversionExecutionService(
            FamilyTargetDocumentService familyTargetDocumentService,
            FamilyGeometryCopyService familyGeometryCopyService,
            FamilySaveLoadService familySaveLoadService,
            ILogger logger)
        {
            _familyTargetDocumentService = familyTargetDocumentService;
            _familyGeometryCopyService = familyGeometryCopyService;
            _familySaveLoadService = familySaveLoadService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public (Document? targetFamilyDoc, string tempFamilyPath) Execute(Document projectDoc, Family sourceFamily, Document sourceFamilyDoc, string templatePath, string targetFamilyName)
        {
            _logger.Log($"--- Starting Template-Based Conversion for: {targetFamilyName} ---", scope: "FamilyConversion");

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
                    _logger.LogWarning($"Family conversion execution failed for '{targetFamilyName}': {ex.Message}", scope: "FamilyConversion");
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
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
