using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;
using System.IO;

namespace LECG.Services
{
    public class FamilyConversionService : IFamilyConversionService
    {
        private readonly IFamilyTemplatePathService _templatePathService;
        private readonly IFamilyTempFileCleanupService _tempFileCleanupService;
        private readonly IFamilyTargetDocumentService _familyTargetDocumentService;
        private readonly IFamilyGeometryCopyService _familyGeometryCopyService;
        private readonly IFamilySaveLoadService _familySaveLoadService;
        private readonly IFamilyConversionNamingService _familyConversionNamingService;

        public FamilyConversionService() : this(new FamilyTemplatePathService(), new FamilyTempFileCleanupService(), new FamilyTargetDocumentService(), new FamilyGeometryCopyService(new FamilyGeometryCollectionService(), new FamilyParameterSetupService()), new FamilySaveLoadService(new FamilySaveService(), new FamilyProjectLoadService(new FamilyLoadOptionsFactory())), new FamilyConversionNamingService())
        {
        }

        public FamilyConversionService(IFamilyTemplatePathService templatePathService, IFamilyTempFileCleanupService tempFileCleanupService, IFamilyTargetDocumentService familyTargetDocumentService, IFamilyGeometryCopyService familyGeometryCopyService, IFamilySaveLoadService familySaveLoadService, IFamilyConversionNamingService familyConversionNamingService)
        {
            _templatePathService = templatePathService;
            _tempFileCleanupService = tempFileCleanupService;
            _familyTargetDocumentService = familyTargetDocumentService;
            _familyGeometryCopyService = familyGeometryCopyService;
            _familySaveLoadService = familySaveLoadService;
            _familyConversionNamingService = familyConversionNamingService;
        }

        public void ConvertFamily(Document doc, FamilyInstance instance, string customName, string templatePath, bool isTemporary)
        {
            if (instance == null) return;

            Family sourceFamily = instance.Symbol.Family;
            string sourceFamilyName = sourceFamily.Name;
            string targetFamilyName = _familyConversionNamingService.ResolveTargetFamilyName(sourceFamilyName, customName);

            Logger.Instance.Log($"Converting Family: {sourceFamilyName} -> {targetFamilyName}");
            Logger.Instance.Log($"Template: {Path.GetFileName(templatePath)}");
            Logger.Instance.Log($"Temporary Mode: {isTemporary}");

            // 1. Open Source Family Document
            Document sourceFamilyDoc = doc.EditFamily(sourceFamily);
            if (sourceFamilyDoc == null)
            {
                Logger.Instance.Log("Error: Could not open source family for editing.");
                return;
            }

            Document? targetFamilyDoc = null;
            string tempFamilyPath = "";

            try
            {
                // 2. Create Target Family Document
                targetFamilyDoc = _familyTargetDocumentService.Create(doc, templatePath);
                if (targetFamilyDoc == null)
                {
                    return;
                }

                int copiedCount = _familyGeometryCopyService.CopyGeometry(sourceFamilyDoc, targetFamilyDoc);
                Logger.Instance.Log($"Found {copiedCount} geometry elements to copy.");

                tempFamilyPath = _familySaveLoadService.SaveAndLoad(doc, targetFamilyDoc, targetFamilyName);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Critical Error: {ex.Message}");
                Logger.Instance.Log(ex.StackTrace ?? "");
            }
            finally
            {
                sourceFamilyDoc.Close(false);
                if (targetFamilyDoc != null) targetFamilyDoc.Close(false);
                _tempFileCleanupService.Cleanup(tempFamilyPath, isTemporary);
            }
        }

        public string GetTargetTemplatePath(Autodesk.Revit.ApplicationServices.Application app, Category category)
        {
            return _templatePathService.GetTargetTemplatePath(app, category);
        }

    }
}
