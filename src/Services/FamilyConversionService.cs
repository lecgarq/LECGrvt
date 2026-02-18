using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;

namespace LECG.Services
{
    public class FamilyConversionService : IFamilyConversionService
    {
        private readonly IFamilyTemplatePathService _templatePathService;
        private readonly IFamilyTargetDocumentService _familyTargetDocumentService;
        private readonly IFamilySourceDocumentService _familySourceDocumentService;
        private readonly IFamilyGeometryCopyService _familyGeometryCopyService;
        private readonly IFamilySaveLoadService _familySaveLoadService;
        private readonly IFamilyConversionNamingService _familyConversionNamingService;
        private readonly IFamilyConversionLoggingService _familyConversionLoggingService;
        private readonly IFamilyConversionFinalizeService _familyConversionFinalizeService;

        public FamilyConversionService() : this(new FamilyTemplatePathService(), new FamilyTargetDocumentService(), new FamilySourceDocumentService(), new FamilyGeometryCopyService(new FamilyGeometryCollectionService(), new FamilyParameterSetupService()), new FamilySaveLoadService(new FamilySaveService(), new FamilyProjectLoadService(new FamilyLoadOptionsFactory())), new FamilyConversionNamingService(), new FamilyConversionLoggingService(), new FamilyConversionFinalizeService(new FamilyTempFileCleanupService()))
        {
        }

        public FamilyConversionService(IFamilyTemplatePathService templatePathService, IFamilyTargetDocumentService familyTargetDocumentService, IFamilySourceDocumentService familySourceDocumentService, IFamilyGeometryCopyService familyGeometryCopyService, IFamilySaveLoadService familySaveLoadService, IFamilyConversionNamingService familyConversionNamingService, IFamilyConversionLoggingService familyConversionLoggingService, IFamilyConversionFinalizeService familyConversionFinalizeService)
        {
            _templatePathService = templatePathService;
            _familyTargetDocumentService = familyTargetDocumentService;
            _familySourceDocumentService = familySourceDocumentService;
            _familyGeometryCopyService = familyGeometryCopyService;
            _familySaveLoadService = familySaveLoadService;
            _familyConversionNamingService = familyConversionNamingService;
            _familyConversionLoggingService = familyConversionLoggingService;
            _familyConversionFinalizeService = familyConversionFinalizeService;
        }

        public void ConvertFamily(Document doc, FamilyInstance instance, string customName, string templatePath, bool isTemporary)
        {
            if (instance == null) return;

            Family sourceFamily = instance.Symbol.Family;
            string sourceFamilyName = sourceFamily.Name;
            string targetFamilyName = _familyConversionNamingService.ResolveTargetFamilyName(sourceFamilyName, customName);

            _familyConversionLoggingService.LogStart(sourceFamilyName, targetFamilyName, templatePath, isTemporary);

            Document? sourceFamilyDoc = _familySourceDocumentService.Open(doc, sourceFamily);
            if (sourceFamilyDoc == null)
            {
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
                _familyConversionLoggingService.LogCriticalError(ex.Message, ex.StackTrace ?? "");
            }
            finally
            {
                _familyConversionFinalizeService.Finalize(sourceFamilyDoc, targetFamilyDoc, tempFamilyPath, isTemporary);
            }
        }

        public string GetTargetTemplatePath(Autodesk.Revit.ApplicationServices.Application app, Category category)
        {
            return _templatePathService.GetTargetTemplatePath(app, category);
        }

    }
}
