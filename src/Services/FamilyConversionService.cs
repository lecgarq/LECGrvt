using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class FamilyConversionService : IFamilyConversionService
    {
        private readonly IFamilyTemplatePathService _templatePathService;
        private readonly IFamilySourceDocumentService _familySourceDocumentService;
        private readonly IFamilyConversionExecutionService _familyConversionExecutionService;
        private readonly IFamilyConversionNamingService _familyConversionNamingService;
        private readonly IFamilyConversionLoggingService _familyConversionLoggingService;
        private readonly IFamilyConversionFinalizeService _familyConversionFinalizeService;

        public FamilyConversionService() : this(new FamilyTemplatePathService(), new FamilySourceDocumentService(), new FamilyConversionExecutionService(new FamilyTargetDocumentService(), new FamilyGeometryCopyService(new FamilyGeometryCollectionService(), new FamilyParameterSetupService()), new FamilySaveLoadService(new FamilySaveService(), new FamilyProjectLoadService(new FamilyLoadOptionsFactory()))), new FamilyConversionNamingService(), new FamilyConversionLoggingService(), new FamilyConversionFinalizeService(new FamilyTempFileCleanupService()))
        {
        }

        public FamilyConversionService(IFamilyTemplatePathService templatePathService, IFamilySourceDocumentService familySourceDocumentService, IFamilyConversionExecutionService familyConversionExecutionService, IFamilyConversionNamingService familyConversionNamingService, IFamilyConversionLoggingService familyConversionLoggingService, IFamilyConversionFinalizeService familyConversionFinalizeService)
        {
            _templatePathService = templatePathService;
            _familySourceDocumentService = familySourceDocumentService;
            _familyConversionExecutionService = familyConversionExecutionService;
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
                (targetFamilyDoc, tempFamilyPath) = _familyConversionExecutionService.Execute(doc, sourceFamilyDoc, templatePath, targetFamilyName);
                if (targetFamilyDoc == null)
                {
                    return;
                }
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
